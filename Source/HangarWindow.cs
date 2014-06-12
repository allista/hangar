using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using KSP.IO;

namespace AtHangar
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class HangarWindow : AddonWindowBase<HangarWindow>
	{
		//settings
		static bool hide_ui = false;
		static bool highlight_hangar = false;
		private new string window_name = "Hangar";
		
		//hangars
		List<Hangar> hangars;
		DropDownList hangar_list;
		Hangar selected_hangar;
		int hangar_id;
		
		//vessels
		List<Hangar.VesselInfo> vessels;
		DropDownList vessel_list;
		Hangar.VesselInfo selected_vessel;
		Guid vessel_id;
		
		
		//build dropdown list of all hangars in the vessel
		void BuildHangarList(Vessel vsl)
		{
			if(selected_hangar != null)	selected_hangar.part.SetHighlightDefault();
			hangars = null;
			hangar_list = null;
			selected_hangar = null;
			
			var _hangars = new List<Hangar>();
			foreach(var p in vsl.Parts)
				_hangars.AddRange(p.Modules.OfType<Hangar>());
			
			if(_hangars.Count > 0) 
			{
				hangars = _hangars;
				selected_hangar = hangars.Find(h => h.part.GetInstanceID() == hangar_id);
				if(selected_hangar == null) selected_hangar = hangars[0];
				var hangar_names = new List<string>();
				int ind = 0;
				foreach(var p in hangars) 
				{
					hangar_names.Add("hangar-" + ind);
					ind++;
				}
				hangar_list = new DropDownList(hangar_names);
			}
		}
		
		//build dropdown list of stored vessels
		void BuildVesselList(Hangar hangar)
		{
			vessels = null;
			vessel_list = null;
			selected_vessel = null;
			if(hangar == null) return;
			
			List<Hangar.VesselInfo> _vessels = hangar.GetVessels();
			if(_vessels.Count > 0) 
			{
				vessels = _vessels;
				selected_vessel = vessels.Find(v => v.vid == vessel_id);
				if(selected_vessel == null) selected_vessel = vessels[0];
				var vessel_names = new List<string>();
				foreach(var vsl in vessels) 
					vessel_names.Add(vsl.vesselName);
				vessel_list = new DropDownList(vessel_names);
			}
		}
		
		//callbacks
		void onVesselChange(Vessel vsl)
		{
			BuildHangarList(vsl);
			BuildVesselList(selected_hangar);
			UpdateGUIState();
		}

		void onVesselWasModified(Vessel vsl)
		{ 
			if(FlightGlobals.ActiveVessel == vsl) 
			{
				BuildHangarList(vsl);	
				BuildVesselList(selected_hangar);
			}
		}
		
		override public void UpdateGUIState()
		{
			enabled = !hide_ui && hangars != null && gui_enabled;
			if(selected_hangar != null) 
			{
				if(enabled && highlight_hangar) 
				{
					selected_hangar.part.SetHighlightColor(XKCDColors.LightSeaGreen);
					selected_hangar.part.SetHighlight(true);
				} 
				else selected_hangar.part.SetHighlightDefault();
			}
			if(hangars != null)
			{
				foreach(var p in hangars)
					p.UpdateMenus(enabled && p == selected_hangar);
			}
		}
		
		void onHideUI()
		{
			hide_ui = true;
			UpdateGUIState();
		}

		void onShowUI()
		{
			hide_ui = false;
			UpdateGUIState();
		}

		new void Awake()
		{
			base.Awake();
			GameEvents.onVesselChange.Add(onVesselChange);
			GameEvents.onVesselWasModified.Add(onVesselWasModified);
			GameEvents.onHideUI.Add(onHideUI);
			GameEvents.onShowUI.Add(onShowUI);
			enabled = false;
		}

		new void OnDestroy()
		{
			GameEvents.onVesselChange.Remove(onVesselChange);
			GameEvents.onVesselWasModified.Remove(onVesselWasModified);
			GameEvents.onHideUI.Remove(onHideUI);
			GameEvents.onShowUI.Remove(onShowUI);
			base.OnDestroy();
		}
		
		//buttons
		void LaunchButton()
		{
			if(GUILayout.Button("Launch Vessel", GUILayout.ExpandWidth(true)))
				selected_hangar.TryRestoreVessel(selected_vessel.vid);
		}
		
		void ToggleGatesButton()
		{
			if(selected_hangar.gates_state == HangarGates.Closed ||
			   selected_hangar.gates_state == HangarGates.Closing)
			{
				if(GUILayout.Button("Open Gates", GUILayout.ExpandWidth(true)))
					selected_hangar.Open();
			}
			else if(GUILayout.Button("Close Gates", GUILayout.ExpandWidth(true)))
					selected_hangar.Close();
		}
		
		void PrepareButton()
		{
			if(selected_hangar.hangar_state != Hangar.HangarState.Busy) return;
			if(GUILayout.Button("Prepare Hangar", GUILayout.ExpandWidth(true)))
				selected_hangar.Prepare();
		}
		
		void CloseButton()
		{
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if(GUILayout.Button("Close")) HideGUI();
			GUILayout.EndHorizontal();
		}
		
		//info labels
		void HangarInfo()
		{
			GUILayout.BeginVertical();
			GUILayout.Label("Total volume: "+selected_hangar.total_v, GUILayout.ExpandWidth(true));
			GUILayout.Label("Used volume: "+selected_hangar.used_v, GUILayout.ExpandWidth(true));
			GUILayout.Label("Vessels docked: "+selected_hangar.vessels_docked, GUILayout.ExpandWidth(true));
			GUILayout.Label(String.Format("Vessel crew: {0}/{1}", selected_hangar.vessel.GetCrewCount(), selected_hangar.vessel.GetCrewCapacity()), GUILayout.ExpandWidth(true));
			GUILayout.EndVertical();
		}
		
		
		//Hangar selection list
		void SelectHangar_start() 
		{ 
			hangar_list.styleListBox = Styles.listBox;
			hangar_list.styleListItem = Styles.listItem;
			hangar_list.DrawBlockingSelector(); 
		}

		void Select_Hangar(Hangar hangar)
		{
			if(selected_hangar) selected_hangar.part.SetHighlightDefault();
			selected_hangar = hangar;
			hangar_id = hangar.GetInstanceID();
			hangar_list.SelectItem(hangars.IndexOf(hangar));
			UpdateGUIState();
		}

		void SelectHangar()
		{
			GUILayout.BeginHorizontal();
			hangar_list.DrawButton();
			highlight_hangar = GUILayout.Toggle(highlight_hangar, "Highlight hangar");
			Select_Hangar(hangars[hangar_list.SelectedIndex]);
			GUILayout.EndHorizontal();
		}
		public static void SelectHangar(Hangar hangar) { instance.Select_Hangar(hangar); }

		void SelectHangar_end()
		{
			if(hangar_list == null) return;
			hangar_list.DrawDropDown();
			hangar_list.CloseOnOutsideClick();
		}
		
		
		//Vessel selection list
		void SelectVessel_start() 
		{ 
			vessel_list.styleListBox = Styles.listBox;
			vessel_list.styleListItem = Styles.listItem;
			vessel_list.DrawBlockingSelector(); 
		}

		void Select_Vessel(Hangar.VesselInfo vsl)
		{
			selected_vessel = vsl;
			vessel_id = vsl.vid;
			vessel_list.SelectItem(vessels.IndexOf(vsl));
		}

		void SelectVessel()
		{
			GUILayout.BeginHorizontal();
			vessel_list.DrawButton();
			Select_Vessel(vessels[vessel_list.SelectedIndex]);
			GUILayout.EndHorizontal();
		}
		public static void SelectVessel(Hangar.VesselInfo vsl) { instance.Select_Vessel(vsl); }

		void SelectVessel_end()
		{
			if(vessel_list == null) return;
			vessel_list.DrawDropDown();
			vessel_list.CloseOnOutsideClick();
		}
		
		
		//the window itself
		override public void WindowGUI(int windowID)
		{
			Styles.Init();
			//hangar list
			SelectHangar_start();
			GUILayout.BeginVertical();
			HangarInfo();
			SelectHangar();
			GUILayout.EndVertical();
			GUILayout.BeginHorizontal();
			ToggleGatesButton();
			PrepareButton();
			GUILayout.EndHorizontal();
			//vessel list
			if(vessels == null && selected_hangar.numVessels() > 0 ||
			   vessels != null && vessels.Count != selected_hangar.numVessels())
				BuildVesselList(selected_hangar);
			if(vessels != null)
			{
				SelectVessel_start();
				GUILayout.BeginVertical();
				SelectVessel();
				GUILayout.EndVertical();
				LaunchButton();
				
			}
			//situation buttons
			
			CloseButton();
			SelectHangar_end();
			SelectVessel_end();
			
			GUI.DragWindow(new Rect(0, 0, 5000, 20));
		}
	
		override public void OnGUI()
		{
			if (Event.current.type != EventType.Layout) return;
			string hstate = selected_hangar.hangar_state.ToString();
			string gstate = selected_hangar.gates_state.ToString();
			windowPos = GUILayout.Window(GetInstanceID(),
										  windowPos, WindowGUI,
										  String.Format("{0} {1}, Gates {2}", window_name, hstate, gstate),
										  GUILayout.Width(320));
		}

	}
}

