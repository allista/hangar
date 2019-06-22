//   HangarMachineryEditor.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using UnityEngine;
using AT_Utils;
using AT_Utils.UI;
using System.Collections;

namespace AtHangar
{
    public partial class HangarMachinery
    {
        #region In-Editor Content Management
        const int window_width = 400;
        const string eLock  = "Hangar.EditHangar";
        const string scLock = "Hangar.LoadShipConstruct";

        Vector2 constructs_scroll = Vector2.zero;
        Vector2 unfit_scroll = Vector2.zero;
        Rect eWindowPos  = new Rect(Screen.width/2-window_width/2, 100, window_width, 100);

        bool editing_content;
        MeshRenderer content_hull_renderer;
        MeshFilter content_hull_mesh;
        PackedVessel highlighted_content;
        SimpleTextEntry hangar_name_editor;
        VesselTransferWindow vessels_window;
        ShipConstructLoader construct_loader;
        EditorFacility facility;

        void highlight_fitted_content(PackedVessel pc)
        {
            if(pc == highlighted_content && HighLogic.LoadedSceneIsEditor)
                SetHighlightedContent(highlighted_content, true);
        }

        void highlight_unfitted_content(PackedVessel pc)
        {
            if(pc == highlighted_content && HighLogic.LoadedSceneIsEditor)
                SetHighlightedContent(highlighted_content, false);
        }

        public void SetHighlightedContent(PackedVessel pc, bool fits=false)
        {
            highlighted_content = pc;
            content_hull_mesh.gameObject.SetActive(false);
            if(highlighted_content != null)
            {
                var mesh = highlighted_content.metric.hull_mesh;
                if(mesh != null)
                {
                    content_hull_mesh.mesh = mesh;
                    content_hull_renderer.material.color = fits? Colors.Good.Alpha(0.25f) : Colors.Danger.Alpha(0.25f);
                    update_content_hull_mesh();
                }
            }
            else
                stop_at_time = -1;
        }

        void update_content_hull_mesh()
        {
            Vector3 offset;
            var spawn_transform = get_spawn_transform(highlighted_content, out offset);
            offset -= highlighted_content.metric.center;
            content_hull_mesh.transform.position = spawn_transform.position;
            content_hull_mesh.transform.rotation = spawn_transform.rotation;
            content_hull_mesh.transform.Translate(offset);
            content_hull_mesh.gameObject.SetActive(true);
        }

        float stop_at_time = -1;
        IEnumerator stop_highlighting_content_after(float period)
        {
            stop_at_time = Time.time + period;
            do
            {
                yield return new WaitForSeconds(stop_at_time - Time.time);
            } while(stop_at_time-Time.time > 0);
            if(stop_at_time > 0)
                SetHighlightedContent(null);
            stop_at_time = -1;
        }

        public void HighlightContentTemporary(PackedVessel pc, float period, bool fits = true)
        {
            if(highlighted_content == null)
            {
                SetHighlightedContent(pc, fits);
                StartCoroutine(stop_highlighting_content_after(period));
            }
            else
            {
                stop_at_time = Time.time + period;
                SetHighlightedContent(pc, fits);
            }
        }

        void process_construct(ShipConstruct construct)
        {
            var pc = new PackedConstruct(construct, HighLogic.CurrentGame.flagURL);
            //check if the construct contains launch clamps
            if(pc.construct.HasLaunchClamp())
            {
                Utils.Message("\"{0}\" has launch clamps. Remove them before storing.", pc.name);
                pc.UnloadConstruct();
                return;
            }
            pc.UpdateMetric();
            try_store_vessel(pc);
            SetHighlightedContent(pc, Storage.Contains(pc));
            pc.UnloadConstruct();
        }

