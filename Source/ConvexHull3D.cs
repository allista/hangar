//this algorithm was adopted from: http://www.nicoptere.net/AS3/convexhull/src/ConvexHull.as
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class Face : IEnumerable<Vector3>
	{
		public Plane   P  { get; private set; }
		public Vector3 v0 { get; private set; }
		public Vector3 v1 { get; private set; }
		public Vector3 v2 { get; private set; }
		public Vector3 c  { get; private set; }

		public virtual IEnumerator<Vector3> GetEnumerator()
		{
			yield return v0;
			yield return v1;
			yield return v2;
		}
		IEnumerator IEnumerable.GetEnumerator()
		{ return GetEnumerator(); }

		public bool Visible(Vector3 p) { return P.GetDistanceToPoint(p) > 1e-6; }

		public float DistanceTo(Vector3 p) { return P.GetDistanceToPoint(p); }

		public void Flip()
		{
			Plane p = P;
			p.normal = -p.normal; 
			p.distance = -p.distance;
			P = p;
			var t = v0; v0 = v1; v1 = t;
		}

		public void Orient(Vector3 p) { if(Visible(p)) Flip(); }

		public Face(Vector3 v0, Vector3 v1, Vector3 v2)
		{
			this.v0 = v0; this.v1 = v1; this.v2 = v2;
			c = ConvexHull3D.Centroid(v0, v1, v2);
			P = new Plane(v0, v1, v2);
		}

		public Face(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 p)
			: this(v0,v1,v2) { Orient(p); }

		#if DEBUG
		public void Log()
		{
			Utils.Log("Face:\n" +
				"{0}\n" +
				"{1}\n" +
				"{2}\n" +
				"n: {3}\n" +
				"d: {4}", v0, v1, v2, P.normal, P.distance);
		}
		#endif
	}

	public class ConvexHull3D : IEnumerable<Vector3>
	{
		public List<Vector3> Points { get; private set; }
		public List<Face>    Faces  { get; private set; }

		public virtual IEnumerator<Vector3> GetEnumerator()
		{ return Points.GetEnumerator(); }

		IEnumerator IEnumerable.GetEnumerator()
		{ return GetEnumerator(); }

		public static Vector3 Centroid(params Vector3[] points)
		{
			Vector3 c = Vector3.zero;
			foreach(Vector3 p in points) c += p;
			c /= (float)points.Length;
			return c;
		}

		public Vector3[] TranslatePoints(Vector3 move)
		{
			Vector3[] points = new Vector3[Points.Count];
			for(int i = 0; i < Points.Count; i++) points[i] = Points[i]+move;
			return points;
		}

//		static float distance_to_line(Vector3 a, Vector3 n, Vector3 p)
//		{ 
//			Vector3 dp = a-p;
//			return (dp - n * Vector3.Dot(dp, n)).magnitude;
//		}
//
//		void init_seed(IEnumerable<Vector3> points)
//		{
//			Vector3 min_xv, max_xv, min_yv, max_yv, min_zv, max_zv;
//			float   min_x,  max_x,  min_y,  max_y,  min_z,  max_z;
//			min_x = max_x = min_y = max_y = min_z = max_z = -1;
//			foreach(Vector3 p in points)
//			{
//				if(p.x < min_x || min_x < 0) { min_x = p.x; min_xv = p; }
//				else if(p.x > max_x) { max_x = p.x; max_xv = p; }
//
//				if(p.y < min_y || min_y < 0) { min_y = p.y; min_yv = p; }
//				else if(p.y > max_y) { max_y = p.y; max_yv = p; }
//
//				if(p.z < min_z || min_z < 0) { min_z = p.z; min_zv = p; }
//				else if(p.z > max_z) { max_z = p.z; max_zv = p; }
//			}
//			Vector3 xl = (max_xv - min_xv);
//			Vector3 yl = (max_yv - min_yv);
//			Vector3 zl = (max_zv - min_zv);
//			float max_l = -1; Vector3 ml;
//			foreach(Vector3 l in new []{xl, yl, zl})
//			{ if(l.magnitude > max_l) { max_l = l.magnitude; ml = l; } }
//			float max_d = -1; Vector3 mln = ml.normalized;
////			foreach(Vector3 p in new []{min_xv, max_xv, min_yv, max_yv, min_zv, max_zv})
////			{ if(distance_to_line())}
////			Vector3[] dims
//		}

		public ConvexHull3D(List<Vector3> points)
		{
			//initial checks
			if(points.Count < 4) 
				throw new NotSupportedException(string.Format("[Hangar] ConvexHull3D needs at least 4 edges, {0} given", points.Count));
			//lists of faces
			Points = new List<Vector3>();
			Faces  = new List<Face>();
			//build the seed tetrahedron
			//:first face uses first three points
			Faces.Add(new Face(points[0], points[1], points[2]));
			//:now scan other points for the furthest from the f0
			int furthest=3; float max_dist = 0f; Face f0 = Faces[0];
			for(int i = 3; i < points.Count; i++)
			{
				float d = Math.Abs(f0.DistanceTo(points[i]));
				if(d > max_dist)
				{
					max_dist = d;
					furthest = i;
				}
			}
			Vector3 fp = points[furthest];
			f0.Orient(fp); //f0 is not visible now, 
			//so its edges should be taken in the oposite direction
			Faces.Add(new Face(fp, f0.v0, f0.v2));
			Faces.Add(new Face(fp, f0.v2, f0.v1));
			Faces.Add(new Face(fp, f0.v1, f0.v0));
//			Vector3 c0 = Centroid(points[0], points[1], points[2], fp); //debug
//			Utils.Log("Tetrahedron:\n{0}\n{1}\n{2}\n{3}\n" + //debug
//				"Centroid: {4}\n" +
//				"Centroid is inside: {5},\n" +
//				"Distance from centroid to faces:\n" +
//				"{6}, {7}, {8}, {9}", 
//				points[0], points[1], points[2], points[furthest],
//				c0,	Contains(c0),
//				Faces[0].P.GetDistanceToPoint(c0), Faces[1].P.GetDistanceToPoint(c0),
//				Faces[2].P.GetDistanceToPoint(c0), Faces[3].P.GetDistanceToPoint(c0));
			//if this is a tetrahedron, all is done
			if(points.Count == 4)
			{
				Points.AddRange(points);
				return;
			}
			//otherwise incrementally udate the seed
			points.RemoveAt(furthest);
			points.RemoveRange(0,3);
			Update(points);
		}
		public ConvexHull3D(IEnumerable<Vector3> points) : this(new List<Vector3>(points)) {}

		public ConvexHull3D Scale(float s)
		{
			Vector3[] scaled = new Vector3[Points.Count];
			for(int i = 0; i < Points.Count; i++) scaled[i] = Points[i]*s;
			return new ConvexHull3D(scaled);
		}

		/// <summary>
		/// Incrementally update the existing hull using provided points.
		/// </summary>
		/// <param name="points">Points used to update the hull.</param>
		public void Update(IEnumerable<Vector3> points)
		{
			List<Face> tmp     = new List<Face>();
			List<Face> visible = new List<Face>();
			foreach(Vector3 p in points)
			{
				//check visibility for each face
				visible.Clear();
				foreach(Face f in Faces) 
				{ if(f.Visible(p)) visible.Add(f); }
//				Utils.Log("Visible faces:"); //debug
//				visible.ForEach(f => f.Log()); //debug
				//if no face visible, v is inside the hull
				if(visible.Count == 0) continue;
				//otherwise delete visible from Faces 
				visible.ForEach(f => Faces.Remove(f));
				//if only 1 face is visible
				if(visible.Count == 1)
				{
					Face f = visible[0];
					Faces.Add(new Face(p, f.v0, f.v1));
					Faces.Add(new Face(p, f.v1, f.v2));
					Faces.Add(new Face(p, f.v2, f.v0));
//					Utils.Log("Faces: {0}", Faces.Count);
					continue;
				}
				//otherwise add all possible faces 
				tmp.Clear();
				foreach(Face f in visible)
				{
					tmp.Add(new Face(p, f.v0, f.v1));
					tmp.Add(new Face(p, f.v1, f.v2));
					tmp.Add(new Face(p, f.v2, f.v0));
				}
//				Utils.Log("Tmp faces:"); //debug
//				tmp.ForEach(f => f.Log()); //debug
				//and filter out the ones that are inside the hull
				for(int fi = 0; fi < tmp.Count; fi++)
				{
					Face f = tmp[fi];
					foreach(Face other in tmp)
					{
						if(f == other) continue;
						if(f.Visible(other.c))
						{ f = null; break; }
					}
					if(f != null) Faces.Add(f);
//					else { Utils.Log("Removing face:"); tmp[fi].Log(); }
				}
//				Utils.Log("Faces: {0}", Faces.Count);
			}
//			Utils.Log("Faces: {0}", Faces.Count);
//			int nump = Points.Count; //debug
			HashSet<Vector3> _Points = new HashSet<Vector3>();
			Faces.ForEach(f => { _Points.Add(f.v0); _Points.Add(f.v1); _Points.Add(f.v2); });
			Points.Clear(); Points.AddRange(_Points);
//			Utils.Log("ConvexHull: {0} was, {1} now", nump, Points.Count);//debug
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
			List<Vector3> points = new List<Vector3>();
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