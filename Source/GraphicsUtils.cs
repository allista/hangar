using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AtHangar
{
	public static partial class Utils
	{
		static Material _material_no_z;
		public static Material  material_no_z
		{
			get
			{
				if (_material_no_z == null)
					_material_no_z = new Material(Shader.Find("GUI/Text Shader"));
				return new Material(_material_no_z);
			}
		}

		static Material _material;
		public static Material  material
		{
			get
			{
				if (_material == null)
					_material = new Material(Shader.Find("Diffuse"));
				return new Material(_material);
			}
		}

		public static void DrawMesh(Vector3[] edges, IEnumerable<int> tris, Transform t, Color c = default(Color), Material mat = null)
		{
			//make a mesh
			var m = new Mesh();
			m.vertices  = edges;
			m.triangles = tris.ToArray();
			//recalculate normals and bounds
			m.RecalculateBounds();
			m.RecalculateNormals();
			//make own material
			if(mat == null) mat = material_no_z;
			mat.color = (c == default(Color))? Color.white : c;
			//draw mesh in the world space
			Graphics.DrawMesh(m, t.localToWorldMatrix, mat, 0);
		}

		//		edges[0] = new Vector3(min.x, min.y, min.z); //left-bottom-back
		//	    edges[1] = new Vector3(min.x, min.y, max.z); //left-bottom-front
		//	    edges[2] = new Vector3(min.x, max.y, min.z); //left-top-back
		//	    edges[3] = new Vector3(min.x, max.y, max.z); //left-top-front
		//	    edges[4] = new Vector3(max.x, min.y, min.z); //right-bottom-back
		//	    edges[5] = new Vector3(max.x, min.y, max.z); //right-bottom-front
		//	    edges[6] = new Vector3(max.x, max.y, min.z); //right-top-back
		//	    edges[7] = new Vector3(max.x, max.y, max.z); //right-top-front
		public static void DrawBounds(Bounds b, Transform T, Color c)
		{
			Vector3[] edges = Metric.BoundsEdges(b);
			List<int> tris = new List<int>();
			tris.AddRange(new Quad(0, 1, 3, 2));
			tris.AddRange(new Quad(0, 2, 6, 4));
			tris.AddRange(new Quad(0, 1, 5, 4));
			tris.AddRange(new Quad(1, 3, 7, 5));
			tris.AddRange(new Quad(2, 3, 7, 6));
			tris.AddRange(new Quad(6, 7, 5, 4));
			Utils.DrawMesh(edges, tris, T, c);
		}

		public static void DrawPoint(Vector3 point, Transform T, Color c = default(Color))
		{ DrawBounds(new Bounds(point, Vector3.one*0.1f), T, c); }

		public static void DrawArrow(Vector3 ori, Vector3 dir, Transform T, Color c = default(Color))
		{
			float l = dir.magnitude;
			float w = l*0.02f;
			w = w > 0.05f ? 0.05f : (w < 0.01f ? 0.01f : w);
			Vector3 x = Mathf.Abs(Vector3.Dot(dir.normalized,Vector3.up)) < 0.9f ? 
				Vector3.Cross(dir, Vector3.up).normalized : Vector3.Cross(Vector3.forward, dir).normalized;
			Vector3 y = Vector3.Cross(x, dir).normalized*w; x *= w;
			Vector3[] edges = new Vector3[5];
			edges[0] = ori+dir; 
			edges[1] = ori-x-y;
			edges[2] = ori-x+y;
			edges[3] = ori+x+y;
			edges[4] = ori+x-y;
			var tris = new List<int>();
			tris.AddRange(new Quad(1, 2, 3, 4));
			tris.AddRange(new Triangle(0, 1, 2));
			tris.AddRange(new Triangle(0, 2, 3));
			tris.AddRange(new Triangle(0, 3, 4));
			tris.AddRange(new Triangle(0, 4, 1));
			Utils.DrawMesh(edges, tris, T, c);
		}

		public static void DrawYZ(Metric M, Transform T)
		{
			Utils.DrawArrow(Vector3.zero, Vector3.up*M.extents.y*0.8f, T, Color.green);
			Utils.DrawArrow(Vector3.zero, Vector3.forward*M.extents.z*0.8f, T, Color.blue);
		}

		public static void DrawHull(Metric M, Transform T, Color c = default(Color))
		{
			if(M.hull == null) return;
			var h = M.hull;
			var verts = new List<Vector3>(h.Faces.Count*3);
			var tris  = new List<int>(h.Faces.Count*3);
			foreach(Face f in h.Faces) 
			{
				verts.AddRange(f);
				tris.AddRange(new []{0+tris.Count, 1+tris.Count, 2+tris.Count});
			}
			Utils.DrawMesh(verts.ToArray(), tris, T, c, material);
		}
	}

	public class Triangle : IEnumerable<int>
	{
		readonly protected int i1, i2, i3;

		public Triangle(int i1, int i2, int i3) //indecies need to be clockwise
		{ this.i1 = i1; this.i2 = i2; this.i3 = i3; }

		public virtual IEnumerator<int> GetEnumerator()
		{
			yield return i1;
			yield return i2;
			yield return i3;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{ return GetEnumerator(); }
	}

	public class Quad : Triangle
	{
		readonly int i4;

		public Quad(int i1, int i2, int i3, int i4) //indecies need to be clockwise
			: base(i1, i2, i3) { this.i4 = i4; }

		public override IEnumerator<int> GetEnumerator ()
		{
			yield return i1;
			yield return i2;
			yield return i3;

			yield return i3;
			yield return i4;
			yield return i1;
		}
	}

	public class Basis
	{
		public readonly Vector3 x, y, z;
		public Basis(Vector3 x, Vector3 y, Vector3 z)
		{ this.x = x; this.y = y; this.z = z; }
	}
}

