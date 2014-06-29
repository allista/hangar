using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using KSP.IO;

namespace AtHangar
{
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class HangarWindow : AddonWindowBase<HangarWindow>
	{
		//settings
		static int highlight_hangar = 0; //1 -- enable highlight; -1 -- disable highlight; 0 -- do not set highlight (for mouseover highlighting).
		static bool selecting_crew = false;
		static Rect fWindowPos = new Rect();
		static Rect eWindowPos = new Rect();
		static Rect cWindowPos = new Rect();
		
		//this vessel
		Metric vessel_metric;
		
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
		
		//vessel crew
		CrewTransferWindow crew_window = new CrewTransferWindow();
		
		
		//vessel volume 
		private void updateVesselMetrics()
		{
			vessel_metric = new Metric();
			if(EditorLogic.fetch != null)
			{
				List<Part> parts = new List<Part>{};
				try { parts = EditorLogic.SortedShipList; }
				catch (NullReferenceException) { return; }
				vessel_metric = new Metric(parts);
			}
			else if(FlightGlobals.fetch != null)
				vessel_metric = new Metric(FlightGlobals.ActiveVessel);
		}
		
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
			updateVesselMetrics();
			UpdateGUIState();
		}

		void onVesselWasModified(Vessel vsl)
		{ 
			if(FlightGlobals.ActiveVessel == vsl) 
			{
				BuildHangarList(vsl);	
				BuildVesselList(selected_hangar);
				updateVesselMetrics();
			}
		}
		
		//update-init-destroy
		override public void UpdateGUIState()
		{
			base.UpdateGUIState();
			if(selected_hangar != null) 
			{
				if(enabled && highlight_hangar == 1) 
				{
					selected_hangar.part.SetHighlightColor(XKCDColors.LightSeaGreen);
					selected_hangar.part.SetHighlight(true);
				} 
				else if(highlight_hangar == -1)
				{
					selected_hangar.part.SetHighlightDefault();
					highlight_hangar = 0;
				}
			}
			if(hangars != null)
			{
				foreach(var p in hangars)
					p.UpdateMenus(enabled && p == selected_hangar);
			}
		}
		
		public override void OnUpdate() { updateVesselMetrics(); }
		
		
		new void Awake()
		{
			base.Awake();
			GameEvents.onVesselChange.Add(onVesselChange);
			GameEvents.onVesselWasModified.Add(onVesselWasModified);
		}

		new void OnDestroy()
		{
			GameEvents.onVesselChange.Remove(onVesselChange);
			GameEvents.onVesselWasModified.Remove(onVesselWasModified);
			base.OnDestroy();
		}
		
		public override void LoadSettings()
		{
			base.LoadSettings ();
			fWindowPos = configfile.GetValue<Rect>(mangleName("fWindowPos"), fWindowPos);
			eWindowPos = configfile.GetValue<Rect>(mangleName("eWindowPos"), eWindowPos);
			cWindowPos = configfile.GetValue<Rect>(mangleName("cWindowPos"), cWindowPos);
		}
		
		public override void SaveSettings()
		{
			configfile.SetValue(mangleName("fWindowPos"), fWindowPos);
			configfile.SetValue(mangleName("eWindowPos"), eWindowPos);
			configfile.SetValue(mangleName("cWindowPos"), cWindowPos);
			base.SaveSettings();
		}
		
		#region GUI
		//buttons
		void LaunchButton()
		{
			if(GUILayout.Button("Launch Vessel", Styles.yellow_button, GUILayout.ExpandWidth(true)))
				selected_hangar.TryRestoreVessel(selected_vessel.vid);
		}
		
		void ToggleGatesButton()
		{
			if(selected_hangar.gates_state == HangarGates.Closed ||
			   selected_hangar.gates_state == HangarGates.Closing)
			{
				if(GUILayout.Button("Open Gates", Styles.green_button, GUILayout.ExpandWidth(true)))
					selected_hangar.Open();
			}
			else if(GUILayout.Button("Close Gates", Styles.red_button, GUILayout.ExpandWidth(true)))
					selected_hangar.Close();
		}
		
		void ToggleStateButton()
		{
			if(selected_hangar.hangar_state == Hangar.HangarState.Inactive)
			{
				if(GUILayout.Button("Activate Hangar", Styles.green_button, GUILayout.ExpandWidth(true)))
					selected_hangar.Activate();
			}
			else
			{
				if(GUILayout.Button("Deactivate Hangar", Styles.red_button, GUILayout.ExpandWidth(true)))
				selected_hangar.Deactivate();
			}
		}
		
