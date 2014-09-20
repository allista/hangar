using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class TruncatedCone
	{
		//cone properties
		readonly public float R1, R2, H, Area;

		//internal constants
		float dR, h2, ny, nk;

		public static float SurfaceArea(float R1, float R2, float H)
		{ return Mathf.PI*(R1*R1 + R2*R2 + (R1+R2)*Mathf.Sqrt(H*H + Mathf.Pow(R1-R2, 2))); }

		public TruncatedCone(float R1, float R2, float H)
		{ 
			this.R1 = R1; this.R2 = R2; this.H = H; 
			Area = SurfaceArea(R1, R2, H);
			//constants
			dR = R2-R1;
			h2 = H/2f;
			ny = -dR/Mathf.Sqrt(H*H+dR*dR);
			nk = Mathf.Sqrt(1f - ny*ny);
		}

		public Basis GetTangentalBasis(Vector3 pos) 
		{ 
			pos = new Vector3(pos.x, 0, pos.z).normalized;
			Vector3 n = new Vector3(pos.x*nk, ny, pos.z*nk);
			Vector3 f = new Vector3(pos.x*dR, H,  pos.z*dR).normalized;
			Vector3 t = new Vector3(pos.z, 0, -pos.x).normalized;
			return new Basis(t, n, f);
		}

		public Vector3 NewSurfacePosition(Vector3 old_pos)
		{
			float R = Mathf.Sqrt(Mathf.Pow(dR/H*(old_pos.y+H/2) + R1, 2));
			return new Vector3(old_pos.x, old_pos.y, old_pos.z).normalized*R;
		}

		public void WriteTo(int sides, Mesh mesh, bool for_collider = false)
		{
			if(sides < 3) 
			{ sides = 3; Utils.Log("TruncatedCone: number of sides cannot be less than 3"); }
			if(for_collider && sides > 127)
			{ sides = 127; Utils.Log("TruncatedCone: for MeshCollider number of sides cannot be more than 127"); }
			//build the cone
			int loop = sides+1;
			int num_vertices = loop*(for_collider? 2: 4);
			Vector3[] vertices = new Vector3[num_vertices];
			Vector3[] normals  = new Vector3[num_vertices];
			Vector2[] uvs      = new Vector2[num_vertices];
			Vector4[] tangents = new Vector4[num_vertices];
			//offsets
			int bottom_base = loop*2;
			int top_base    = loop*3;
			//constants
			float fi = Mathf.PI*2f/sides;
			float du = 1f/sides;
			//top-bottom normals and tangents
			Vector3 bn = new Vector3( 0, -1, 0);
			Vector4 bt = new Vector4(-1,  0, 0, -1);
			Vector3 tn = new Vector3( 0,  1, 0);
			Vector4 tt = new Vector4( 1,  0, 0,  1);
			//vertices
			for(uint i = 0; i < loop; i++)
			{
				float a   = fi*i;
				float x   = Mathf.Sin(a);
				float z   = Mathf.Cos(a);
				float u   = du*i;
				Vector3 n = new Vector3(x*nk, ny, z*nk);
				Vector4 t = new Vector4(z, 0, -x, 1);
				//sides
				//bottom
				vertices[i] = new Vector3(R1*x, -h2, R1*z);
				uvs[i] = new Vector2(u, 0);
				normals[i]  = n;
				tangents[i] = t;
				//top
				vertices[loop+i] = new Vector3(R2*x, h2, R2*z);
				uvs[loop+i] = new Vector2(u, 0.49f); //leave a 1% gap between sides and bases
				normals[loop+i]  = n;
				tangents[loop+i] = t;
				//bases
				if(!for_collider)
				{
					//bottom
					vertices[i+bottom_base] = vertices[i];
					uvs[i+bottom_base] = new Vector2(0.25f+0.25f*x, 0.75f+0.25f*z);
					normals[i+bottom_base]  = bn;
					tangents[i+bottom_base] = bt;
					//top
					vertices[i+top_base] = vertices[loop+i];
					uvs[i+top_base] = new Vector2(0.75f+0.25f*x, 0.75f+0.25f*z);
					normals[i+top_base]  = tn;
					tangents[i+top_base] = tt;
				}
			}
			//loop ends
			vertices[loop-1]   = vertices[0];
			vertices[loop*2-1] = vertices[loop];
			//faces
			List<Triangle> faces = new List<Triangle>();
			//sides
			for(int i = 1; i < loop; i++)
				faces.Add(new Quad(i, loop+i, loop-1+i, i-1));
			if(!for_collider)
			{
				//bases
				for(int i = 2; i < sides; i++)
				{
					faces.Add(new Triangle(bottom_base+i, bottom_base+i-1, bottom_base));
					faces.Add(new Triangle(top_base, top_base+i-1, top_base+i));
				}
			}
			//write to the mesh
			List<int> triangles = new List<int>();
			faces.ForEach(triangles.AddRange);
			mesh.Clear();
			mesh.vertices  = vertices;
			mesh.normals   = normals;
			mesh.tangents  = tangents;
			mesh.uv        = uvs;
			mesh.triangles = triangles.ToArray();
			mesh.RecalculateBounds();
		}
	}
}