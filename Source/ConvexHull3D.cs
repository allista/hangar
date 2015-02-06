using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//NOTE:
//The for loop is faster than List.ForEach and especially foreach with compiler optimizations.
//See: http://diditwith.net/2006/10/05/PerformanceOfForeachVsListForEach.aspx
//And profiling confirms it.
namespace AtHangar
{
	public class Face : IEnumerable<Vector3>
	{
		public class Edge
		{
			public Vector3 v0 { get { return Host[Index]; } }
			public Vector3 v1 { get { return Host[Index+1]; } }
			public readonly Face Host;
			public readonly int  Index;
			public Face Neighbour;
			public int  NeighbourIndex = -1;
			public Edge NeigbourEdge { get { return Neighbour.GetEdge(NeighbourIndex); } }
			public bool IsBorder { get { return Neighbour == null; } }

			public Edge(Face host, int index)
			{ 
				Host  = host; 
				Index = index;
			}

			#if DEBUG
			public void Log()
			{
				Utils.Log("Edge{0}: {1}, {2}\n" +
					"NeighbourEdge{3}: {4}, {5}", 
					Index, v0, v1, NeighbourIndex, NeigbourEdge.v0, NeigbourEdge.v1);
			}
			#endif
		}

		public const float MinDistance = 1e-6f;

		//plane properties
		readonly Vector3[] vertices = new Vector3[3];
		public Vector3 v0 { get { return vertices[0]; } private set { vertices[0] = value; } }
		public Vector3 v1 { get { return vertices[1]; } private set { vertices[1] = value; } }
		public Vector3 v2 { get { return vertices[2]; } private set { vertices[2] = value; } }
		public Plane   P  { get; private set; }
		//set of points visible from the plane
		public List<Vector3> VisiblePoints = new List<Vector3>();
		public Vector3 Furthest;
		public float   FurthestDistance = -1;
		//set of neighbouring faces
		readonly Edge[] edges = new Edge[3];
		public bool Visited = false;
		public bool Dropped = false;

		#region Constructors
		public Face(Vector3 v0, Vector3 v1, Vector3 v2)
		{
			this.v0 = v0; this.v1 = v1; this.v2 = v2;
			P = new Plane(v0, v1, v2);
			edges[0] = new Edge(this, 0);
			edges[1] = new Edge(this, 1);
			edges[2] = new Edge(this, 2);
		}

		public Face(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 p)
			: this(v0,v1,v2) { Orient(p); }
		#endregion

		#region Access to members
		public Vector3 this[int i]
		{
			get { return vertices[i % 3]; }
			set { vertices[i % 3] = value; }
		}

		//enumerators
		public IEnumerator<Vector3> GetEnumerator()
		{ 
			yield return vertices[0];
			yield return vertices[1];
			yield return vertices[2];
		}
		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

		//edges
		public Edge GetEdge(int i) { return edges[i % 3]; }
		public IEnumerator<Edge> Edges(int start = 0) 
		{ 
			for(int i = start; i < start+3; i++)
				yield return edges[i % 3];
		}

		public void Join(int edge, Edge other)
		{ 
			var e = edges[edge % 3];
			e.Neighbour = other.Host;
			other.Neighbour = this;
			e.NeighbourIndex = other.Index;
			other.NeighbourIndex = e.Index;
		}
		public void Join(int edge, Face other, int other_edge)
		{ Join(edge, other.GetEdge(other_edge)); }
		#endregion

		#region Geometry
		public bool Visible(Vector3 p) { return P.GetDistanceToPoint(p) > MinDistance; }

		public void Flip()
		{
			Plane p = P;
			p.normal = -p.normal; 
			p.distance = -p.distance;
			P = p;
			var t = v0; v0 = v1; v1 = t;
		}

		public void Orient(Vector3 p) { if(P.GetDistanceToPoint(p) > MinDistance) Flip(); }
		#endregion

		#if DEBUG
		public void Log()
		{
			Utils.Log("Face:\n" +
				"{0}\n" +
				"{1}\n" +
				"{2}\n" +
				"n: {3}\n" +
				"d: {4}\n" +
				"Visible Points: {5}\n" +
				"Furthest one:   {6}\n" +
				"Distance to it: {7}", 
				v0, v1, v2, P.normal, P.distance,
				VisiblePoints.Count, Furthest, FurthestDistance,
				edges.Length);
			edges.ForEach(n => n.Log());
		}
		#endif
	}

	class LineSegment : IComparable<LineSegment>
	{
		public Vector3 s { get; private set; }
		public Vector3 e { get; private set; }
		public Vector3 n { get; private set; }
		public float   l { get; private set; }

