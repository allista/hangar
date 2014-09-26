//this algorithm was adopted from: http://www.nicoptere.net/AS3/convexhull/src/ConvexHull.as
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public static class PlaneExtensions
	{ 
		public static void Flip(this Plane p) { p.normal = -p.normal; } 
		public static void Orient(this Plane p, Vector3 c) 
		{ if(p.GetSide(c)) p.normal = -p.normal; } 
	}

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

		public bool GetSide(Vector3 v) { return P.GetSide(v); }

		public void Orient(Vector3 p) 
		{ 
			if(P.GetSide(p)) P.Flip();
			var t = v0; v0 = v1; v1 = t;
		}

		public Face(Vector3 v0, Vector3 v1, Vector3 v2)
		{
			this.v0 = v0; this.v1 = v1; this.v2 = v2;
			c = ConvexHull3D.Centroid(v0, v1, v2);
			P = new Plane(v0, v1, v2);
		}

		public Face(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 p)
			: this(v0,v1,v2) { Orient(p); }
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

		public ConvexHull3D(List<Vector3> points)
		{
			//initial checks
			if(points.Count < 4) 
				throw new NotSupportedException(string.Format("[Hangar] ConvexHull3D needs at least 4 edges, {0} given", points.Count));
			if(points.Count == 4) //trivial case
			{
				Points = points;
				return;
			}
			//lists of faces
			Faces = new List<Face>();
			//build the seed tetrahedron
			var c0 = Centroid(points.GetRange(0,4).ToArray());
			Faces.Add(new Face(points[0], points[1], points[2], c0));
			Faces.Add(new Face(points[3], points[1], points[2], c0));
			Faces.Add(new Face(points[3], points[2], points[3], c0));
			Faces.Add(new Face(points[3], points[1], points[3], c0));
			//incrementally udate the seed
			Update(points.GetRange(4,points.Count-4));
		}
		public ConvexHull3D(Vector3[] points) : this(new List<Vector3>(points)) {}

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
				{ if(f.GetSide(p)) visible.Add(f); }
				//if no face visible, v is inside the hull
				if(visible.Count == 0) continue;
				//otherwise delete visible from faces 
				visible.ForEach(f => Faces.Remove(f));
				//if only 1 face is visible
				if(visible.Count == 1)
				{
					Face f = visible[0];
					Faces.Add(new Face(p, f.v0, f.v1));
					Faces.Add(new Face(p, f.v1, f.v2));
					Faces.Add(new Face(p, f.v2, f.v0));
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
				//and filter the ones that are inside the hull
				for(int fi = 0; fi < tmp.Count-1; fi++)
				{
					Face f = tmp[fi];
					for(int fj = fi; fj < tmp.Count; fj++)
					{
						if(f.GetSide(tmp[fj].c)) 
						{ f = null; break; }
					}
					if(f != null) Faces.Add(f);
				}
			}
			Faces.ForEach(Points.AddRange);
		}

		public void Save(ConfigNode node)
		{
			foreach(Vector3 p in Points)
				node.AddValue("point", ConfigNode.WriteVector(p));
		}

		public void Load(ConfigNode node)
		{
			Points.Clear();
			foreach(ConfigNode.Value v in node.values)
			{
				Vector3 p;
				try 
				{ 
					p = ConfigNode.ParseVector3(v.value); 
					Points.Add(p);
				}
				catch(Exception ex)	{ Debug.LogException(ex); }
			}
		}
	}
}