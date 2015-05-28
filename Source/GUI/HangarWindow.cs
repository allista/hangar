//This code is partly based on the code from Extraplanetary Launchpads plugin. BuildWindow.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AtHangar
{
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class HangarWindow : AddonWindowBase<HangarWindow>
	{
		enum HighlightState {  None, Enable, Disable }
		//settings
		static HighlightState highlight_hangar, highlight_storage;
		enum TransferWindows { SelectCrew, TransferResources, RelocateVessels }
		static Multiplexer<TransferWindows> selected_window = new Multiplexer<TransferWindows>();
		static Rect fWindowPos, eWindowPos, cWindowPos, rWindowPos, vWindowPos;
		static bool draw_directions;

		//lock names
		static readonly string eLock = "HangarWindow.editorUI";
		
		//this vessel
		static Vessel current_vessel
		{
			get
			{
				if(FlightGlobals.fetch != null && 
					FlightGlobals.ActiveVessel != null &&
					!FlightGlobals.ActiveVessel.isEVA)
					return FlightGlobals.ActiveVessel;
				return null;
			}
		}
		Metric vessel_metric;
		
		//hangars
		readonly List<HangarMachinery> hangars = new List<HangarMachinery>();
		readonly DropDownList hangar_list = new DropDownList();
		HangarMachinery selected_hangar;
		uint hangar_id;
		
		//vessels
		List<StoredVessel> vessels = new List<StoredVessel>();
		readonly DropDownList vessel_list = new DropDownList();
		StoredVessel selected_vessel;
		Guid vessel_id;
		
		//vessel relocation, crew and resources transfers
		readonly CrewTransferWindow crew_window = new CrewTransferWindow();
		readonly ResourceTransferWindow resources_window = new ResourceTransferWindow();
		readonly VesselTransferWindow vessels_window = new VesselTransferWindow();
		
		//vessel volume 
		void updateVesselMetric(Vessel vsl = null)
		{
			vessel_metric.Clear();
			if(vsl != null && vsl.loaded) 
				vessel_metric = new Metric(vsl);
			else if(EditorLogic.fetch != null)
			{
				var parts = new List<Part>();
				try { parts = EditorLogic.SortedShipList; }
				catch (NullReferenceException) { return; }
				#if !DEBUG
				if(parts.Count > 0 && parts[0] != null)
					vessel_metric = new Metric(parts);
				#else
				if(parts.Count > 0 && parts[0] != null)
					vessel_metric = new Metric(parts, true);
				#endif
			}
		}
		
		//build dropdown list of all hangars in the vessel
		void BuildHangarList(Vessel vsl)
		{
			//save selected hangar
			HangarMachinery prev_selected = selected_hangar;
			//reset state
			hangars.Clear();
			hangar_list.Items = new List<string>();
			selected_hangar = null;
			//check the vessel
			if(vsl == null) return;
			//build new list
			foreach(var p in vsl.Parts)
				hangars.AddRange(p.Modules.OfType<HangarMachinery>().Where(h => h.enabled));
			if(hangars.Count > 0) 
			{
				selected_hangar = hangars.Find(h => h.part.flightID == hangar_id);
				if(selected_hangar == null) selected_hangar = hangars[0];
				var hangar_names = new List<string>();
				for(int i = 0; i < hangars.Count; i++)
				{
					var h_name = hangars[i].HangarName == string.Empty ? "Unnamed Hangar" : hangars[i].HangarName;
					hangar_names.Add(string.Format("{0} {1}", i+1, h_name));
				}
				hangar_list.Items = hangar_names;
				hangar_list.SelectItem(hangars.IndexOf(selected_hangar));
			}
			//clear highlight of previously selected hangar
			if(highlight_hangar == HighlightState.Disable && prev_selected != null && selected_hangar != prev_selected)
				prev_selected.part.SetHighlightDefault();
		}
		
		//build dropdown list of stored vessels
		void BuildVesselList(HangarMachinery hangar)
		{
			//reset stat
			vessels.Clear();
			vessel_list.Items = new List<string>();
			selected_vessel = null;
			//check hangar
			if(hangar == null) return;
			//build new list
			vessels = hangar.GetVessels();
			if(vessels.Count == 0) return ;
			selected_vessel = vessels.Find(v => v.id == vessel_id);
			if(selected_vessel == null) selected_vessel = vessels[0];
			var vessel_names = new List<string>();
			for(int i = 0; i < vessels.Count; i++)
				vessel_names.Add(string.Format("{0} {1}", i+1, vessels[i].name));
			vessel_list.Items = vessel_names;
			vessel_list.SelectItem(vessels.IndexOf(selected_vessel));
		}
		
		//update-init-destroy
		void onVesselChange(Vessel vsl)
		{
			updateVesselMetric(vsl);
			BuildHangarList(vsl);
			BuildVesselList(selected_hangar);
			UpdateGUIState();
		}

		void onVesselWasModified(Vessel vsl)
		{ 
			if(FlightGlobals.ActiveVessel == vsl) 
			{
				updateVesselMetric(vsl);
				BuildHangarList(vsl);
				BuildVesselList(selected_hangar);
			}
		}

		public override void OnUpdate() 
		{ 
			if(!enabled) return;
			var vsl = current_vessel;
			updateVesselMetric(vsl);
			BuildHangarList(vsl);
			UpdateGUIState();
			if(selected_hangar != null)
			{
				//vessel list
				if(vessels.Count != selected_hangar.VesselsDocked)
					BuildVesselList(selected_hangar);
				if(selected_vessel != null && selected_window[TransferWindows.TransferResources])
					selected_hangar.updateResourceList();
			}
		}
		
		override public void UpdateGUIState()
		{
			base.UpdateGUIState();
			if(!enabled) Utils.LockIfMouseOver(eLock, eWindowPos, false);
			if(selected_hangar != null) 
			{
				//first highlight storage
				if(selected_hangar.ConnectedStorage.Count > 1)
				{
					if(enabled && highlight_storage == HighlightState.Enable) 
						foreach(var s in selected_hangar.ConnectedStorage)
						{
							s.part.SetHighlightColor(HangarGUI.UsedVolumeColor(s));
							s.part.SetHighlight(true, false);
						}
					else if(highlight_storage == HighlightState.Disable)
					{
						foreach(var s in selected_hangar.ConnectedStorage)
							s.part.SetHighlightDefault();
						highlight_storage = HighlightState.None;
					}
				}
				//then highlight hangar
				if(enabled && highlight_hangar == HighlightState.Enable) 
				{
					selected_hangar.part.SetHighlightColor(XKCDColors.LightSeaGreen);
					selected_hangar.part.SetHighlight(true, false);
				} 
				else if(highlight_hangar == HighlightState.Disable)
				{
					selected_hangar.part.SetHighlightDefault();
					highlight_hangar = HighlightState.None;
				}
			}
		}
		
		new void Awake()
		{
			base.Awake();
			GameEvents.onVesselChange.Add(onVesselChange);
			GameEvents.onVesselWasModified.Add(onVesselWasModified);
		}

		new void OnDestroy()
		{
			Utils.LockIfMouseOver(eLock, eWindowPos, false);
			GameEvents.onVesselChange.Remove(onVesselChange);
			GameEvents.onVesselWasModified.Remove(onVesselWasModified);
			base.OnDestroy();
		}
		
		public override void LoadSettings()
		{
			base.LoadSettings();
			fWindowPos = GetConfigValue<Rect>("fWindowPos", fWindowPos);
			eWindowPos = GetConfigValue<Rect>("eWindowPos", eWindowPos);
			cWindowPos = GetConfigValue<Rect>("cWindowPos", cWindowPos);
			rWindowPos = GetConfigValue<Rect>("rWindowPos", rWindowPos);
			vWindowPos = GetConfigValue<Rect>("vWindowPos", vWindowPos);
			hangar_id  = GetConfigValue<uint>("hangar_id",  default(uint));
			vessel_id  = GetConfigValue<Guid>("vessel_id",  Guid.Empty);
		}
		
		public override void SaveSettings()
		{
			SetConfigValue("fWindowPos", fWindowPos);
			SetConfigValue("eWindowPos", eWindowPos);
			SetConfigValue("cWindowPos", cWindowPos);
			SetConfigValue("rWindowPos", rWindowPos);
			SetConfigValue("vWindowPos", vWindowPos);
			SetConfigValue("hangar_id",  hangar_id);
			SetConfigValue("vessel_id",  vessel_id);
			base.SaveSettings();
		}
		
		#region GUI
		//buttons
		void LaunchButton()
		{
			GUILayout.BeginHorizontal();
			if(selected_hangar != null 
				&&  selected_hangar.LaunchVelocity != Vector3.zero 
				&& !selected_hangar.vessel.LandedOrSplashed)
				selected_hangar.LaunchWithPunch = GUILayout.Toggle(selected_hangar.LaunchWithPunch, "Push Vessel Out");
			if(GUILayout.Button("Launch Vessel", Styles.yellow_button, GUILayout.ExpandWidth(true)))
				selected_hangar.TryRestoreVessel(selected_vessel);
			GUILayout.EndHorizontal();
		}
		
		void ToggleGatesButton()
		{
			switch(selected_hangar.gates_state)
			{
			case AnimatorState.Closed:
			case AnimatorState.Closing:
				if(GUILayout.Button("Open Gates", Styles.green_button, GUILayout.ExpandWidth(true)))
					selected_hangar.Open();
				break;
			case AnimatorState.Opened:
			case AnimatorState.Opening:
				if(GUILayout.Button("Close Gates", Styles.red_button, GUILayout.ExpandWidth(true)))
					selected_hangar.Close();
				break;
			}
		}
		
		void ToggleStateButton()
		{
			if(selected_hangar.hangar_state == HangarMachinery.HangarState.Inactive)
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
			if(selected_hangar.NoCrewTransfers ||
				selected_vessel == null ||
				selected_vessel.CrewCapacity == 0 ||
				selected_hangar.vessel.GetCrewCount() == 0) return;
			if(GUILayout.Button("Change Vessel Crew", GUILayout.ExpandWidth(true)))
				selected_window.Toggle(TransferWindows.SelectCrew);
		}
		
		void ResourcesTransferButton()
		{
			if(selected_hangar.NoResourceTransfers ||
				selected_vessel == null) return;
			if(GUILayout.Button("Transfer Resources", GUILayout.ExpandWidth(true)))
				selected_window.Toggle(TransferWindows.TransferResources);
		}

		void VesselsRelocationButton()
		{
			if(selected_hangar == null) return;
			if(!selected_hangar.CanRelocate) return;
			if(GUILayout.Button("<< Relocate Vessels >>", Styles.yellow_button, GUILayout.ExpandWidth(true)))
			{
				selected_window.Toggle(TransferWindows.RelocateVessels);
				if(!selected_window[TransferWindows.RelocateVessels]) vessels_window.ClearSelection();
			}
		}
		
		static void CloseButton()
		{
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if(GUILayout.Button("Close")) HideGUI();
			GUILayout.EndHorizontal();
		}
		
		//info labels
		void HangarVesselInfo()
		{
			GUILayout.BeginVertical(Styles.white);
			GUILayout.Label(string.Format("Vessel crew: {0}/{1}", selected_hangar.vessel.GetCrewCount(), 
				selected_hangar.vessel.GetCrewCapacity()), GUILayout.ExpandWidth(true));
			GUILayout.Label("Vessel Volume: "+Utils.formatVolume(vessel_metric.volume), GUILayout.ExpandWidth(true));
			GUILayout.Label("Vessel Size: "+Utils.formatDimensions(vessel_metric.size), GUILayout.ExpandWidth(true));
			GUILayout.Label(string.Format("Mass: {0} stored, {1} total", Utils.formatMass(selected_hangar.TotalStoredMass), 
				Utils.formatMass(selected_hangar.vessel.GetTotalMass())), GUILayout.ExpandWidth(true));
			GUILayout.EndVertical();
		}

		void StorageInfo()
		{
			if(selected_hangar.ConnectedStorage.Count < 2) return;
			GUILayout.BeginVertical(Styles.white);
			GUILayout.Label("Total Vessels Docked: "+selected_hangar.TotalVesselsDocked, GUILayout.ExpandWidth(true));
			GUILayout.Label("Total Storage Volume: "+Utils.formatVolume(selected_hangar.TotalVolume), GUILayout.ExpandWidth(true));
			if(GUILayout.Toggle(highlight_storage == HighlightState.Enable, "Highlight Storage Parts")) highlight_storage = HighlightState.Enable;
			else if(highlight_storage == HighlightState.Enable) highlight_storage = HighlightState.Disable;
			HangarGUI.UsedVolumeLabel(selected_hangar.TotalUsedVolume, selected_hangar.TotalUsedVolumeFrac, "Total Used Volume");
			GUILayout.EndVertical();
		}

		void HangarInfo()
		{
			GUILayout.BeginVertical(Styles.white);
			GUILayout.Label("Volume: "+Utils.formatVolume(selected_hangar.Volume), GUILayout.ExpandWidth(true));
			GUILayout.Label("Dock Size: "+Utils.formatDimensions(selected_hangar.DockSize), GUILayout.ExpandWidth(true));
			GUILayout.Label("Vessels Docked: "+selected_hangar.VesselsDocked, GUILayout.ExpandWidth(true));
			HangarGUI.UsedVolumeLabel(selected_hangar.UsedVolume, selected_hangar.UsedVolumeFrac);
			GUILayout.EndVertical();
		}

		//Hangar selection list
		void SelectHangar_start() 
		{ 
			if(hangars.Count < 2) return;
			hangar_list.styleListBox  = Styles.list_box;
			hangar_list.styleListItem = Styles.list_item;
			hangar_list.windowRect    = fWindowPos;
			hangar_list.DrawBlockingSelector(); 
		}

		void Select_Hangar(HangarMachinery hangar)
		{
			if(selected_hangar != hangar)
			{
				if(highlight_hangar == HighlightState.Enable)
				{
					highlight_hangar = HighlightState.Disable;
					UpdateGUIState();
					highlight_hangar = HighlightState.Enable;
				}
				selected_hangar = hangar;
				hangar_id = hangar.part.flightID;
				hangar_list.SelectItem(hangars.IndexOf(hangar));
				BuildVesselList(selected_hangar);
			}
		}

		void SelectHangar()
		{
			if(hangars.Count < 2) return;
			GUILayout.BeginHorizontal();
			hangar_list.DrawButton();
			if(GUILayout.Toggle(highlight_hangar == HighlightState.Enable, "Highlight Hangar")) highlight_hangar = HighlightState.Enable;
			else if(highlight_hangar == HighlightState.Enable) highlight_hangar = HighlightState.Disable;
			Select_Hangar(hangars[hangar_list.SelectedIndex]);
			GUILayout.EndHorizontal();
		}
		public static void SelectHangar(HangarMachinery hangar) { instance.Select_Hangar(hangar); }

		void SelectHangar_end()
		{
			if(hangars.Count < 2) return;
			hangar_list.DrawDropDown();
			hangar_list.CloseOnOutsideClick();
		}
		
		
		//Vessel selection list
		void SelectVessel_start() 
		{ 
			vessel_list.styleListBox  = Styles.list_box;
			vessel_list.styleListItem = Styles.list_item;
			vessel_list.windowRect    = fWindowPos;
			vessel_list.DrawBlockingSelector(); 
		}

		void Select_Vessel(StoredVessel vsl)
		{
			vessel_id = vsl.proto_vessel.vesselID;
			vessel_list.SelectItem(vessels.IndexOf(vsl));
			if(vsl != selected_vessel) 
				selected_hangar.resourceTransferList.Clear();
			selected_vessel = vsl;
		}

		void SelectVessel()
		{
			GUILayout.BeginHorizontal();
			vessel_list.DrawButton();
			Select_Vessel(vessels[vessel_list.SelectedIndex]);
			GUILayout.EndHorizontal();
		}
		public static void SelectVessel(StoredVessel vsl) { instance.Select_Vessel(vsl); }

		void SelectVessel_end()
		{
			vessel_list.DrawDropDown();
			vessel_list.CloseOnOutsideClick();
		}
		
		//vessel info GUI
		void VesselInfo(int windowID)
		{ 
			GUILayout.BeginVertical();
			GUILayout.Label(string.Format("Mass: {0}   Volume: {1}", 
			                              Utils.formatMass(vessel_metric.mass), 
			                              Utils.formatVolume(vessel_metric.volume)), GUILayout.ExpandWidth(true));
			GUILayout.Label("Size: "+Utils.formatDimensions(vessel_metric.size), GUILayout.ExpandWidth(true));
			GUILayout.Label(String.Format("Crew Capacity: {0}", vessel_metric.CrewCapacity), GUILayout.ExpandWidth(true));
			if(HighLogic.LoadedSceneIsEditor)
			{
				if(GUILayout.Toggle(draw_directions, "Show Directions")) 
				{
					draw_directions = true;
					GUILayout.BeginHorizontal();
					GUILayout.Label("Green", Styles.green);
					GUILayout.FlexibleSpace();
					GUILayout.Label("Vessel's Forward");
					GUILayout.EndHorizontal();
					GUILayout.BeginHorizontal();
					GUILayout.Label("Blue", Styles.blue);
					GUILayout.FlexibleSpace();
					GUILayout.Label("Vessel's Bottom");
					GUILayout.EndHorizontal();
					GUILayout.Label("If there are hangars in the vessel with strict launch positioning, " +
									"additional sets of arrows show orientation in which a vessel will be launched " +
					                "from each of such hangars", Styles.label,
					                GUILayout.MaxWidth(eWindowPos.width-40), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
				}
				else draw_directions = false;
			}
			GUILayout.EndVertical();
			GUI.DragWindow(new Rect(0, 0, Screen.width, 20));
		}
		
		//hangar controls GUI
		void HangarCotrols(int windowID)
		{
			//hangar list
			SelectHangar_start();
			GUILayout.BeginVertical();
			HangarVesselInfo();
			StorageInfo();
			VesselsRelocationButton();
			SelectHangar();
			HangarInfo();
			GUILayout.EndVertical();
			GUILayout.BeginHorizontal();
			ToggleGatesButton();
			ToggleStateButton();
			GUILayout.EndHorizontal();
			if(vessels.Count > 0)
			{
				SelectVessel_start();
				GUILayout.BeginVertical();
				SelectVessel();
				GUILayout.EndVertical();
				CrewTransferButton();
				ResourcesTransferButton();
				LaunchButton();
			}
			CloseButton();
			SelectVessel_end();
			SelectHangar_end();
			GUI.DragWindow(new Rect(0, 0, Screen.width, 20));
		}
		#endregion
	
		override public void OnGUI()
		{
			if(vessel_metric.Empty) return;
			if(Event.current.type != EventType.Layout) return;
			base.OnGUI();
			if(hangars.Count > 0 && !selected_hangar.vessel.packed && selected_hangar.IsControllable && !selected_hangar.NoGUI)
			{
				//controls
				string hstate = selected_hangar.hangar_state.ToString();
				string gstate = selected_hangar.gates_state.ToString();
				fWindowPos = GUILayout.Window(GetInstanceID(),
											 fWindowPos, HangarCotrols,
											 String.Format("{0} {1}, Gates {2}", "Hangar", hstate, gstate),
										 	 GUILayout.Width(320),
											 GUILayout.Height(100));
				HangarGUI.CheckRect(ref fWindowPos);
				//transfers
				if(selected_vessel == null) 
				{
					selected_window[TransferWindows.SelectCrew] = false;
					selected_window[TransferWindows.TransferResources] = false;
				}
				if(selected_window[TransferWindows.SelectCrew])
				{
					cWindowPos = crew_window.Draw(selected_hangar.vessel.GetVesselCrew(), 
				    	                          selected_vessel.crew, selected_vessel.CrewCapacity, cWindowPos);
					HangarGUI.CheckRect(ref cWindowPos);
				}
				else if(selected_window[TransferWindows.TransferResources])
				{
					selected_hangar.prepareResourceList(selected_vessel);
					rWindowPos = resources_window.Draw(selected_hangar.resourceTransferList, rWindowPos);
					HangarGUI.CheckRect(ref rWindowPos);
					if(resources_window.transferNow)
					{
						selected_hangar.transferResources(selected_vessel);
						resources_window.transferNow = false;
						selected_window[TransferWindows.TransferResources] = false;
					}
				}
				else if(selected_window[TransferWindows.RelocateVessels])
				{
					vWindowPos = vessels_window.Draw(selected_hangar.ConnectedStorage, vWindowPos);
					HangarGUI.CheckRect(ref vWindowPos);
					vessels_window.TransferVessel();
					if(vessels_window.Closed)
					{
						vessels_window.ClearSelection();
						selected_window[TransferWindows.RelocateVessels] = false;
					}
				}
			}
			else
			{
				Utils.LockIfMouseOver(eLock, eWindowPos, enabled);
				eWindowPos = GUILayout.Window(GetInstanceID(),
											  eWindowPos, VesselInfo,
											  "Vessel info",
											  GUILayout.Width(300),
											  GUILayout.Height(100));
				HangarGUI.CheckRect(ref eWindowPos);
			}
			UpdateGUIState();
		}

		public override void Update()
		{
			base.Update();
			if(HighLogic.LoadedSceneIsEditor && draw_directions && !vessel_metric.Empty)
			{
				List<Part> parts;
				try { parts = EditorLogic.SortedShipList; }
				catch (NullReferenceException) { parts = null; }
				if(parts != null && parts.Count > 0 && parts[0] != null)
					HangarGUI.DrawYZ(vessel_metric, parts[0].partTransform);
				foreach(Part p in parts.Where(p => p.HasModule<HangarMachinery>()))
				{
					var h = p.GetComponent<HangarMachinery>();
					if(h == null) continue;
					var t = h.GetSpawnTransform();
					if(t != null)
						HangarGUI.DrawYZ(h.PartMetric, t);
				}
			}
			#if DEBUG
			DrawPoints();
			#endif
		}

		#if DEBUG
		void DrawPoints()
		{
			if(vessel_metric.Empty) return;
			if(HighLogic.LoadedSceneIsEditor)
			{
				List<Part> parts;
				try { parts = EditorLogic.SortedShipList; }
				catch (NullReferenceException) { return; }
				if(parts.Count == 0 || parts[0] == null) return;
				vessel_metric.DrawCenter(parts[0].partTransform);
//				HangarGUI.DrawHull(vessel_metric, parts[0].partTransform);
			}
			else 
			{
				vessel_metric.DrawCenter(FlightGlobals.ActiveVessel.vesselTransform);
				HangarGUI.DrawPoint(FlightGlobals.ActiveVessel.vesselTransform.InverseTransformPoint(FlightGlobals.ActiveVessel.CurrentCoM), 
								 FlightGlobals.ActiveVessel.vesselTransform, Color.green);
				HangarGUI.DrawPoint(Vector3.zero, 
								 FlightGlobals.ActiveVessel.vesselTransform, Color.red);
			}
		}
		#endif
	}
}