		public LineSegment(Vector3 start, Vector3 end)
		{ 
			s = start; e = end; 
			var d = end-start;
			n = d.normalized;
			l = d.magnitude;
		}

		public float DistanceTo(Vector3 p)
		{ 
			Vector3 sp = s-p;
			return (sp - n * Vector3.Dot(sp, n)).magnitude;
		}

		public int CompareTo(LineSegment other)
		{ return l.CompareTo(other.l); }

		#if DEBUG
		public void Log()
		{
			Utils.Log("Line:\n" +
				"s: {0}\n" +
				"e: {1}\n" +
				"n: {2}\n" +
				"l: {3}", s, e, n, l);
		}
		#endif
	}

	public class ConvexHull3D : IEnumerable<Vector3>
	{
		class VisibleFaces : List<Face>
		{ 
			public List<Face.Edge> Horizon = new List<Face.Edge>(); 
			public new void Clear()	{ base.Clear(); Horizon.Clear(); }
		}

		public List<Vector3> Points { get; private set; }
		public List<Face>    Faces  { get; private set; }

		public virtual IEnumerator<Vector3> GetEnumerator()
		{ return Points.GetEnumerator(); }

		IEnumerator IEnumerable.GetEnumerator()
		{ return GetEnumerator(); }

		/// <summary>
		/// Form a pyramid of Faces with the apex p
		/// and the base defined as the CCW list of edges.
		/// </summary>
		/// <param name="p">Apex of the pyramid.</param>
		/// <param name="edges">CCW list of edges belonging to the Faces 
		/// to which the pyramid will be connected.</param>
		static List<Face> make_pyramid(Vector3 p, IList<Face.Edge> edges)
		{
			int num_edges = edges.Count;
			var faces = new List<Face>(num_edges);
			for(int i = 0; i < num_edges; i++)
			{
				Face.Edge e = edges[i];
				var nf = new Face(p, e.v1, e.v0);
				nf.Join(1, e); //join with the horizon
				if(i > 0) nf.Join(0, faces[i-1], 2); //join with previous
				if(i == num_edges-1) nf.Join(2, faces[0], 0); //join with the firts
				faces.Add(nf);
			}
			return faces;
		}

		void make_seed(List<Vector3> points)
		{
			//initialize Faces
			Faces = new List<Face>(4);
			//find min-max points of a set
			Vector3 min_xv, max_xv, min_yv, max_yv, min_zv, max_zv;
			float   min_x,  max_x,  min_y,  max_y,  min_z,  max_z;
			min_xv = max_xv = min_yv = max_yv = min_zv = max_zv = Vector3.zero;
			min_x = max_x = min_y = max_y = min_z = max_z = -1;
			foreach(Vector3 p in points)
			{
				if(p.x < min_x || min_x < 0) { min_x = p.x; min_xv = p; }
				else if(p.x > max_x) { max_x = p.x; max_xv = p; }

				if(p.y < min_y || min_y < 0) { min_y = p.y; min_yv = p; }
				else if(p.y > max_y) { max_y = p.y; max_yv = p; }

				if(p.z < min_z || min_z < 0) { min_z = p.z; min_zv = p; }
				else if(p.z > max_z) { max_z = p.z; max_zv = p; }
			}
			//find the longest line segment between the extremums of each dimension
			var xl = new LineSegment(min_xv, max_xv);
			var yl = new LineSegment(min_yv, max_yv);
			var zl = new LineSegment(min_zv, max_zv);
			LineSegment ml = new []{xl, yl, zl}.Max();
			//find the furthest point from the longest line
			var EP = new []{ min_xv, max_xv, min_yv, max_yv, min_zv, max_zv };
			Vector3 mv1 = EP.SelectMax(ml.DistanceTo);
			//make a face and find the furthest point from it
			var f0 = new Face(ml.s, ml.e, mv1);
			Vector3 mv2 = EP.SelectMax(p => Math.Abs(f0.P.GetDistanceToPoint(p)));
			//make other 3 faces
			f0.Orient(mv2); //f0 is not visible now, 
			//so its edges should be taken in the oposite direction
			var edges = new List<Face.Edge>(3);
			edges.Add(f0.GetEdge(2));
			edges.Add(f0.GetEdge(1));
			edges.Add(f0.GetEdge(0));
			var faces = make_pyramid(mv2, edges);
			Faces.Add(f0); Faces.AddRange(faces);
			points.RemoveAll(v => v.Equals(mv1) || v.Equals(mv2) || v.Equals(ml.s) || v.Equals(ml.e));
		}

