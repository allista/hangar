using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public struct Metric
	{
		//convex hull
		public ConvexHull3D hull { get; private set; }
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
			var edges = new Vector3[8];
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
			var b = new Bounds(center, size);
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
			var b = new Bounds(edges[0], new Vector3());
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

		static Vector3[] uniqueVertices(Mesh m)
		{
			var v_set = new HashSet<Vector3>(m.vertices);
			var new_verts = new Vector3[v_set.Count];
			v_set.CopyTo(new_verts);
			return new_verts;
		}

		Bounds partsBounds(List<Part> parts, Transform vT, bool compute_hull=false)
		{
			//reset metric
			mass = 0;
			cost = 0;
			CrewCapacity = 0;
			Bounds b = default(Bounds);
			if(parts == null || parts.Count == 0)
			{ Utils.Log("Metric.partsBounds: WARNING! No parts were provided."); return b; }
			//calculate bounds and convex hull
			float b_size = 0;
			List<Vector3> hull_points = compute_hull ? new List<Vector3>() : null;
			foreach(Part p in parts)
			{
				if(p == null) continue; //EditorLogic.SortedShipList returns List<Part>{null} when all parts are deleted
				foreach(MeshFilter m in p.FindModelComponents<MeshFilter>())
				{
					//skip meshes without renderer
					if(m.renderer == null || !m.renderer.enabled) continue;
					//skip meshes from the blacklist
					bool skip_mesh = false;
					foreach(string mesh_name in HangarConfig.Globals.MeshesToSkipList)
					{
						if(mesh_name == "") continue;
						skip_mesh = m.name.IndexOf(mesh_name, StringComparison.OrdinalIgnoreCase) >= 0;
						if(skip_mesh) break;
					} if(skip_mesh) continue;
					//wheels are round and rotating >_<
					//TODO: rework this block for more efficiency: do not call Mesh.vertices twice
					Vector3[] edges = m.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0 ? 
									  m.sharedMesh.vertices : BoundsEdges(m.sharedMesh.bounds);
					updateBounds(ref b, local2local(m.transform, vT, edges));
					if(compute_hull)
					{
						float m_size = Vector3.Scale(m.sharedMesh.bounds.size, m.transform.lossyScale).sqrMagnitude;
						var verts = m_size > b_size/10? local2local(m.transform, vT, uniqueVertices(m.sharedMesh)) : edges;
						hull_points.AddRange(verts);
						b_size = b.size.sqrMagnitude;
					}
				}
				CrewCapacity += p.CrewCapacity;
				if(p.IsPhysicallySignificant())	mass += p.TotalMass();
				cost += p.TotalCost();
			}
			if(compute_hull && hull_points.Count >= 4) 
				hull = new ConvexHull3D(hull_points); 
			return b;
		}

		#region Constructors
		//metric copy
		public Metric(Metric m) : this()
		{
			hull   = m.hull;
			bounds = new Bounds(m.bounds.center, m.bounds.size);
			volume = m.volume;
			area   = m.area;
			mass   = m.mass;
			CrewCapacity = m.CrewCapacity;
		}
		
		//metric from bounds
		public Metric(Bounds b, float m = 0f, int crew_capacity = 0) : this()
		{
			bounds = new Bounds(b.center, b.size);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
			mass   = m;
			CrewCapacity = crew_capacity;
		}
		
		//metric from size
		public Metric(Vector3 center, Vector3 size, float m = 0f, int crew_capacity = 0) : this()
		{
			bounds = new Bounds(center, size);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
			mass   = m;
			CrewCapacity = crew_capacity;
		}
		public Metric(Vector3 size, float m = 0f, int crew_capacity = 0) 
			: this(Vector3.zero, size, m, crew_capacity) {}

		//metric from volume
		public Metric(float V, float m = 0f, int crew_capacity = 0) : this()
		{
			var a  = Mathf.Pow(V, 1/3f);
			bounds = new Bounds(Vector3.zero, new Vector3(a,a,a));
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
			mass   = m;
			CrewCapacity = crew_capacity;
		}
		
		//metric form edges
		public Metric(Vector3[] edges, float m = 0f, int crew_capacity = 0, bool compute_hull=false) : this()
		{
			if(compute_hull) hull = new ConvexHull3D(edges);
			bounds = initBounds(edges);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
			mass   = m;
			CrewCapacity = crew_capacity;
		}
		
		//metric from config node
		public Metric(ConfigNode node) : this() { Load(node); }
		
		//mesh metric
		public Metric(Part part, string mesh_name, bool compute_hull=false) : this()
		{
			if(string.IsNullOrEmpty(mesh_name)) return;
			MeshFilter m = part.FindModelComponent<MeshFilter>(mesh_name);
			if(m == null) { Utils.Log("[Metric] {0} does not have '{1}' mesh", part.name, mesh_name); return; }
			if(compute_hull) hull = new ConvexHull3D(uniqueVertices(m.sharedMesh));
			Vector3[] edges = BoundsEdges(m.sharedMesh.bounds);
			local2local(m.transform, part.partTransform, edges);
			bounds = initBounds(edges);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
			mass   = 0f;
		}
		
		//part metric
		public Metric(Part part, bool compute_hull=false) : this()
		{
			bounds = partsBounds(new List<Part>{part}, part.partTransform, compute_hull);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
		}
		
		//vessel metric
		public Metric(Vessel vessel, bool compute_hull=false) : this()
		{
			bounds = partsBounds(vessel.parts, vessel.vesselTransform, compute_hull);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
		}
		
		//in-editor vessel metric
		public Metric(List<Part> vessel, bool compute_hull=false) : this()
		{
			bounds = partsBounds(vessel, vessel[0].partTransform, compute_hull);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
		}
		#endregion
		
		//public methods
		public void Clear() 
		{ 
			bounds = default(Bounds);
			volume = area = mass = cost = 0;
			CrewCapacity = 0;
			hull   = null;
		}

		public Bounds GetBounds() { return new Bounds(bounds.center, bounds.size); }

		public void Scale(float s)
		{
			bounds = new Bounds(center, size*s);
			volume = boundsVolume(bounds);
			area   = boundsArea(bounds);
			if(hull != null) hull = hull.Scale(s);
		}
		
		public void Save(ConfigNode node)
		{
			node.AddValue("bounds_center", ConfigNode.WriteVector(bounds.center));
			node.AddValue("bounds_size", ConfigNode.WriteVector(bounds.size));
			node.AddValue("crew_capacity", CrewCapacity);
			node.AddValue("mass", mass);
			node.AddValue("cost", cost);
			if(hull != null) hull.Save(node.AddNode("HULL"));
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
			if(node.HasNode("HULL")) hull = ConvexHull3D.Load(node.GetNode("HULL"));
		}

		#region Fitting
		bool fits_somehow(List<float> _D)
		{
			var  D = new List<float>{size.x, size.y, size.z};
			D.Sort(); _D.Sort();
			foreach(float d in D)
			{
				if(_D.Count == 0) break;
				int ud = -1;
				for(int i = 0; i < _D.Count; i++)
				{ 
					if(d <= _D[i]) 
					{ ud = i; break; } 
				}
				if(ud < 0) return false;
				_D.RemoveAt(ud);
			}
			return true;
		}

		public bool FitsSomehow(Metric other)
		{
			var _D = new List<float>{other.size.x, other.size.y, other.size.z};
			return fits_somehow(_D);
		}

		public bool FitsSomehow(Vector2 node)
		{
			var _D = new List<float>{node.x, node.y};
			return fits_somehow(_D);
		}

		/// <summary>
		/// Returns true if THIS metric fits inside the OTHER metric.
		/// </summary>
		/// <param name="this_T">Transform of this metric.</param>
		/// <param name="other_T">Transform of the other metric.</param>
		/// <param name="other">Metric acting as a container.</param>
		public bool FitsAligned(Transform this_T, Transform other_T, Metric other)
		{
			var edges = hull != null? hull.Points.ToArray() : BoundsEdges(bounds);
			for(int i = 0; i < edges.Length; i++) 
			{
				Vector3 edge = other_T.InverseTransformPoint(this_T.position+this_T.TransformDirection(edges[i]-center));
				if(other.hull != null) 
				{ if(!other.hull.Contains(edge)) return false; }
				else if(!other.bounds.Contains(edge)) return false;
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
			var edges = hull != null? hull.Points.ToArray() : BoundsEdges(bounds);
			//check each triangle of container
			var c_edges   = container.vertices;
			var triangles = container.triangles;
			if(triangles.Length/3 > edges.Length)
			{
				for(int i = 0; i < edges.Length; i++) 
					edges[i] = other_T.InverseTransformPoint(this_T.position+this_T.TransformDirection(edges[i]-center));
				for(int i = 0; i < triangles.Length/3; i++)
				{
					var V1 = c_edges[triangles[i*3]];
					var V2 = c_edges[triangles[i*3+1]];
					var V3 = c_edges[triangles[i*3+2]];
					var P  = new Plane(V1, V2, V3);
					foreach(Vector3 edge in edges)
					{ if(!P.GetSide(edge)) return false; }
				}
			}
			else
			{
				var planes = new Plane[triangles.Length/3];
				for(int i = 0; i < triangles.Length/3; i++)
				{
					var V1 = c_edges[triangles[i*3]];
					var V2 = c_edges[triangles[i*3+1]];
					var V3 = c_edges[triangles[i*3+2]];
					planes[i] = new Plane(V1, V2, V3);
				}
				for(int i = 0; i < edges.Length; i++) 
				{
					var edge = other_T.InverseTransformPoint(this_T.position+this_T.TransformDirection(edges[i]-center));
					foreach(Plane P in planes)
					{ if(!P.GetSide(edge)) return false; }
				}
			}
			return true;
		}
		#endregion
		
		#region Operators
		public static Metric operator*(Metric m, float scale)
		{
			var _new = new Metric(m);
			_new.Scale(scale);
			return _new;
		}
		
		public static Metric operator/(Metric m, float scale)
		{ return m*(1.0f/scale); }
		
		//static methods
		public static float Volume(Part part) { return (new Metric(part)).volume; }
		public static float Volume(Vessel vessel) { return (new Metric(vessel)).volume; }
		#endregion

		#if DEBUG
		public void DrawBox(Transform vT) { HangarGUI.DrawBounds(bounds, vT, Color.white); }

		public void DrawCenter(Transform vT) { HangarGUI.DrawPoint(center, vT); }
		#endif
	}
}

