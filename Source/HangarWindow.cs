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
		//settings
		static int  highlight_hangar = 0; //1 -- enable highlight; -1 -- disable highlight; 0 -- do not set highlight (for mouseover highlighting).
		static bool draw_directions = false;
		static bool selecting_crew = false;
		static bool transfering_resources = false;
		static Rect fWindowPos = new Rect();
		static Rect eWindowPos = new Rect();
		static Rect cWindowPos = new Rect();
		static Rect rWindowPos = new Rect();

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
		readonly List<Hangar> hangars = new List<Hangar>();
		readonly DropDownList hangar_list = new DropDownList();
		Hangar selected_hangar;
		uint hangar_id;
		
		//vessels
		List<StoredVessel> vessels = new List<StoredVessel>();
		readonly DropDownList vessel_list = new DropDownList();
		StoredVessel selected_vessel;
		Guid vessel_id;
		
		//vessel crew and resources
		CrewTransferWindow crew_window = new CrewTransferWindow();
		ResourceTransferWindow resources_window = new ResourceTransferWindow();
		
		
		//vessel volume 
		void updateVesselMetrics(Vessel vsl = null)
		{
			vessel_metric = null;
			if(vsl != null) vessel_metric = new Metric(vsl);
			else if(EditorLogic.fetch != null)
			{
				List<Part> parts = new List<Part>();
				try { parts = EditorLogic.SortedShipList; }
				catch (NullReferenceException) { return; }
				if(parts.Count > 0 && parts[0] != null)
					vessel_metric = new Metric(parts);
			}
		}
		
		//build dropdown list of all hangars in the vessel
		void BuildHangarList(Vessel vsl)
		{
			//reset state
			if(selected_hangar != null)	
				selected_hangar.part.SetHighlightDefault();
			hangars.Clear();
			hangar_list.Items.Clear();
			selected_hangar = null;
			//check the vessel
			if(vsl == null) return;
			//build new list
			foreach(var p in vsl.Parts)
				hangars.AddRange(p.Modules.OfType<Hangar>().Where(h => h.enabled));
			if(hangars.Count > 0) 
			{
				selected_hangar = hangars.Find(h => h.part.flightID == hangar_id);
				if(selected_hangar == null) selected_hangar = hangars[0];
				var hangar_names = new List<string>();
				for(int i = 0; i < hangars.Count; i++)
				{
					string h_name = hangars[i].HangarName == default(string) ? "Unnamed Hangar" : hangars[i].HangarName;
					hangar_names.Add(string.Format("{0} {1}", i+1, h_name));
				}
				hangar_list.Items = hangar_names;
				hangar_list.SelectItem(hangars.IndexOf(selected_hangar));
			}
		}
		
		//build dropdown list of stored vessels
		void BuildVesselList(Hangar hangar)
		{
			//reset stat
			vessels.Clear();
			vessel_list.Items.Clear();
			selected_vessel = null;
			//check hangar
			if(hangar == null) return;
			//build new list
			vessels = hangar.GetVessels();
			if(vessels.Count > 0) 
			{
				selected_vessel = vessels.Find(v => v.vessel.vesselID == vessel_id);
				if(selected_vessel == null) selected_vessel = vessels[0];
				var vessel_names = new List<string>();
				for(int i = 0; i < vessels.Count; i++)
					vessel_names.Add(string.Format("{0} {1}", i+1, vessels[i].vessel.vesselName));
				vessel_list.Items = vessel_names;
				vessel_list.SelectItem(vessels.IndexOf(selected_vessel));
			}
		}
		
		//update-init-destroy
		void onVesselChange(Vessel vsl)
		{
			updateVesselMetrics(vsl);
			BuildHangarList(vsl);
			BuildVesselList(selected_hangar);
			UpdateGUIState();
		}

		void onVesselWasModified(Vessel vsl)
		{ 
			if(FlightGlobals.ActiveVessel == vsl) 
			{
				updateVesselMetrics(vsl);
				BuildHangarList(vsl);
				BuildVesselList(selected_hangar);
			}
		}

		public override void OnUpdate() 
		{ 
			if(!enabled) return;
			Vessel vsl = current_vessel;
			updateVesselMetrics(vsl);
			BuildHangarList(vsl);
			UpdateGUIState();
			if(selected_hangar != null)
			{
				//vessel list
				if(vessels.Count != selected_hangar.numVessels())
					BuildVesselList(selected_hangar);
				if(selected_vessel != null && transfering_resources)
					selected_hangar.updateResourceList();
			}
		}
		
		override public void UpdateGUIState()
		{
			base.UpdateGUIState();
			if(!enabled) Utils.LockIfMouseOver(eLock, eWindowPos, false);
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
			foreach(var p in hangars)
				p.UpdateMenus(enabled && p == selected_hangar);
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
			base.LoadSettings ();
			fWindowPos = configfile.GetValue<Rect>(mangleName("fWindowPos"), fWindowPos);
			eWindowPos = configfile.GetValue<Rect>(mangleName("eWindowPos"), eWindowPos);
			cWindowPos = configfile.GetValue<Rect>(mangleName("cWindowPos"), cWindowPos);
			rWindowPos = configfile.GetValue<Rect>(mangleName("rWindowPos"), rWindowPos);
			hangar_id  = configfile.GetValue<uint>(mangleName("hangar_id"),  default(uint));
			vessel_id  = configfile.GetValue<Guid>(mangleName("vessel_id"),  Guid.Empty);
		}
		
		public override void SaveSettings()
		{
			configfile.SetValue(mangleName("fWindowPos"), fWindowPos);
			configfile.SetValue(mangleName("eWindowPos"), eWindowPos);
			configfile.SetValue(mangleName("cWindowPos"), cWindowPos);
			configfile.SetValue(mangleName("rWindowPos"), rWindowPos);
			configfile.SetValue(mangleName("hangar_id"), hangar_id);
			configfile.SetValue(mangleName("vessel_id"), vessel_id);
			base.SaveSettings();
		}
		
		#region GUI
		//buttons
		void LaunchButton()
		{
			if(GUILayout.Button("Launch Vessel", Styles.yellow_button, GUILayout.ExpandWidth(true)))
				selected_hangar.TryRestoreVessel(selected_vessel);
		}
		
		void ToggleGatesButton()
		{
			if(selected_hangar.gates_state == AnimatorState.Closed ||
			   selected_hangar.gates_state == AnimatorState.Closing)
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
			{
				selecting_crew = !selecting_crew;
				if(transfering_resources && selecting_crew)
					transfering_resources = false;
			}
		}
		
		void ResourcesTransferButton()
		{
			if(selected_vessel == null) return;
			if(GUILayout.Button("Transfer resources", GUILayout.ExpandWidth(true)))
			{
				transfering_resources = !transfering_resources;
				if(transfering_resources && selecting_crew)
					selecting_crew = false;
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
		void HangarInfo()
		{
			GUILayout.BeginVertical();
			GUILayout.Label("Vessel Volume: "+Utils.formatVolume(vessel_metric.volume), GUILayout.ExpandWidth(true));
			GUILayout.Label("Vessel Dimensions: "+Utils.formatDimensions(vessel_metric.size), GUILayout.ExpandWidth(true));
			GUILayout.Label("Hangar Dimensions: "+Utils.formatDimensions(selected_hangar.hangar_metric.size), GUILayout.ExpandWidth(true));
			GUILayout.Label("Hangar volume: "+Utils.formatVolume(selected_hangar.hangar_metric.volume), GUILayout.ExpandWidth(true));
			GUILayout.Label(string.Format("Used volume: {0}, {1:F1}%", Utils.formatVolume(selected_hangar.used_volume), selected_hangar.used_volume_frac*100), 
			                Styles.fracStyle(1-selected_hangar.used_volume_frac), GUILayout.ExpandWidth(true));
			GUILayout.Label(string.Format("Mass: {0} stored, {1} total", Utils.formatMass(selected_hangar.vessels_mass), 
			                              Utils.formatMass(selected_hangar.vessel.GetTotalMass())), GUILayout.ExpandWidth(true));
			GUILayout.Label("Vessels docked: "+selected_hangar.numVessels(), GUILayout.ExpandWidth(true));
			GUILayout.Label(string.Format("Vessel crew: {0}/{1}", selected_hangar.vessel.GetCrewCount(), 
			                              selected_hangar.vessel.GetCrewCapacity()), GUILayout.ExpandWidth(true));
			GUILayout.EndVertical();
		}
		
		
		//Hangar selection list
		void SelectHangar_start() 
		{ 
			if(hangars.Count < 2) return;
			hangar_list.styleListBox = Styles.list_box;
			hangar_list.styleListItem = Styles.list_item;
			hangar_list.windowRect = fWindowPos;
			hangar_list.DrawBlockingSelector(); 
		}

		void Select_Hangar(Hangar hangar)
		{
			if(selected_hangar != hangar)
			{
				if(highlight_hangar == 1)
				{
					highlight_hangar = -1;
					UpdateGUIState();
					highlight_hangar =  1;
				}
				selected_hangar = hangar;
				hangar_id = hangar.part.flightID;
				hangar_list.SelectItem(hangars.IndexOf(hangar));
			}
		}

		void SelectHangar()
		{
			if(hangars.Count < 2) return;
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
			if(hangars.Count < 2) return;
			hangar_list.DrawDropDown();
			hangar_list.CloseOnOutsideClick();
		}
		
		
		//Vessel selection list
		void SelectVessel_start() 
		{ 
			vessel_list.styleListBox = Styles.list_box;
			vessel_list.styleListItem = Styles.list_item;
			vessel_list.windowRect = fWindowPos;
			vessel_list.DrawBlockingSelector(); 
		}

		void Select_Vessel(StoredVessel vsl)
		{
			vessel_id = vsl.vessel.vesselID;
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
			GUILayout.Label("Dimensions: "+Utils.formatDimensions(vessel_metric.size), GUILayout.ExpandWidth(true));
			GUILayout.Label(String.Format("Crew Capacity: {0}", vessel_metric.CrewCapacity), GUILayout.ExpandWidth(true));
			if(HighLogic.LoadedScene == GameScenes.EDITOR ||
			   HighLogic.LoadedScene == GameScenes.SPH)
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
					GUILayout.Label("If there are hangars in the vessel, additional sets of arrows show orientation " +
									"in which a vessel will be launched from each of the hangars", GUILayout.ExpandWidth(true));
				}
				else draw_directions = false;
			}
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
			
			GUI.DragWindow(new Rect(0, 0, 5000, 20));
		}
		#endregion
	
		override public void OnGUI()
		{
			if(vessel_metric == null) return;
			if(Event.current.type != EventType.Layout) return;
			base.OnGUI();
			if(hangars.Count > 0)
			{
				//controls
				string hstate = selected_hangar.hangar_state.ToString();
				string gstate = selected_hangar.gates_state.ToString();
				fWindowPos = GUILayout.Window(GetInstanceID(),
											 fWindowPos, HangarCotrols,
											 String.Format("{0} {1}, Gates {2}", "Hangar", hstate, gstate),
										 	 GUILayout.Width(320),
											 GUILayout.Height(100));
				CheckRect(ref fWindowPos);
				//transfers
				if(selected_vessel == null) selecting_crew = transfering_resources = false;
				if(selecting_crew)
				{
					cWindowPos = crew_window.Draw(selected_hangar.vessel.GetVesselCrew(), 
				    	                          selected_vessel.crew, selected_vessel.CrewCapacity, cWindowPos);
					CheckRect(ref cWindowPos);
				}
				if(transfering_resources)
				{
					selected_hangar.prepareResourceList(selected_vessel);
					rWindowPos = resources_window.Draw(selected_hangar.resourceTransferList, rWindowPos);
					CheckRect(ref rWindowPos);
					if(resources_window.transferNow)
					{
						selected_hangar.transferResources(selected_vessel);
						resources_window.transferNow = false;
						transfering_resources = false;
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
				CheckRect(ref eWindowPos);
			}
			UpdateGUIState();
		}

		public override void Update()
		{
			base.Update();
			if(draw_directions && vessel_metric != null && 
				(HighLogic.LoadedScene == GameScenes.EDITOR ||
				 HighLogic.LoadedScene == GameScenes.SPH))
			{
				List<Part> parts;
				try { parts = EditorLogic.SortedShipList; }
				catch (NullReferenceException) { parts = null; }
				if(parts != null && parts.Count > 0 && parts[0] != null)
					Utils.DrawYZ(vessel_metric, parts[0].partTransform);
				foreach(Part p in parts.Where(p => p.HasModule<Hangar>()))
				{
					Hangar h = p.GetComponent<Hangar>();
					if(h != null)
						Utils.DrawYZ(h.part_metric, h.GetLaunchTransform());
				}
			}
			#if DEBUG
			DrawPoints();
			#endif
		}

		#if DEBUG
		void DrawPoints()
		{
			if(vessel_metric == null) return;
			if(EditorLogic.fetch != null)
			{
				List<Part> parts;
				try { parts = EditorLogic.SortedShipList; }
				catch (NullReferenceException) { return; }
				if(parts.Count == 0 || parts[0] == null) return;
				vessel_metric.DrawCenter(parts[0].partTransform);
			}
			else 
			{
				vessel_metric.DrawCenter(FlightGlobals.ActiveVessel.vesselTransform);
				Utils.DrawPoint(FlightGlobals.ActiveVessel.findLocalCenterOfMass(), 
								 FlightGlobals.ActiveVessel.vesselTransform, Color.green);
				Utils.DrawPoint(Vector3.zero, 
								 FlightGlobals.ActiveVessel.vesselTransform, Color.red);
			}
		}
		#endif
	}
}