        void hangar_content_editor(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            //Vessel selector
            if(GUILayout.Button("Select Vessel", Styles.normal_button, GUILayout.ExpandWidth(true))) 
                construct_loader.SelectVessel();
            if(GUILayout.Button("Select Subassembly", Styles.normal_button, GUILayout.ExpandWidth(true)))
                construct_loader.SelectSubassembly();
            if(GUILayout.Button("Select Part", Styles.normal_button, GUILayout.ExpandWidth(true)))
                construct_loader.SelectPart(part.flagURL);
            GUILayout.EndHorizontal();
            //hangar info
            if(ConnectedStorage.Count > 1)
                HangarGUI.UsedVolumeLabel(TotalUsedVolume, TotalUsedVolumeFrac, "Total Used Volume");
            HangarGUI.UsedVolumeLabel(UsedVolume, UsedVolumeFrac);
            //hangar contents
            var vessels = Storage.GetVessels();
            vessels.Sort((a, b) => a.name.CompareTo(b.name));
            constructs_scroll = GUILayout.BeginScrollView(constructs_scroll, GUILayout.Height(200), GUILayout.Width(window_width));
            GUILayout.BeginVertical();
            foreach(PackedVessel pv in vessels)
            {
                GUILayout.BeginHorizontal();
                if(HangarGUI.PackedVesselLabel(pv, pv == highlighted_content? Styles.white : Styles.label))
                {
                    if(highlighted_content != pv)
                        SetHighlightedContent(pv, true);
                    else
                        SetHighlightedContent(null);
                }
                if(GUILayout.Button("+1", Styles.open_button, GUILayout.Width(25)))
                {
                    if(pv is PackedConstruct pc)
                        try_store_vessel(pc.Clone());
                }
                if(GUILayout.Button("X", Styles.danger_button, GUILayout.Width(25))) 
                {
                    if(pv == highlighted_content)
                        SetHighlightedContent(null);
                    Storage.RemoveVessel(pv);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            //unfit constructs
            var constructs = Storage.UnfitConstucts;
            if(constructs.Count > 0)
            {
                GUILayout.Label("Unfit vessels:", Styles.active, GUILayout.ExpandWidth(true));
                unfit_scroll = GUILayout.BeginScrollView(unfit_scroll, GUILayout.Height(100), GUILayout.Width(window_width));
                GUILayout.BeginVertical();
                foreach(PackedConstruct pc in Storage.UnfitConstucts)
                {
                    GUILayout.BeginHorizontal();
                    if(HangarGUI.PackedVesselLabel(pc, pc == highlighted_content? Styles.white : Styles.label))
                    {
                        if(highlighted_content != pc)
                            SetHighlightedContent(pc, false);
                        else
                            SetHighlightedContent(null);
                    }
                    if(GUILayout.Button("^", Styles.open_button, GUILayout.Width(25))) 
                    { if(try_store_vessel(pc.Clone())) Storage.RemoveUnfit(pc); }
                    if(GUILayout.Button("X", Styles.danger_button, GUILayout.Width(25))) 
                    {
                        if(pc == highlighted_content)
                            SetHighlightedContent(null);
                        Storage.RemoveUnfit(pc);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
                GUILayout.EndScrollView();
            }
            //common buttons
            if(GUILayout.Button("Clear", Styles.danger_button, GUILayout.ExpandWidth(true)))
            {
                Storage.ClearConstructs();
                SetHighlightedContent(null);
            }
            if(GUILayout.Button("Close", Styles.normal_button, GUILayout.ExpandWidth(true))) 
            {
                Utils.LockControls(eLock, false);
                SetHighlightedContent(null);
                editing_content = false;
            }
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, Screen.width, 20));
        }

        public void OnGUI() 
        { 
            if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
            Styles.Init();
            //edit hangar
            if(editing_content)
            {
                Utils.LockIfMouseOver(eLock, eWindowPos);
                eWindowPos = GUILayout.Window(GetInstanceID(), eWindowPos,
                                              hangar_content_editor,
                                              "Hangar Contents Editor",
                                              GUILayout.Width(window_width),
                                              GUILayout.Height(300)).clampToScreen();
                construct_loader.Draw();
            }
            //transfer vessels
            if(vessels_window != null)
            {
                vessels_window.Draw(ConnectedStorage);
                vessels_window.TransferVessel();
            }
        }

        [KSPEvent (guiActive = true, guiActiveEditor = true, guiName = "Rename Hangar", active = true)]
        public void EditName() 
        { 
            hangar_name_editor.Text = HangarName;
            hangar_name_editor.Toggle();
        }

        [KSPEvent (guiActiveEditor = true, guiName = "Edit contents", active = true)]
        public void EditHangar() 
        { 
            if(!HighLogic.LoadedSceneIsEditor) return;
            editing_content = !editing_content;
            Utils.LockIfMouseOver(eLock, eWindowPos, editing_content);
        }

        [KSPEvent (guiActiveEditor = true, guiName = "Relocate vessels", active = true)]
        public void RelocateVessels() 
        { 
            if(vessels_window != null) 
                vessels_window.Toggle(); 
        }
        #endregion

        public override string ToString() { return HangarName; }

        #if DEBUG
        void OnRenderObject()
        {
            if(vessel != null)
            {
                if(vessel != FlightGlobals.ActiveVessel)
                {
                    Utils.GLDrawPoint(vessel.transform.position, Color.red);
                    Utils.GLDrawPoint(vessel.CoM, Color.green);
                }
//              Utils.GLLine(vessel.transform.position, vessel.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime()+TimeWarp.fixedDeltaTime).xzy+vessel.mainBody.position, Color.magenta);
//              Utils.GLVec(vessel.transform.position,  vessel.orbit.GetRotFrameVel(vessel.mainBody).xzy*TimeWarp.fixedDeltaTime, Color.blue);  
                Utils.GLVec(part.transform.position+part.transform.TransformDirection(part.CoMOffset), momentumDelta, Color.red);
            }
        }
        #endif
    }
}

