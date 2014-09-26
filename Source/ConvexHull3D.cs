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
		int i0, i1, i2;
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

		void init()
		{
			c = ConvexHull3D.Centroid(v0, v1, v2);
			P = new Plane(v0, v1, v2);
		}

		public Face() {}
		public Face(Vector3 v0, Vector3 v1, Vector3 v2)
		{
			this.v0 = v0; this.v1 = v1; this.v2 = v2;
			init();
		}
		public Face(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 p)
			: this(v0,v1,v2) { Orient(p); }

		public void UpdateIndices(List<Vector3> points)
		{
			i0 = points.FindIndex(p => p.Equals(v0));
			i1 = points.FindIndex(p => p.Equals(v1));
			i2 = points.FindIndex(p => p.Equals(v2));
		}

		public void UpdateFromIndices(List<Vector3> points)
		{
			v0 = points[i0];
			v1 = points[i1];
			v2 = points[i2];
			init();
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("i0", i0);
			node.AddValue("i1", i1);
			node.AddValue("i2", i2);
		}

		public void Load(ConfigNode node, List<Vector3> points)
		{
			i0 = int.Parse(node.GetValue("i0"));
			i1 = int.Parse(node.GetValue("i1"));
			i2 = int.Parse(node.GetValue("i2"));
			UpdateFromIndices(points);
		}
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

		public ConvexHull3D()
		{
			Points = new List<Vector3>();
			Faces  = new List<Face>();
		}

		public ConvexHull3D(List<Vector3> points) : this()
		{
			//initial checks
			if(points.Count < 4) 
				throw new NotSupportedException(string.Format("[Hangar] ConvexHull3D needs at least 4 edges, {0} given", points.Count));
			//build the seed tetrahedron
			var c0 = Centroid(points.GetRange(0,4).ToArray());
			Faces.Add(new Face(points[0], points[1], points[2], c0));
			Faces.Add(new Face(points[3], points[1], points[2], c0));
			Faces.Add(new Face(points[3], points[2], points[3], c0));
			Faces.Add(new Face(points[3], points[1], points[3], c0));
			//if this is a tetrahedron, all is done
			if(points.Count == 4)
			{
				Points.AddRange(points);
				return;
			}
			//otherwise incrementally udate the seed
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
			Points.Clear();
			Faces.ForEach(Points.AddRange);
		}

		/// <summary>
		/// Updates indices of vertices of faces.
		/// Use it BEFORE Save.
		/// </summary>
		public void UpdateFaces() { Faces.ForEach(f => f.UpdateIndices(Points)); }

		public void Scale(float s)
		{
			for(int i = 0; i < Points.Count; i++) Points[i] = Points[i]*s;
			Faces.ForEach(f => f.UpdateFromIndices(Points));
		}

		public void Save(ConfigNode node)
		{
			ConfigNode points = node.AddNode("POINTS");
			ConfigNode faces  = node.AddNode("FACES");
			foreach(Vector3 p in Points)
				points.AddValue("point", ConfigNode.WriteVector(p));
			foreach(Face f in Faces) 
				f.Save(faces.AddNode("FACE"));
		}

		public void Load(ConfigNode node)
		{
			Points.Clear(); Faces.Clear();
			foreach(ConfigNode.Value v in node.GetNode("POINTS").values)
				Points.Add(ConfigNode.ParseVector3(v.value));
			foreach(ConfigNode n in node.GetNode("FACES").nodes)	
			{ Face f = new Face(); f.Load(n, Points); }
		}
	}
}