		static void sort_points(IList<Vector3> points, IList<Face> faces)
		{
			var num_faces = faces.Count;
			for(int p = 0; p < points.Count; p++)
			{ 
				for(int f = 0; f < num_faces; f++)
				{ 
					var face  = faces[f];
					var point = points[p];
					var d = face.P.GetDistanceToPoint(point);
					if(d > Face.MinDistance)
					{ 
						face.VisiblePoints.Add(point);
						if(d > face.FurthestDistance)
						{ face.FurthestDistance = d; face.Furthest = point; }
						break; 
					}
				}
			}
		}

		static void build_horizon(Vector3 p, VisibleFaces visible, Face start_face, int start_edge=0)
		{
			start_face.Visited = true;
			start_face.Dropped = true;
			visible.Add(start_face);
			var edges = start_face.Edges(start_edge);
			while(edges.MoveNext())
			{
				var e = edges.Current;
				if(e.Neighbour.Visited || e.Neighbour.Dropped) continue;
				if(e.Neighbour.Visible(p)) 
					build_horizon(p, visible, e.Neighbour, e.NeighbourIndex);
				else visible.Horizon.Add(e.NeigbourEdge);
			}
			start_face.Visited = false;
		}

		/// <summary>
		/// Incrementally update the existing hull using provided points.
		/// </summary>
		/// <param name="points">Points used to update the hull.</param>
		public void Update(IList<Vector3> points)
		{
			var visible     = new VisibleFaces();
			var working_set = new LinkedList<Face>(Faces);
			var final_set   = new LinkedList<Face>();
			sort_points(points, Faces); Faces.Clear();
			while(working_set.Count > 0)
			{
				Face f = working_set.Pop();
				//if the face was dropped, skip it
				if(f.Dropped) continue;
				//if the face has no visible points it belongs to the hull
				if(f.VisiblePoints.Count == 0) 
				{ final_set.AddFirst(f); continue; }
				//if not, build the visible set of faces and the horizon for the furthest visible point 
				visible.Clear();
				build_horizon(f.Furthest, visible, f);
				//create new faces
				var new_faces = make_pyramid(f.Furthest, visible.Horizon);
				//add points from visible faces to the new faces
				for(int i = 0; i < visible.Count; i++) 
					sort_points(visible[i].VisiblePoints, new_faces);
				//add new faces to the working set
				for(int i = 0; i < new_faces.Count; i++)
					working_set.AddFirst(new_faces[i]);
			}
			//filter out faces that are still visible
			Faces.AddRange(from f in final_set where !f.Dropped select f);
			//build a list of unique hull points
			var _Points = new HashSet<Vector3>();
			for(int i = 0; i < Faces.Count; i++)
			{ var f = Faces[i]; _Points.Add(f.v0); _Points.Add(f.v1); _Points.Add(f.v2); }
			Points = new List<Vector3>(_Points.Count);
			Points.AddRange(_Points);
		}

		public ConvexHull3D(List<Vector3> points)
		{
			//initial checks
			if(points.Count < 4) 
				throw new NotSupportedException(string.Format("[Hangar] ConvexHull3D needs at least 4 edges, {0} given", points.Count));
			//initialize the initial tetrahedron
			make_seed(points);
			//incrementally udate the seed
			Update(points);
		}
		public ConvexHull3D(IEnumerable<Vector3> points) : this(points.ToList()) {}

		public ConvexHull3D Scale(float s)
		{
			var scaled = new Vector3[Points.Count];
			for(int i = 0; i < Points.Count; i++) scaled[i] = Points[i]*s;
			return new ConvexHull3D(scaled);
		}

		public bool Contains(Vector3 p)
		{ return Faces.TrueForAll(f => !f.Visible(p)); }

		public bool Contains(IEnumerable<Vector3> points)
		{ 
			bool contains = true;
			foreach(Vector3 p in points) 
			{
				contains &= Contains(p);
				if(!contains) break;
			}
			return contains;
		}

		public void Save(ConfigNode node)
		{
			foreach(Vector3 p in Points)
				node.AddValue("point", ConfigNode.WriteVector(p));
		}

		public static ConvexHull3D Load(ConfigNode node)
		{
			var points = new List<Vector3>();
			foreach(ConfigNode.Value v in node.values)
			{
				Vector3 p;
				try 
				{ 
					p = ConfigNode.ParseVector3(v.value); 
					points.Add(p);
				}
				catch(Exception ex)	{ Debug.LogException(ex); }
			}
			return new ConvexHull3D(points);
		}
	}
}