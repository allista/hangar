using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;
using KSPAPIExtensions;

namespace AtHangar
{
	static class HangarGUI
	{
		/// <summary>
		/// The camel case components matching regexp.
		/// From: http://stackoverflow.com/questions/155303/net-how-can-you-split-a-caps-delimited-string-into-an-array
		/// </summary>
		const string CamelCaseRegexp = "([a-z](?=[A-Z])|[A-Z](?=[A-Z][a-z]))";

		public static string ParseCamelCase(string s) { return Regex.Replace(s, CamelCaseRegexp, "$1 "); }

		#region Widgets
		public static void UsedVolumeLabel(float UsedVolume, float UsedVolumeFrac, string label="Used Volume")
		{
			GUILayout.Label(string.Format("{0}: {1}   {2:F1}%", label, 
				Utils.formatVolume(UsedVolume), UsedVolumeFrac*100f), 
				Styles.fracStyle(1-UsedVolumeFrac), GUILayout.ExpandWidth(true));
		}

		public static void PackedVesselLabel(PackedVessel v)
		{
			GUILayout.Label(string.Format("{0}: {1}   Cost: {2:F1}", 
				v.name, Utils.formatMass(v.mass), v.cost), 
				Styles.label, GUILayout.ExpandWidth(true));
		}

		public static Color UsedVolumeColor(HangarStorage s)
		{
			var frac = s.UsedVolumeFrac;
			return new Color(frac, 1f-frac, 0);
		}

		public static int LeftRightChooser(string text, int width = 0)
		{
			var left  = GUILayout.Button("<", Styles.yellow_button, GUILayout.Width(20));
			if(width > 0) GUILayout.Label(text, Styles.white, GUILayout.Width(width));
			else GUILayout.Label(text, Styles.white);
			var right = GUILayout.Button(">", Styles.yellow_button, GUILayout.Width(20));
			return left? -1 : (right? 1 : 0);
		}

		public static void CheckRect(ref Rect R)
		{
			//check size
			if(R.width > Screen.width) R.width = Screen.width;
			if(R.height > Screen.height) R.height = Screen.height;
			//check position
			if(R.xMin < 0) R.x -= R.xMin;
			else if(R.xMax > Screen.width) R.x -= R.xMax-Screen.width;
			if(R.yMin < 0) R.y -= R.yMin;
			else if(R.yMax > Screen.height) R.y -= R.yMax-Screen.height;
		}
		#endregion

		#region KSP_UI
		public static void EnableField(BaseField field, bool enable = true)
		{
			field.guiActive = field.guiActiveEditor = enable;
			var current_editor = field.uiControlEditor as UI_ChooseOption;
			if(current_editor != null) current_editor.controlEnabled = enable;
			current_editor = field.uiControlFlight as UI_ChooseOption;
			if(current_editor != null) current_editor.controlEnabled = enable;
		}

		static void setup_chooser_control(string[] names, string[] values, UI_Control control)
		{
			var current_editor = control as UI_ChooseOption;
			if(current_editor == null) return;
			current_editor.display = names;
			current_editor.options = values;
		}

		public static void SetupChooser(string[] names, string[] values, BaseField field)
		{
			setup_chooser_control(names, values, field.uiControlEditor);
			setup_chooser_control(names, values, field.uiControlFlight);
		}
		#endregion

		#region 3D
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

		public static void DrawArrow(Vector3 ori, Vector3 dir, Transform T, Color c = default(Color))
		{
			float l = dir.magnitude;
			float w = l*0.02f;
			w = w > 0.05f ? 0.05f : (w < 0.01f ? 0.01f : w);
			Vector3 x = Mathf.Abs(Vector3.Dot(dir.normalized,Vector3.up)) < 0.9f ? 
				Vector3.Cross(dir, Vector3.up).normalized : Vector3.Cross(Vector3.forward, dir).normalized;
			Vector3 y = Vector3.Cross(x, dir).normalized*w; x *= w;
			var edges = new Vector3[5];
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
			HangarGUI.DrawMesh(edges, tris, T, c);
		}

		public static void DrawYZ(Metric M, Transform T)
		{
			HangarGUI.DrawArrow(Vector3.zero, Vector3.up*M.extents.y*0.8f, T, Color.green);
			HangarGUI.DrawArrow(Vector3.zero, Vector3.forward*M.extents.z*0.8f, T, Color.blue);
		}

		#if DEBUG
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
			var tris = new List<int>();
			tris.AddRange(new Quad(0, 1, 3, 2));
			tris.AddRange(new Quad(0, 2, 6, 4));
			tris.AddRange(new Quad(0, 1, 5, 4));
			tris.AddRange(new Quad(1, 3, 7, 5));
			tris.AddRange(new Quad(2, 3, 7, 6));
			tris.AddRange(new Quad(6, 7, 5, 4));
			HangarGUI.DrawMesh(edges, tris, T, c);
		}

		public static void DrawPoint(Vector3 point, Transform T, Color c = default(Color))
		{ DrawBounds(new Bounds(point, Vector3.one*0.1f), T, c); }

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
			HangarGUI.DrawMesh(verts.ToArray(), tris, T, c, material);
		}
		#endif
		#endregion
	}

	class Multiplexer<T> where T : struct, IComparable, IFormattable, IConvertible
	{
		readonly Dictionary<T, bool> switches = new Dictionary<T, bool>();

		public Multiplexer() 
		{ 
			if(!typeof(T).IsEnum) throw new ArgumentException("Multiplexer<T> T must be an enumerated type");
			foreach(T s in Enum.GetValues(typeof(T))) switches.Add(s, false); 
		}

		public void Add(T key) { switches.Add(key, false); }
		public bool Remove(T key) { return switches.Remove(key); }
		public void Reset() { foreach(var s in new List<T>(switches.Keys)) switches[s] = false; }
		public bool Any() { foreach(var s in switches.Values) if(s) return true; return false; }

		public void Toggle(T key) 
		{ 
			bool state;
			if(switches.TryGetValue(key, out state))
				this[key] = !state;
		}

		public bool this[T key] 
		{ 
			get 
			{ 
				bool ret;
				if(!switches.TryGetValue(key, out ret))
					Utils.Log("Multiplexer: WARNING: no such switch {0}", key.ToString());
				return ret;
			} 
			set 
			{ 
				if(value) Reset();
				switches[key] = value; 
			}
		}
	}
}

