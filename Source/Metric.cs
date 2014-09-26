using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class Metric
	{
		//bounds
		public Bounds  bounds  { get; private set; }
		public Vector3 center  { get { return bounds.center; } }
		public Vector3 extents { get { return bounds.extents; } }
		public Vector3 size    { get { return bounds.size; } }
		//physical properties
		public float volume { get; private set; }
		public float area   { get; private set; }
		public float mass   { get; set; }
		//part-vessel properties
		public int CrewCapacity { get; private set; }
		public float cost { get; set; }

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
		
		static Vector3[] local2local(Transform _from, Transform _to, Vector3[] points)
		{
			for(int p=0; p < points.Length; p++)
				points[p] = _to.InverseTransformPoint(_from.TransformPoint(points[p]));
			return points;
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

		static void updateBounds(ref Bounds b, Vector3[] edges)
		{
			if(b == default(Bounds)) b = initBounds(edges);
			else foreach(Vector3 edge in edges) b.Encapsulate(edge);
		}

		static void updateBounds(ref Bounds b, Bounds nb)
		{
			if(b == default(Bounds)) b = nb;
			else b.Encapsulate(nb);
		}

		Bounds partsBounds(List<Part> parts, Transform vT)
		{
			mass = 0;
			cost = 0;
			CrewCapacity = 0;
			Bounds b = default(Bounds);
			if(parts == null) return b;
			foreach(Part p in parts)
			{
				if(p == null) continue; //EditorLogic.SortedShipList returns List<Part>{null} when all parts are deleted
				foreach(MeshFilter m in p.FindModelComponents<MeshFilter>())
				{
					if(m.renderer == null || !m.renderer.enabled) continue;
					//wheels are round and rotating >_<
					Vector3[] edges = m.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0 ? 
						m.sharedMesh.vertices : BoundsEdges(m.sharedMesh.bounds);
					updateBounds(ref b, local2local(m.transform, vT, edges));
				}
				CrewCapacity += p.CrewCapacity;
				if(p.IsPhysicallySignificant())	mass += p.TotalMass();
				cost += p.TotalCost();
			}
			return b;
		}

		#region Constructors
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
			Vector3[] edges = BoundsEdges(m.sharedMesh.bounds);
			local2local(m.transform, part.partTransform, edges);
			bounds = initBounds(edges);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
			mass   = 0f;
		}
		
		//part metric
		public Metric(Part part)
		{
			bounds = partsBounds(new List<Part>{part}, part.partTransform);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
		}
		
		//vessel metric
		public Metric(Vessel vessel)
		{
			bounds = partsBounds(vessel.parts, vessel.vesselTransform);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
		}
		
		//in-editor vessel metric
		public Metric(List<Part> vessel)
		{
			bounds = partsBounds(vessel, vessel[0].partTransform);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
		}
		#endregion
		
		//public methods
		public Bounds GetBounds() { return new Bounds(bounds.center, bounds.size); }

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

		#region Fitting
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

		/// <summary>
		/// Returns true if THIS metric fits inside the OTHER metric.
		/// </summary>
		/// <param name="this_T">Transform of this metric.</param>
		/// <param name="other_T">Transform of the other metric.</param>
		/// <param name="other">Metric acting as a container.</param>
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

		/// <summary>
		/// Returns true if THIS metric fits inside the given CONTAINER mesh.
		/// </summary>
		/// <param name="this_T">Transform of this metric.</param>
		/// <param name="other_T">Transform of the given mesh.</param>
		/// <param name="container">Mesh acting as a container.</param>
		/// Implemeted using algorithm described at
		/// http://answers.unity3d.com/questions/611947/am-i-inside-a-volume-without-colliders.html
		public bool FitsAligned(Transform this_T, Transform other_T, Mesh container)
		{
			//get edges in containers reference frame
			Vector3[] edges = BoundsEdges(Vector3.zero, bounds.size);
			for(int i = 0; i < edges.Length; i++) 
				edges[i] = other_T.InverseTransformPoint(this_T.position+this_T.TransformDirection(edges[i]));
			//check each triangle of container
			Vector3[] c_edges = container.vertices;
			int[] triangles   = container.triangles;
			for(int i = 0; i < triangles.Length/3; i++)
			{
				var V1 = c_edges[triangles[i*3]];
				var V2 = c_edges[triangles[i*3+1]];
				var V3 = c_edges[triangles[i*3+2]];
				var P  = new Plane(V1, V2, V3);
				#if !DEBUG
				foreach(Vector3 edge in edges)
				{ if(!P.GetSide(edge)) return false; }
				#else
				Utils.Log("Plane:\n{0}\n{1}\n{2}", V1,V2,V3);
				foreach(Vector3 edge in edges)
				{
					if(!P.GetSide(edge))
					{
						Utils.Log("Edge is outside: {0}", edge);
						return false;
					}
					Utils.Log("Edge is inside: {0}", edge);
				}
				#endif
			}
			return true;
		}
		#endregion
		
		#region Operators
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
		#endregion
		
		#region Graphics
		public void DrawBox(Transform vT) { Utils.DrawBounds(bounds, vT, Color.white); }

		public void DrawCenter(Transform vT) { Utils.DrawPoint(center, vT); }
		#endregion
	}
}

