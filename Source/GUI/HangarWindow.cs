//   HangarWindow.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

//This code is partly based on the code from Extraplanetary Launchpads plugin. BuildWindow.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public class HangarWindow : AddonWindowBase<HangarWindow>
    {
        //update parameters
        float next_update = 0;
        const float update_interval = 0.5f;

        enum HighlightState {  None, Enable, Disable }
        //settings
        static HighlightState highlight_hangar, highlight_storage;
        static bool draw_directions;

        //this vessel
        [ConfigOption] Rect InfoWindow;
        static Vessel vessel;
        Metric vessel_metric;

        //hangars
        string hangars_tooltip = string.Empty;
        readonly List<HangarMachinery> hangars = new List<HangarMachinery>();
        HangarMachinery selected_hangar;
        [ConfigOption] uint hangar_id;

        //vessels
        string vessels_tooltip = string.Empty;
        List<PackedVessel> vessels = new List<PackedVessel>();
        PackedVessel selected_vessel;
        [ConfigOption] Guid vessel_id;

        //vessel relocation, crew and resources transfers
        #pragma warning disable CS0649
        CrewTransferWindow crew_window;
        ResourceTransferWindow resources_window;
        VesselTransferWindow vessels_window;
        #pragma warning restore CS0649

        //vessel volume 
        void update_vessel_metric(Vessel vsl = null)
        {
            vessel_metric.Clear();
            if(vsl != null) 
            {
                if(vsl.loaded && vsl.parts.Count > 0)
                    vessel_metric = new Metric(vsl);
            }
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
        void build_hangar_list(Vessel vsl)
        {
            //save selected hangar
            HangarMachinery prev_selected = selected_hangar;
            //reset state
            hangars.Clear();
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
                    hangar_names.Add(string.Format("{0} {1}\n", i+1, h_name));
                }
                hangars_tooltip = string.Concat(hangar_names.ToArray());
            }
            //clear highlight of previously selected hangar
            if(highlight_hangar == HighlightState.Disable && prev_selected != null && selected_hangar != prev_selected)
                prev_selected.part.SetHighlightDefault();
        }

        //build dropdown list of stored vessels
        void build_vessel_list(HangarMachinery hangar)
        {
            //reset stat
            vessels.Clear();
            select_vessel(null);
            //check hangar
            if(hangar == null) return;
            //build new list
            vessels = hangar.GetVessels();
            if(vessels.Count == 0) return ;
            select_vessel(vessels.Find(v => v.id == vessel_id));
            if(selected_vessel == null) select_vessel(vessels[0]);
            var vessel_names = new List<string>();
            for(int i = 0; i < vessels.Count; i++)
                vessel_names.Add(string.Format("{0} {1}\n", i+1, vessels[i].name));
            vessels_tooltip = string.Concat(vessel_names.ToArray());
        }

        void update_lists()
        {
            update_vessel_metric(vessel);
            build_hangar_list(vessel);
            build_vessel_list(selected_hangar);
        }

        void highlight_parts()
        {
            if(selected_hangar != null) 
            {
                //first highlight storage
                if(selected_hangar.ConnectedStorage.Count > 1)
                {
                    if(doShow && highlight_storage == HighlightState.Enable) 
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
                if(doShow && highlight_hangar == HighlightState.Enable) 
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

        protected override void update_content()
        {
            base.update_content();
            update_vessel_metric(vessel);
            build_hangar_list(vessel);
            highlight_parts();
            if(selected_hangar != null)
            {
                //vessel list
                if(vessels.Count != selected_hangar.VesselsDocked)
                    build_vessel_list(selected_hangar);
                if(selected_vessel != null && resources_window.WindowEnabled)
                    selected_hangar.UpdateResourceList();
            }
        }

        public override void Awake()
        {
            base.Awake();
            next_update = Time.time; 
            GameEvents.onVesselChange.Add(onVesselChange);
            GameEvents.onVesselWasModified.Add(onVesselWasModified);
        }

        public override void OnDestroy()
        {
            UnlockControls();
            GameEvents.onVesselChange.Remove(onVesselChange);
            GameEvents.onVesselWasModified.Remove(onVesselWasModified);
            base.OnDestroy();
        }

        void onVesselChange(Vessel vsl)
        {
            vessel = null;
            hangars.Clear();
            vessels.Clear();
            vessel_metric.Clear();
            subwindows.ForEach(sw => sw.Show(false));
            if(vsl.isEVA) return;
            vessel = vsl;
            update_lists();
            highlight_parts();
        }

        void onVesselWasModified(Vessel vsl)
        { if(vsl != null && vessel == vsl) update_lists(); }


        public void Update() 
        { 
            if(Time.time > next_update)
            {
                if(window_enabled) update_content();
                next_update += update_interval;
            }
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
            if(GUILayout.Button("Launch Vessel", Styles.active_button, GUILayout.ExpandWidth(true)))
                selected_hangar.TryRestoreVessel(selected_vessel);
            GUILayout.EndHorizontal();
        }

        void ToggleGatesButton()
        {
            switch(selected_hangar.gates_state)
            {
            case AnimatorState.Closed:
            case AnimatorState.Closing:
                if(GUILayout.Button("Open Gates", Styles.open_button, GUILayout.ExpandWidth(true)))
                    selected_hangar.Open();
                break;
            case AnimatorState.Opened:
            case AnimatorState.Opening:
                if(GUILayout.Button("Close Gates", Styles.close_button, GUILayout.ExpandWidth(true)))
                    selected_hangar.Close();
                break;
            }
        }

        void ToggleStateButton()
        {
            if(selected_hangar.hangar_state == HangarMachinery.HangarState.Inactive)
            {
                if(GUILayout.Button("Activate Hangar", Styles.open_button, GUILayout.ExpandWidth(true)))
                    selected_hangar.Activate();
            }
            else
            {
                if(GUILayout.Button("Deactivate Hangar", Styles.close_button, GUILayout.ExpandWidth(true)))
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
                crew_window.Toggle();
        }

        void ResourcesTransferButton()
        {
            if(selected_hangar.NoResourceTransfers ||
               selected_vessel == null) return;
            if(GUILayout.Button("Transfer Resources", GUILayout.ExpandWidth(true)))
                resources_window.Toggle();
        }

        void VesselsRelocationButton()
        {
            if(selected_hangar == null) return;
            if(!selected_hangar.CanRelocate) return;
            if(GUILayout.Button("<< Relocate Vessels >>", Styles.active_button, GUILayout.ExpandWidth(true)))
                vessels_window.Toggle();
        }

        static void CloseButton()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("Close"))
                ShowInstance(false);
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

        void select_hangar(HangarMachinery hangar)
        {
            if(selected_hangar != hangar)
            {
                if(highlight_hangar == HighlightState.Enable)
                {
                    highlight_hangar = HighlightState.Disable;
                    highlight_parts();
                    highlight_hangar = HighlightState.Enable;
                }
                selected_hangar = hangar;
                hangar_id = hangar.part.flightID;
                build_vessel_list(selected_hangar);
            }
        }

        void SelectHangar()
        {
            if(hangars.Count < 2) return;
            GUILayout.BeginHorizontal();
            var next_hangar = Utils.LeftRightChooser(selected_hangar, hangars, hangars_tooltip);
            if(GUILayout.Toggle(highlight_hangar == HighlightState.Enable, "Highlight Hangar")) highlight_hangar = HighlightState.Enable;
            else if(highlight_hangar == HighlightState.Enable) highlight_hangar = HighlightState.Disable;
            GUILayout.EndHorizontal();
            select_hangar(next_hangar);
        }
        public static void SelectHangar(HangarMachinery hangar) { Instance.select_hangar(hangar); }

        //Vessel selection list
        void select_vessel(PackedVessel vsl)
        {
            if(vsl != null)
            {
                vessel_id = vsl.id;
                if(vsl != selected_vessel) 
                {
                    selected_hangar.ResourceTransferList.Clear();
                    resources_window.TransferAction = () => selected_hangar.TransferResources(vsl);
                    selected_hangar.SetHighlightedContent(null);
                }
            }
            else
            {
                vessel_id = Guid.Empty;
                resources_window.TransferAction = null;
            }
            selected_vessel = vsl;
        }

        static readonly GUIContent show_button = new GUIContent("Show", "Show current payload for a short time");
        void SelectVessel()
        {
            GUILayout.BeginHorizontal();
            var next_vessel = Utils.LeftRightChooser(selected_vessel, vessels, vessels_tooltip);
            if(GUILayout.Button(show_button, Styles.active_button, GUILayout.ExpandWidth(false)))
                selected_hangar.HighlightContentTemporary(next_vessel, 5);
            select_vessel(next_vessel);
            GUILayout.EndHorizontal();
        }
        public static void SelectVessel(PackedVessel vsl) { Instance.select_vessel(vsl); }

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
                                    GUILayout.MaxWidth(InfoWindow.width-40), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                }
                else draw_directions = false;
            }
            GUILayout.EndVertical();
            TooltipsAndDragWindow();
        }

        //hangar controls GUI
        void HangarCotrols(int windowID)
        {
            //hangar list
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
                SelectVessel();
                selected_hangar.DrawSpawnRotationControls(selected_vessel);
                CrewTransferButton();
                ResourcesTransferButton();
                LaunchButton();
            }
            CloseButton();
            TooltipsAndDragWindow();
        }
        #endregion

        public override void UnlockControls()
        {
            base.UnlockControls();
            Utils.LockIfMouseOver(LockName, InfoWindow, false);
        }
        protected override bool can_draw() { return Time.timeSinceLevelLoad > 3 && !vessel_metric.Empty; }
        protected override void draw_gui()
        {
            if(vessel != null && !vessel.packed && hangars.Count > 0 && selected_hangar.IsControllable && !selected_hangar.NoGUI)
            {
                //controls
                LockControls();
                string hstate = selected_hangar.hangar_state.ToString();
                string gstate = selected_hangar.gates_state.ToString();
                WindowPos = GUILayout.Window(GetInstanceID(),
                                             WindowPos, HangarCotrols,
                                             string.Format("{0} {1}, Gates {2}", "Hangar", hstate, gstate),
                                             GUILayout.Width(320),
                                             GUILayout.Height(100)).clampToScreen();
                //transfers
                if(selected_vessel != null)
                {
                    //crew transfer
                    crew_window.Draw(selected_hangar.vessel.GetVesselCrew(), 
                                     selected_vessel.crew, selected_vessel.CrewCapacity);
                    //resource transfer
                    selected_hangar.PrepareResourceList(selected_vessel);
                    resources_window.Draw(string.Format("Transfer between: \"{0}\" \"{1}\"", 
                                                        selected_hangar.HangarName, selected_vessel.name), 
                                          selected_hangar.ResourceTransferList);
                }
                //vessel transfer
                vessels_window.Draw(selected_hangar.ConnectedStorage);
                vessels_window.TransferVessel();
            }
            else
            {
                Utils.LockIfMouseOver(LockName, InfoWindow);
                InfoWindow = GUILayout.Window(GetInstanceID(),
                                              InfoWindow, VesselInfo,
                                              "Vessel info",
                                              GUILayout.Width(300),
                                              GUILayout.Height(100)).clampToScreen();
            }
            highlight_parts();
        }

        void OnRenderObject()
        {
            if(HighLogic.LoadedSceneIsEditor && draw_directions && !vessel_metric.Empty)
            {
                List<Part> parts;
                try { parts = EditorLogic.SortedShipList; }
                catch (NullReferenceException) { parts = null; }
                if(parts != null && parts.Count > 0 && parts[0] != null)
                    HangarGUI.DrawYZ(vessel_metric, parts[0].partTransform);
                for(int i = 0, partsCount = parts.Count; i < partsCount; i++)
                {
                    Part p = parts[i];
                    var h = p.Modules.GetModule<HangarMachinery>();
                    if(h == null) continue;
                    var t = h.GetSpawnTransform();
                    if(t != null)
                    {
                        if(h.Storage != null && h.Storage.AutoPositionVessel)
                            Utils.GLDrawPoint(t.position, Color.green);
                        else
                            HangarGUI.DrawYZ(h.PartMetric, t);
                    }
                }
            }
            #if DEBUG
//            DrawBounds();
//            DrawPoints();
            #endif
        }

        #if DEBUG
        void DrawBounds()
        {
            if(vessel_metric.Empty) return;
            if(EditorLogic.fetch != null)
            {
                var parts = EditorLogic.fetch.getSortedShipList();
                if(parts.Count == 0 || parts[0] == null) return;
                vessel_metric.DrawBox(parts[0].partTransform);
                if(vessel_metric.hull != null && draw_directions)
                    Utils.GLDrawHull(vessel_metric.hull, parts[0].partTransform, c:Color.yellow);
            }
            //            else vessel_metric.DrawBox(FlightGlobals.ActiveVessel.vesselTransform);
        }

        void DrawPoints()
        {
            if(vessel_metric.Empty) return;
            if(EditorLogic.fetch != null)
            {
                var parts = EditorLogic.fetch.getSortedShipList();
                if(parts.Count == 0 || parts[0] == null) return;
                vessel_metric.DrawCenter(parts[0].partTransform);
            }
            else if(vessel != null)
            {
                vessel_metric.DrawCenter(vessel.vesselTransform);
                Utils.GLDrawPoint(vessel.transform.position, Color.red);
                Utils.GLDrawPoint(vessel.CoM, Color.green);
                Utils.GLLine(vessel.transform.position, vessel.CoM, Color.green);
            }
        }
        #endif
    }
}

