//   HangarMachineryEditor.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using AT_Utils;

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
		SimpleTextEntry hangar_name_editor;
		VesselTransferWindow vessels_window;
		SubassemblySelector subassembly_selector;
		CraftBrowserDialog vessel_selector;
		EditorFacility facility;

		IEnumerator<YieldInstruction> delayed_try_store_construct(PackedConstruct pc)
		{
			if(pc.construct == null) yield break;
			Utils.LockControls(scLock);
			for(int i = 0; i < 3; i++) yield return null;
			pc.UpdateMetric();
			try_store_vessel(pc);
			pc.UnloadConstruct();
            highlighted_content = pc;
			Utils.LockControls(scLock, false);
		}

		void process_loaded_construct(PackedConstruct pc)
		{
			//check if the construct contains launch clamps
			if(pc.construct.HasLaunchClamp())
			{
				Utils.Message("\"{0}\" has launch clamps. Remove them before storing.", pc.name);
				pc.UnloadConstruct();
				return;
			}
			//check if it's possible to launch such vessel
			bool cant_launch = false;
			var preFlightCheck = new PreFlightCheck(new Callback(() => cant_launch = false), new Callback(() => cant_launch = true));
			preFlightCheck.AddTest(new PreFlightTests.ExperimentalPartsAvailable(pc.construct));
			preFlightCheck.RunTests(); 
			//cleanup loaded parts and try to store construct
			if(cant_launch) pc.UnloadConstruct();
			else StartCoroutine(delayed_try_store_construct(pc));
		}

		void subassembly_selected(ShipTemplate template)
		{
			ShipConstruction.CreateConstructFromTemplate(template, construct =>
			{
				if(construct != null)
					process_loaded_construct(new PackedConstruct(construct, HighLogic.CurrentGame.flagURL));
			});
		}

		void vessel_selected(string filename, CraftBrowserDialog.LoadType t)
		{
			EditorLogic EL = EditorLogic.fetch;
			if(EL == null) return;
			//load vessel config
			vessel_selector = null;
			var pc = new PackedConstruct(filename, HighLogic.CurrentGame.flagURL);
			if(pc.construct == null) 
			{
				Utils.Log("PackedConstruct: unable to load ShipConstruct from {}. " +
				          "This usually means that some parts are missing " +
				          "or some modules failed to initialize.", filename);
				Utils.Message("Unable to load {0}", filename);
				return;
			}
			process_loaded_construct(pc);
		}
		void selection_canceled() { vessel_selector = null; }

        PackedVessel highlighted_content;
		void hangar_content_editor(int windowID)
		{
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			//Vessel selector
			if(GUILayout.Button("Select Vessel", Styles.normal_button, GUILayout.ExpandWidth(true))) 
				vessel_selector = 
					CraftBrowserDialog.Spawn(
						facility,
						HighLogic.SaveFolder,
						vessel_selected,
						selection_canceled, false);
			if(GUILayout.Button("Select Subassembly", Styles.normal_button, GUILayout.ExpandWidth(true)))
				subassembly_selector.Show(true);
			GUILayout.EndHorizontal();
			//hangar info
			if(ConnectedStorage.Count > 1)
				HangarGUI.UsedVolumeLabel(TotalUsedVolume, TotalUsedVolumeFrac, "Total Used Volume");
			HangarGUI.UsedVolumeLabel(UsedVolume, UsedVolumeFrac);
			//hangar contents
			var constructs = Storage.GetConstructs();
			constructs.Sort((a, b) => a.name.CompareTo(b.name));
			constructs_scroll = GUILayout.BeginScrollView(constructs_scroll, GUILayout.Height(200), GUILayout.Width(window_width));
			GUILayout.BeginVertical();
			foreach(PackedConstruct pc in constructs)
			{
				GUILayout.BeginHorizontal();
                if(HangarGUI.PackedVesselLabel(pc, pc == highlighted_content? Styles.white : Styles.label))
                    highlighted_content = pc;
				if(GUILayout.Button("+1", Styles.green_button, GUILayout.Width(25))) 
					try_store_vessel(pc.Clone());
				if(GUILayout.Button("X", Styles.red_button, GUILayout.Width(25))) 
					Storage.RemoveVessel(pc);
				GUILayout.EndHorizontal();
			}
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
			//unfit constructs
			constructs = Storage.UnfitConstucts;
			if(constructs.Count > 0)
			{
				GUILayout.Label("Unfit vessels:", Styles.yellow, GUILayout.ExpandWidth(true));
				unfit_scroll = GUILayout.BeginScrollView(unfit_scroll, GUILayout.Height(100), GUILayout.Width(window_width));
				GUILayout.BeginVertical();
				foreach(PackedConstruct pc in Storage.UnfitConstucts)
				{
					GUILayout.BeginHorizontal();
                    if(HangarGUI.PackedVesselLabel(pc, pc == highlighted_content? Styles.white : Styles.label))
                        highlighted_content = pc;
					if(GUILayout.Button("^", Styles.green_button, GUILayout.Width(25))) 
					{ if(try_store_vessel(pc.Clone())) Storage.RemoveUnfit(pc); }
					if(GUILayout.Button("X", Styles.red_button, GUILayout.Width(25))) 
						Storage.RemoveUnfit(pc);
					GUILayout.EndHorizontal();
				}
				GUILayout.EndVertical();
				GUILayout.EndScrollView();
			}
			//common buttons
			if(GUILayout.Button("Clear", Styles.red_button, GUILayout.ExpandWidth(true)))
				Storage.ClearConstructs();
			if(GUILayout.Button("Close", Styles.normal_button, GUILayout.ExpandWidth(true))) 
			{
				Utils.LockControls(eLock, false);
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
				if(vessel_selector == null && 
				   (subassembly_selector == null || 
				    !subassembly_selector.WindowEnabled))
				{
					Utils.LockIfMouseOver(eLock, eWindowPos);
					eWindowPos = GUILayout.Window(GetInstanceID(), eWindowPos,
												  hangar_content_editor,
												  "Hangar Contents Editor",
												  GUILayout.Width(window_width),
					                              GUILayout.Height(300)).clampToScreen();
				}
				if(subassembly_selector != null)
					subassembly_selector.Draw(subassembly_selected);
			}
			//rename hangar
			if(hangar_name_editor.Draw("Rename Hangar") == SimpleDialog.Answer.Yes) 
				HangarName = hangar_name_editor.Text;
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
//				Utils.GLLine(vessel.transform.position, vessel.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime()+TimeWarp.fixedDeltaTime).xzy+vessel.mainBody.position, Color.magenta);
//				Utils.GLVec(vessel.transform.position,  vessel.orbit.GetRotFrameVel(vessel.mainBody).xzy*TimeWarp.fixedDeltaTime, Color.blue);	
				Utils.GLVec(part.transform.position+part.transform.TransformDirection(part.CoMOffset), momentumDelta, Color.red);
			}
			if(launched_vessel != null && launched_vessel.vessel != null)
			{
				Utils.GLDrawPoint(launched_vessel.vessel.transform.position, Color.yellow);
				Utils.GLLine(launched_vessel.vessel.transform.position, vessel.transform.position, Color.yellow);
				Utils.GLVec(launched_vessel.vessel.transform.position, part.Rigidbody.velocity, Color.red);
				Utils.GLVec(launched_vessel.vessel.transform.position, launched_vessel.dV, Color.cyan);
			}
			if(editing_content && Storage != null)
			{
				PackedVessel vsl = null;
				if(Storage.ConstructsCount > 0) vsl = Storage.GetConstructs()[0];
				else if(Storage.UnfitCount > 0) vsl = Storage.UnfitConstucts[0];
				if(vsl != null)
				{
					var metric = vsl.metric;
					var hull = metric.hull;
					var spawn_transform = get_spawn_transform(vsl);
					var spawn_point = metric.center-get_spawn_offset(vsl);
					Utils.GLDrawPoint(Vector3.zero, spawn_transform, Color.red);
					if(hull != null) Utils.GLDrawHull(hull, spawn_transform, Color.green, offset:spawn_point, filled:false);
				}
//				if(Storage.hangar_space != null)
//					Utils.GLDrawMesh(Storage.hangar_space.sharedMesh, Storage.hangar_space.transform, c:Color.cyan, filled:false);
			}
//			foreach(var dc in part.DragCubes.Cubes)
//				Utils.GLDrawBounds(new Bounds(dc.Center, dc.Size), part.transform, Color.yellow*dc.Weight);
		}
		#else
        void OnRenderObject()
        {
            if(editing_content && vessel_selector == null && Storage != null)
            {
                if(highlighted_content != null && highlighted_content.metric.hull != null)
                {
                    Utils.GLDrawHull2(highlighted_content.metric.hull, get_spawn_transform(highlighted_content), 
                                      Color.green, null,
                                      highlighted_content.metric.center-get_spawn_offset(highlighted_content), false);
                }
            }
        }
        #endif
	}
}

