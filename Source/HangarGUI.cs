using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	class VesselTransferWindow: MonoBehaviour
	{
		const int scroll_width  = 300;
		const int scroll_height = 200;

		public bool Closed { get; private set; }

		List<HangarStorage> storages;
		HangarStorage lhs, rhs;
		PackedVessel lhs_selected, rhs_selected;

		Vector2 lhs_parts_scroll   = Vector2.zero;
		Vector2 rhs_parts_scroll   = Vector2.zero;
		Vector2 lhs_vessels_scroll = Vector2.zero;
		Vector2 rhs_vessels_scroll = Vector2.zero;

		static void reset_highlight(PartModule pm)
		{
			if(pm == null || pm.part == null) return;
			pm.part.SetHighlightDefault();
			pm.part.highlightRecurse = HighLogic.LoadedSceneIsEditor;
		}

		void parts_list(ref Vector2 scroll, ref HangarStorage selected, bool is_lhs=true)
		{
			if(storages == null || storages.Count == 0) return;
			scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(scroll_height), GUILayout.Width(scroll_width));
			GUILayout.BeginVertical();
			foreach(var s in storages)
			{
				GUIStyle style = (s == selected)? Styles.yellow_button : Styles.normal_button;
				if(!is_lhs && s == lhs || is_lhs && s == rhs) 
					GUILayout.Label(s.name, Styles.label, GUILayout.ExpandWidth(true));
				else if(GUILayout.Button(s.name, style, GUILayout.ExpandWidth(true))) 
				{
					if(selected != null) reset_highlight(selected);
					selected = s == selected ? null : s;
				}
			}
			if(selected != null)
			{
				selected.part.highlightRecurse = false;
				selected.part.SetHighlightColor(XKCDColors.Yellow);
				selected.part.SetHighlight(true);
			}
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
		}

		static void vessels_list(HangarStorage storage, ref Vector2 scroll, ref PackedVessel selected, bool is_lhs=true)
		{
			if(storage == null) return;
			scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(scroll_height), GUILayout.Width(scroll_width));
			GUILayout.BeginVertical();
			List<PackedVessel> vessels = storage.GetAllVesselsBase();
			vessels.Sort((a, b) => a.name.CompareTo(b.name));
			foreach(var v in vessels)
			{

				GUILayout.BeginHorizontal();
				if(is_lhs) HangarGUI.PackedVesselLabel(v);
				if(GUILayout.Button(is_lhs? ">>" : "<<", Styles.normal_button, GUILayout.ExpandWidth(true))) selected = v;
				if(!is_lhs) HangarGUI.PackedVesselLabel(v);
				GUILayout.EndHorizontal();
			}
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
		}

		void TransferWindow(int windowId)
		{
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			GUILayout.BeginVertical();
			//lhs
			parts_list(ref lhs_parts_scroll, ref lhs);
			if(lhs != null) 
			{
				HangarGUI.UsedVolumeLabel(lhs.UsedVolume, lhs.UsedVolumeFrac);
				vessels_list(lhs, ref lhs_vessels_scroll, ref lhs_selected);
			}
			GUILayout.EndVertical();
			//rhs
			GUILayout.BeginVertical();
			parts_list(ref rhs_parts_scroll, ref rhs, false);
			if(rhs != null) 
			{
				HangarGUI.UsedVolumeLabel(rhs.UsedVolume, rhs.UsedVolumeFrac);
				vessels_list(rhs, ref rhs_vessels_scroll, ref rhs_selected, false);
			}
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
			Closed = GUILayout.Button("Close", GUILayout.ExpandWidth(true));
			GUILayout.EndVertical();
			GUI.DragWindow(new Rect(0, 0, 500, 20));
		}

		public Rect Draw(List<HangarStorage> storages, Rect windowPos, int windowId)
		{
			this.storages = storages;
			windowPos = GUILayout.Window(windowId, 
				windowPos, TransferWindow,
				string.Format("Relocate Vessels"),
				GUILayout.Width(scroll_width*2));
			return windowPos;
		}

		public Rect Draw(List<HangarStorage> storages, Rect windowPos)
		{ return Draw(storages, windowPos, GetInstanceID()); }

		public void TransferVessel()
		{
			if(lhs == null || rhs == null) return;
			if(lhs_selected != null)
			{
				lhs.TryTransferTo(lhs_selected, rhs);
				lhs_selected = null;
			}
			else if(rhs_selected != null)
			{
				rhs.TryTransferTo(rhs_selected, lhs);
				rhs_selected = null;
			}
		}

		public void ClearSelection()
		{
			reset_highlight(lhs);
			reset_highlight(rhs);
			lhs = rhs = null; lhs_selected = null;
			if(storages != null) storages.ForEach(reset_highlight);
		}
	}

	class Multiplexer
	{
		readonly Dictionary<string, bool> switches = new Dictionary<string, bool>();

		public Multiplexer(params string[] switches)
		{ foreach(var s in switches) this.switches.Add(s, false); }

		public void Add(string name) { switches.Add(name, false); }
		public bool Remove(string name) { return switches.Remove(name); }
		public void Reset() { foreach(var s in new List<string>(switches.Keys)) switches[s] = false; }
		public bool Any() { foreach(var s in switches.Values) if(s) return true; return false; }

		public void Toggle(string name) 
		{ 
			bool state;
			if(switches.TryGetValue(name, out state))
				switches[name] = !state;
		}

		public bool this[string name] 
		{ 
			get 
			{ 
				bool ret;
				if(!switches.TryGetValue(name, out ret))
					Utils.Log("Multiplexer: WARNING: no such switch {0}", name);
				return ret;
			} 
			set 
			{ 
				if(value) Reset();
				switches[name] = value; 
			}
		}
	}

	static class HangarGUI
	{
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
}

