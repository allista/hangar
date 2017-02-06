//   VesselTransferWindow.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
	class VesselTransferWindow : GUIWindowBase
	{
		const int scroll_width  = 350;
		const int scroll_height = 100;

		public VesselTransferWindow()
		{
            width = scroll_width*2;
            height = scroll_height*2;
			WindowPos = new Rect(Screen.width/2-width/2, 100, width, 100);
		}

		public override void Awake()
		{
			base.Awake();
			Show(false);
		}

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
		}

		void parts_list(ref Vector2 scroll, ref HangarStorage selected, bool is_lhs=true)
		{
			if(storages == null || storages.Count == 0) return;
			scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(scroll_height), GUILayout.Width(scroll_width));
			Color selected_color = is_lhs? XKCDColors.Cyan : XKCDColors.Magenta;
			GUIStyle selected_style = is_lhs? Styles.cyan_button : Styles.magenta_button;
			GUILayout.BeginVertical();
			foreach(var s in storages)
			{
				GUIStyle style = (s == selected)? selected_style : Styles.normal_button;
				if(!is_lhs && s == lhs || is_lhs && s == rhs) 
					GUILayout.Label(s.Title(), Styles.grey, GUILayout.ExpandWidth(true));
				else if(GUILayout.Button(s.Title(), style, GUILayout.ExpandWidth(true))) 
				{
					if(selected != null) reset_highlight(selected);
					selected = s == selected ? null : s;
				}
			}
			if(selected != null)
			{
				selected.part.highlightType = Part.HighlightType.AlwaysOn;
				selected.part.SetHighlight(true, false);
				selected.part.SetHighlightColor(selected_color);
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
			GUILayout.FlexibleSpace();
			if(GUILayout.Button("Close", Styles.close_button, GUILayout.ExpandWidth(true))) Show(false);
			GUILayout.EndVertical();
			TooltipsAndDragWindow();
		}

		public void Draw(List<HangarStorage> storages)
		{
			if(doShow)
			{
				this.storages = storages;
				LockControls();
                WindowPos = GUILayout.Window(GetInstanceID(), 
				                             WindowPos, TransferWindow,
				                             "Relocate Vessels",
                                             GUILayout.Width(width),
                                             GUILayout.Height(height));
			}
			else UnlockControls();
		}

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

		public override void Show(bool show)
		{
			base.Show(show);
			if(!show) ClearSelection();
		}
	}
}