		void CrewTransferButton()
		{
			if(selected_vessel == null) return;
			if(GUILayout.Button("Change vessel crew", GUILayout.ExpandWidth(true)))
				selecting_crew = !selecting_crew;
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
			GUILayout.Label("Vessel Volume: "+Utils.formatVolume(vessel_metric.volume), GUILayout.ExpandWidth(true));
			GUILayout.Label("Vessel Dimensios: "+Utils.formatDimensions(vessel_metric.size), GUILayout.ExpandWidth(true));
			GUILayout.Label("Hangar Dimensios: "+Utils.formatDimensions(selected_hangar.hangar_metric.size), GUILayout.ExpandWidth(true));
			GUILayout.Label("Hangar volume: "+Utils.formatVolume(selected_hangar.hangar_metric.volume), GUILayout.ExpandWidth(true));
			GUILayout.Label("Used volume: "+Utils.formatVolume(selected_hangar.used_volume), GUILayout.ExpandWidth(true));
			GUILayout.Label(string.Format("Mass: {0} stored, {1} total", Utils.formatMass(selected_hangar.vessels_mass), Utils.formatMass(selected_hangar.vessel.GetTotalMass())), GUILayout.ExpandWidth(true));
			GUILayout.Label("Vessels docked: "+selected_hangar.numVessels(), GUILayout.ExpandWidth(true));
			GUILayout.Label(string.Format("Vessel crew: {0}/{1}", selected_hangar.vessel.GetCrewCount(), selected_hangar.vessel.GetCrewCapacity()), GUILayout.ExpandWidth(true));
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
			if(selected_hangar != hangar)
			{
				selected_hangar = hangar;
				hangar_id = hangar.GetInstanceID();
				hangar_list.SelectItem(hangars.IndexOf(hangar));
			}
		}

		void SelectHangar()
		{
			GUILayout.BeginHorizontal();
			hangar_list.DrawButton();
			if(GUILayout.Toggle(highlight_hangar == 1, "Highlight hangar")) highlight_hangar = 1;
			else if(highlight_hangar == 1) highlight_hangar = -1;
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
		
		//vessel info GUI
		void VesselInfo(int windowID)
		{ 
			GUILayout.BeginVertical();
			GUILayout.Label("Vessel Volume: "+Utils.formatVolume(vessel_metric.volume), GUILayout.ExpandWidth(true));
			GUILayout.Label("Vessel Dimensions: "+Utils.formatDimensions(vessel_metric.size), GUILayout.ExpandWidth(true));
			GUILayout.Label(String.Format("Crew Capacity: {0}", vessel_metric.CrewCapacity), GUILayout.ExpandWidth(true));
			GUILayout.EndVertical();
			GUI.DragWindow(new Rect(0, 0, 5000, 20));
		}
		
		//hangar controls GUI
		void HangarCotrols(int windowID)
		{
			//hangar list
			SelectHangar_start();
			GUILayout.BeginVertical();
			HangarInfo();
			SelectHangar();
			GUILayout.EndVertical();
			GUILayout.BeginHorizontal();
			ToggleGatesButton();
			ToggleStateButton();
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
				CrewTransferButton();
				LaunchButton();
			}
			CloseButton();
			SelectHangar_end();
			SelectVessel_end();
			
			GUI.DragWindow(new Rect(0, 0, 5000, 20));
		}
	
		override public void OnGUI()
		{
			if(Event.current.type != EventType.Layout) return;
			if((FlightGlobals.fetch == null || FlightGlobals.ActiveVessel == null) &&
				EditorLogic.fetch == null) return;
			base.OnGUI();
			if(hangars != null)
			{
				string hstate = selected_hangar.hangar_state.ToString();
				string gstate = selected_hangar.gates_state.ToString();
				fWindowPos = GUILayout.Window(GetInstanceID(),
											 fWindowPos, HangarCotrols,
											 String.Format("{0} {1}, Gates {2}", "Hangar", hstate, gstate),
											 GUILayout.Width(320));
			}
			else
			{
				eWindowPos = GUILayout.Window(GetInstanceID(),
											 eWindowPos, VesselInfo,
											 "Vessel info",
											 GUILayout.Width(300));
			}
			if(selecting_crew)
			{
				Hangar.StoredVessel sv = selected_hangar.GetVessel(selected_vessel.vid);
				if(sv != null)
					cWindowPos = crew_window.Draw(selected_hangar.vessel.GetVesselCrew(), 
					                              sv.crew, sv.CrewCapacity, cWindowPos);
			}
			UpdateGUIState();
		}
		#endregion
	}
}

