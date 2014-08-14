using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class Metric
	{
		public float volume { get; private set; }
		public float area { get; private set; }
		public float mass { get; set; }
		public float cost { get; set; }
		public Bounds bounds { get; private set; }
		public Vector3 center { get { return bounds.center; } }
		public Vector3 extents { get { return bounds.extents; } }
		public Vector3 size { get { return bounds.size; } }
		public int CrewCapacity { get; private set; }
		public bool Empty { get { return volume == 0 && area == 0; } }
		
		public static Vector3[] BoundsEdges(Bounds b)
		{
			Vector3[] edges = new Vector3[8];
			Vector3 min = b.min;
			Vector3 max = b.max;
			edges[0] = new Vector3(min.x, min.y, min.z); //left-bottom-back
		    edges[1] = new Vector3(min.x, min.y, max.z); //left-bottom-front
		    edges[2] = new Vector3(min.x, max.y, min.z); //left-top-back
		    edges[3] = new Vector3(min.x, max.y, max.z); //left-top-front
		    edges[4] = new Vector3(max.x, min.y, min.z); //right-bottom-back
		    edges[5] = new Vector3(max.x, min.y, max.z); //right-bottom-front
		    edges[6] = new Vector3(max.x, max.y, min.z); //right-top-back
		    edges[7] = new Vector3(max.x, max.y, max.z); //right-top-front
			return edges;
		}
		
		public static Vector3[] BoundsEdges(Vector3 center, Vector3 size)
		{
			Bounds b = new Bounds(center, size);
			return BoundsEdges(b);
		}
		
		static void local2local(Transform _from, Transform _to, Vector3[] points)
		{
			for(int p=0; p < points.Length; p++)
				points[p] = _to.InverseTransformPoint(_from.TransformPoint(points[p]));
		}
		
		static float boundsVolume(Bounds b)
		{ return b.size.x*b.size.y*b.size.z; }

		static float boundsArea(Bounds b)
		{ return b.size.x*b.size.y*2+b.size.x*b.size.z*2+b.size.y*b.size.z*2; }
		
		static Bounds initBounds(Vector3[] edges)
		{
			Bounds b = new Bounds(edges[0], new Vector3());
			for(int i = 1; i < edges.Length; i++)
				b.Encapsulate(edges[i]);
			return b;
		}
		
		Bounds partsBounds(List<Part> parts, Transform vT)
		{
			mass = 0;
			cost = 0;
			CrewCapacity = 0;
			Bounds b = default(Bounds);
			foreach(Part p in parts)
			{
				if(p == null) continue; //EditorLogic.SortedShipList returns List<Part>{null} when all parts are deleted
				foreach(MeshFilter m in p.FindModelComponents<MeshFilter>())
				{
					if(m.renderer == null || !m.renderer.enabled) continue;
					Transform mT = m.transform;
					Vector3[] edges;
					//wheels are round and rotating >_<
					if(m.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0)
						edges = m.sharedMesh.vertices;
					else edges = BoundsEdges(m.sharedMesh.bounds);
					local2local(mT, vT, edges);
					if(b == default(Bounds)) b = initBounds(edges);
					else foreach(Vector3 edge in edges) b.Encapsulate(edge);
				}
				CrewCapacity += p.CrewCapacity;
				if(p.IsPhysicallySignificant())	mass += p.TotalMass();
				cost += p.TotalCost();
			}
			return b;
		}
		
		
		//empty metric
		public Metric()
		{
			bounds = new Bounds();
			volume = 0f;
			area   = 0f;
			mass   = 0f;
			CrewCapacity = 0;
		}
		
		//metric copy
		public Metric(Metric m)
		{
			bounds = new Bounds(m.bounds.center, m.bounds.size);
			volume = m.volume;
			area   = m.area;
			mass   = m.mass;
			CrewCapacity = m.CrewCapacity;
		}
		
		//metric from bounds
		public Metric(Bounds b, float m = 0f, int crew_capacity = 0)
		{
			bounds = new Bounds(b.center, b.size);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
			mass   = m;
			CrewCapacity = crew_capacity;
		}
		
		//metric from size
		public Metric(Vector3 center, Vector3 size, float m = 0f, int crew_capacity = 0)
		{
			bounds = new Bounds(center, size);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
			mass   = m;
			CrewCapacity = crew_capacity;
		}
		
		//metric form edges
		public Metric(Vector3[] edges, float m = 0f, int crew_capacity = 0)
		{
			bounds = initBounds(edges);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
			mass   = m;
			CrewCapacity = crew_capacity;
		}
		
		//metric from config node
		public Metric(ConfigNode node) { Load(node); }
		
		//mesh metric
		public Metric(Part part, string mesh_name)
		{
			MeshFilter m = part.FindModelComponent<MeshFilter>(mesh_name);
			if(m == null) { Utils.Log("[Metric] {0} does not have '{1}' mesh", part.name, mesh_name); return; }
			Transform pT = part.partTransform;
			Transform mT = m.transform;
			Vector3[] edges = BoundsEdges(m.sharedMesh.bounds);
			local2local(mT, pT, edges);
			bounds = initBounds(edges);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
			mass   = 0f;
		}
		
		//part metric
		public Metric(Part part)
		{
			Transform pT = part.partTransform;
			bounds = partsBounds(new List<Part>{part}, pT);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
		}
		
		//vessel metric
		public Metric(Vessel vessel)
		{
			Transform vT = vessel.vesselTransform;
			bounds = partsBounds(vessel.parts, vT);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
		}
		
		//in-editor vessel metric
		public Metric(List<Part> vessel)
		{
			Transform vT = vessel[0].partTransform;
			bounds = partsBounds(vessel, vT);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
		}
		
		//public methods
		public Bounds GetBounds() { return new Bounds(bounds.center, bounds.size); }
		
		public bool FitsSomehow(Metric other)
		{
			List<float>  D = new List<float>{size.x, size.y, size.z};
			List<float> _D = new List<float>{other.size.x, other.size.y, other.size.z};
			D.Sort(); _D.Sort();
			foreach(float d in D)
			{
				float ud = -1;
				foreach(float _d in _D)
				{
					if(d <= _d)
					{
						ud = _d;
						break;
					}
				}
				if(ud < 0) return false;
				_D.Remove(ud);
			}
			return true;
		}
		
		public bool FitsAligned(Transform this_T, Transform other_T, Metric other)
		{
			Vector3[] edges = BoundsEdges(Vector3.zero, bounds.size);
			foreach(Vector3 edge in edges)
			{
				Vector3 _edge = other_T.InverseTransformPoint(this_T.position+this_T.TransformDirection(edge));
				if(!other.bounds.Contains(_edge)) return false;
			}
			return true;
		}
		
		public void Scale(float s)
		{
			bounds = new Bounds(center, size*s);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
		}
		
		public void Save(ConfigNode node)
		{
			node.AddValue("bounds_center", ConfigNode.WriteVector(bounds.center));
			node.AddValue("bounds_size", ConfigNode.WriteVector(bounds.size));
			node.AddValue("crew_capacity", CrewCapacity);
			node.AddValue("mass", mass);
			node.AddValue("cost", cost);
		}
		
		public void Load(ConfigNode node)
		{
			if(!node.HasValue("bounds_center") || 
			   !node.HasValue("bounds_size")   ||
			   !node.HasValue("crew_capacity") ||
			   !node.HasValue("mass")||
			   !node.HasValue("cost"))
				throw new KeyNotFoundException("Metric.Load: not all needed values are present in the config node.");
			Vector3 _center = ConfigNode.ParseVector3(node.GetValue("bounds_center"));
			Vector3 _size   = ConfigNode.ParseVector3(node.GetValue("bounds_size"));
			bounds = new Bounds(_center, _size);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
			CrewCapacity = int.Parse(node.GetValue("crew_capacity"));
			mass = float.Parse(node.GetValue("mass"));
			cost = float.Parse(node.GetValue("cost"));
		}
		
		
		//operators
		public static Metric operator*(Metric m, float scale)
		{
			Metric _new = new Metric(m);
			_new.Scale(scale);
			return _new;
		}
		
		public static Metric operator/(Metric m, float scale)
		{ return m*(1.0f/scale); }
		
		//static methods
		public static float Volume(Part part) { return (new Metric(part)).volume; }
		public static float Volume(Vessel vessel) { return (new Metric(vessel)).volume; }
		
		#region Graphics
		public void DrawBox(Transform vT) { Utils.DrawBounds(bounds, vT, Color.white); }

		public void DrawCenter(Transform vT) { Utils.DrawPoint(center, vT); }
		#endregion
	}
}

