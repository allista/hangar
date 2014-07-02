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
		public Vector3 world_center { get; private set; }
		public int CrewCapacity { get; private set; }
		
		
		private static Vector3[] bound_edges(Bounds b)
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
		
		private static void local2local(Transform _from, Transform _to, Vector3[] points)
		{
			for(int p=0; p < points.Length; p++)
				points[p] = _to.InverseTransformPoint(_from.TransformPoint(points[p]));
		}
		
		private static float boundsVolume(Bounds b)
		{ return b.size.x*b.size.y*b.size.z; }
		
		private static Bounds initBounds(Vector3[] edges)
		{
			Bounds b = new Bounds(edges[0], new Vector3());
			for(int i = 1; i < edges.Length; i++)
				b.Encapsulate(edges[i]);
			return b;
		}
		
		private Bounds partsBounds(List<Part> parts, Transform vT)
		{
			CrewCapacity = 0;
			Bounds b; bool inited = false;
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
					else edges = bound_edges(m.sharedMesh.bounds);
					local2local(mT, vT, edges);
					if(!inited) { b = initBounds(edges); inited = true; }
					else foreach(Vector3 edge in edges) b.Encapsulate(edge);
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
			world_center = new Vector3(0,0,0);
		}
		
		//metric copy
		public Metric(Metric m)
		{
			bounds = new Bounds(m.bounds.center, m.bounds.size);
			volume = m.volume;
			CrewCapacity = m.CrewCapacity;
			world_center = m.world_center;
		}
		
		//metric from config node
		public Metric(ConfigNode node) { Load(node); }
		
		//mesh metric
		public Metric(Part part, string mesh_name)
		{
			MeshFilter m = part.FindModelComponent<MeshFilter>(mesh_name);
			if(m == null) { Debug.LogWarning(string.Format("[Metric] {0} does not have '{1}' mesh", part.name, mesh_name)); return; }
			Transform pT = part.partTransform;
			Transform mT = m.transform;
			Vector3[] edges = bound_edges(m.sharedMesh.bounds);
			local2local(mT, pT, edges);
			bounds = initBounds(edges);
			volume = boundsVolume(bounds);
			world_center = pT.TransformPoint(bounds.center);
		}
		
		//part metric
		public Metric(Part part)
		{
			Transform pT = part.partTransform;
			bounds = partsBounds(new List<Part>(){part}, pT);
			volume = boundsVolume(bounds);
			world_center = pT.TransformPoint(bounds.center);
		}
		
		//vessel metric
		private static Transform get_controller_transform(List<Part> parts)
		{
			Transform t = null;
			foreach(Part p in parts)
			{
				if(p.isControlSource)
				{
					t = p.partTransform;
					break;
				}
			}
			return t;
		}
		
		public Metric(Vessel vessel)
		{
//			Transform vT = get_controller_transform(vessel.parts);
//			if(vT == null) 
			Transform vT = vessel.vesselTransform;
			bounds = partsBounds(vessel.parts, vT);
			volume = boundsVolume(bounds);
			world_center = vT.TransformPoint(bounds.center);
		}
		
		//in-editor vessel metric
		public Metric(List<Part> vessel)
		{
//			Transform vT = get_controller_transform(vessel);
//			if(vT == null) 
			Transform vT = vessel[0].partTransform;
			bounds = partsBounds(vessel, vT);
			volume = boundsVolume(bounds);
			world_center = vT.TransformPoint(bounds.center);
		}
		
		//public methods
		public Bounds GetBounds() { return new Bounds(bounds.center, bounds.size); }
		
		public bool Empy() { return volume == 0; }
		
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
			Bounds other_b = other.GetBounds();
			return (other_b.Contains(other_T.InverseTransformPoint(this_T.TransformPoint(Vector3.up*bounds.extents.y-bounds.extents))) &&
				    other_b.Contains(other_T.InverseTransformPoint(this_T.TransformPoint(bounds.extents+Vector3.up*bounds.extents.y))));
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
			node.AddValue("bounds_center", ConfigNode.WriteVector(bounds.center));
			node.AddValue("bounds_size", ConfigNode.WriteVector(bounds.size));
			node.AddValue("world_center", ConfigNode.WriteVector(world_center));
			node.AddValue("crew_capacity", CrewCapacity);
		}
		
		public void Load(ConfigNode node)
		{
			if(!node.HasValue("bounds_center") || 
			   !node.HasValue("bounds_size") ||
			   !node.HasValue("world_center") ||
			   !node.HasValue("crew_capacity"))
				throw new KeyNotFoundException("Metric.Load: no 'bounds_center' or 'bound_size' values in the config node.");
			Vector3 center = ConfigNode.ParseVector3(node.GetValue("bounds_center"));
			Vector3 size   = ConfigNode.ParseVector3(node.GetValue("bounds_size"));
			world_center   = ConfigNode.ParseVector3(node.GetValue("world_center"));
			bounds = new Bounds(center, size);
			volume = boundsVolume(bounds);
			CrewCapacity = int.Parse(node.GetValue("crew_capacity"));
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
		
		
		#region Debug
		static Material _material;
        static Material material
        {
            get
            {
                if (_material == null)
					_material = new Material(Shader.Find("Particles/Additive"));
                return _material;
            }
        }
//		edges[0] = new Vector3(min.x, min.y, min.z); //left-bottom-back
//	    edges[1] = new Vector3(min.x, min.y, max.z); //left-bottom-front
//	    edges[2] = new Vector3(min.x, max.y, min.z); //left-top-back
//	    edges[3] = new Vector3(min.x, max.y, max.z); //left-top-front
//	    edges[4] = new Vector3(max.x, min.y, min.z); //right-bottom-back
//	    edges[5] = new Vector3(max.x, min.y, max.z); //right-bottom-front
//	    edges[6] = new Vector3(max.x, max.y, min.z); //right-top-back
//	    edges[7] = new Vector3(max.x, max.y, max.z); //right-top-front
		private static void draw_box(Bounds b, Transform t)
		{
			Vector3[] edges = bound_edges(b);
			int[] tri = new int[18];
			int i = 0;
			tri[i  ] = 0; tri[++i] = 1; tri[++i] = 2;
			tri[++i] = 3; tri[++i] = 1; tri[++i] = 2;
			
			tri[++i] = 0; tri[++i] = 2; tri[++i] = 6;
			tri[++i] = 0; tri[++i] = 4; tri[++i] = 6;
			
			tri[++i] = 0; tri[++i] = 1; tri[++i] = 4;
			tri[++i] = 5; tri[++i] = 1; tri[++i] = 4;
			
			Mesh m = new Mesh();
			m.vertices = edges;
			m.triangles = tri;
			
			m.RecalculateBounds();
    		m.RecalculateNormals();
			
			Graphics.DrawMesh(m, t.localToWorldMatrix, material, 0);
		}
		public void DrawBox(Transform vT) 
		{ 
//			Transform vT = get_controller_transform(vessel);
//			if(vT == null) vT = vessel[0].partTransform;
			draw_box(bounds, vT); 
		}
		#endregion
	}
}

