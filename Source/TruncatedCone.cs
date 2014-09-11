using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	class TruncatedCone
	{
		class Triangle : IEnumerable<int>
		{
			readonly protected int i1, i2, i3;

			public Triangle(int i1, int i2, int i3)
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

		class Quad : Triangle
		{
			readonly int i4;

			public Quad(int i1, int i2, int i3, int i4)
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

		readonly int num_vertices;
		readonly Vector3[] vertices;
		readonly Vector3[] normals;
		readonly Vector2[] uvs;
		readonly Vector4[] tangents;
		readonly List<Triangle> faces = new List<Triangle>();

		public TruncatedCone(float R1, float R2, float H, int sides)
		{
			if(sides < 3) throw new InvalidProgramException("TruncatedCone: number of sides " +
				"cannot be less than 3");
			num_vertices = sides*4;
			vertices = new Vector3[num_vertices];
			normals  = new Vector3[num_vertices];
			uvs      = new Vector2[num_vertices];
			tangents = new Vector4[num_vertices];
			//offsets
			int bottom_base = sides*2;
			int top_base    = sides*3;
			//constants
			float dR = R1-R2;
			float h2 = H/2f;
			float fi = Mathf.PI*2f/sides;
			float du = 1f/sides;
			float ny = dR/Mathf.Sqrt(H*H+dR*dR);
			float nk = Mathf.Sqrt(1f - ny*ny);
			//top-bottom normals and tangents
			Vector3 bn = new Vector3(0, -1, 0);
			Vector4 bt = new Vector4(1,  0, 0, -1);
			Vector3 tn = new Vector3(0,  1, 0);
			Vector4 tt = new Vector4(1,  0, 0,  1);
			//vertices
			for(uint i = 0; i < sides; i++)
			{
				float a    = fi*i;
				float x    = Mathf.Sin(a);
				float z    = Mathf.Cos(a);
				float u    = du*i;
				Vector3 n  = new Vector3(x*nk, ny, z*nk);
				Vector4 t  = new Vector4(z, 0, -x, 1);
				//bottom
				vertices[i] = vertices[i+bottom_base] = new Vector3(R1*x, -h2, R1*z);
				uvs[i] = new Vector2(u, 0);
				normals[i]  = n;
				tangents[i] = t;
				uvs[i+bottom_base] = new Vector2(0.5f*x, 0.5f+0.5f*z);
				normals[i+bottom_base]  = bn;
				tangents[i+bottom_base] = bt;
				//top
				vertices[sides+i] = vertices[i+top_base] = new Vector3(R2*x, h2, R2*z);
				uvs[sides+i] = new Vector2(u, 0.49f); //leave a 1% gap between sides and bases
				normals[sides+i]  = n;
				tangents[sides+i] = t;
				uvs[i+top_base] = new Vector2(0.5f+0.5f*x, 0.5f+0.5f*z);
				normals[i+top_base]  = tn;
				tangents[i+top_base] = tt;
			}
			//bases
			for(int i = 2; i < sides; i++)
			{
				faces.Add(new Triangle(bottom_base, bottom_base+i-1, bottom_base+i));
				faces.Add(new Triangle(top_base, top_base+i-1, top_base+i));
			}
			//sides
			for(int i = 1; i < sides; i++)
				faces.Add(new Quad(i-1, sides+i-1, sides+i, i));
		}

		public void WriteTo(Mesh mesh, string name = null)
		{
			List<int> triangles = new List<int>();
			faces.ForEach(triangles.AddRange);
			mesh.Clear ();
			if(name != null) mesh.name = name;
			mesh.vertices  = vertices;
			mesh.normals   = normals;
			mesh.tangents  = tangents;
			mesh.uv        = uvs;
			mesh.triangles = triangles.ToArray();
		}
	}
}