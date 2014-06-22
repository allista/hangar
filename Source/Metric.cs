using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AtHangar
{
	public class Metric
	{
		public float volume { get; private set; }
		public Bounds bounds { get; private set; }
		public Vector3 size { get { return bounds.size; } }
		public int CrewCapacity { get; private set; }
		
		
		private static Vector3[] bound_edges(Bounds b)
		{
			Vector3[] edges = new Vector3[8];
			Vector3 min = b.min;
			Vector3 max = b.max;
			edges[0] = new Vector3(min.x, min.y, min.z);
		    edges[1] = new Vector3(min.x, min.y, max.z);
		    edges[2] = new Vector3(min.x, max.y, min.z);
		    edges[3] = new Vector3(min.x, max.y, max.z);
		    edges[4] = new Vector3(max.x, min.y, min.z);
		    edges[5] = new Vector3(max.x, min.y, max.z);
		    edges[6] = new Vector3(max.x, max.y, min.z);
		    edges[7] = new Vector3(max.x, max.y, max.z);
			return edges;
		}
		
		private static void local2local(Transform _from, Transform _to, Vector3[] points)
		{
			for(int p=0; p < points.Length; p++)
				points[p] = _to.InverseTransformPoint(_from.TransformPoint(points[p]));
		}
		
		private static float boundsVolume(Bounds b)
		{ return b.size.x*b.size.y*b.size.z; }
		
		private Bounds partsBounds(List<Part> parts, Transform vT)
		{
			Bounds b = new Bounds();
			foreach(Part p in parts)
			{
				foreach(MeshFilter m in p.FindModelComponents<MeshFilter>())
				{
					if(m.renderer == null) continue;
					Transform mT = m.transform;
					Vector3[] edges = bound_edges(m.sharedMesh.bounds);
					local2local(mT, vT, edges);
					foreach(Vector3 edge in edges)
						b.Encapsulate(edge);
				}
				CrewCapacity += p.CrewCapacity;
			}
			return b;
		}
		
		
		//empty metric
		public Metric()
		{
			bounds = new Bounds();
			volume = 0f;
			CrewCapacity = 0;
		}
		
		//metric copy
		public Metric(Metric m)
		{
			bounds = new Bounds(m.bounds.center, m.bounds.size);
			volume = m.volume;
			CrewCapacity = m.CrewCapacity;
		}
		
		//metric from config node
		public Metric(ConfigNode node) { Load(node); }
		
		//mesh metric
		public Metric(Part part, string mesh_name)
		{
			MeshFilter m = part.FindModelComponent<MeshFilter>(mesh_name);
			if(m == null) { Debug.LogWarning(string.Format("{0} does not have '{1}' mesh", part.name, m.name)); return; }
			Bounds b = new Bounds();
			Transform pT = part.partTransform;
			Transform mT = m.transform;
			Vector3[] edges = bound_edges(m.sharedMesh.bounds);
			local2local(mT, pT, edges);
			foreach(Vector3 edge in edges)
				b.Encapsulate(edge);
			bounds = b;
			volume = boundsVolume(bounds);
		}
		
		//part metric
		public Metric(Part part)
		{
			Transform pT = part.partTransform;
			bounds = partsBounds(new List<Part>(){part}, pT);
			volume = boundsVolume(bounds);
			CrewCapacity = part.CrewCapacity;
		}
		
		//vessel metric
		public Metric(Vessel vessel)
		{
			bounds = partsBounds(vessel.parts, vessel.vesselTransform);
			volume = boundsVolume(bounds);
		}
		
		//in-editor vessel metric
		public Metric(List<Part> vessel)
		{
			Transform vT = vessel[0].partTransform;
			bounds = partsBounds(vessel, vT);
			volume = boundsVolume(bounds);
		}
		
		//public methods
		public bool Empy() { return volume == 0; }
		
		public bool Fits(Metric other)
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
		
		public void Scale(float s)
		{
			Bounds b = bounds;
			b.SetMinMax(b.center-b.extents*s, b.center+b.extents*s);
			bounds = b;
			volume = boundsVolume(bounds);
		}
		
		public void Save(ConfigNode node)
		{
			node.AddValue("bounds_center", bounds.center);
			node.AddValue("bounds_size", bounds.size);
			node.AddValue("crew_capacity", CrewCapacity);
		}
		
		public void Load(ConfigNode node)
		{
			if(!node.HasValue("bounds_center") || 
			   !node.HasValue("bounds_size") ||
			   !node.HasValue("crew_capacity"))
				throw new KeyNotFoundException("Metric.Load: no 'bounds_center' or 'bound_size' values in the config node.");
			Vector3 center = ConfigNode.ParseVector3(node.GetValue("bounds_center"));
			Vector3 size   = ConfigNode.ParseVector3(node.GetValue("bounds_size"));
			bounds = new Bounds(center, size);
			volume = boundsVolume(bounds);
			CrewCapacity = int.Parse(node.GetValue("cew_capacity"));
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
	}
}

