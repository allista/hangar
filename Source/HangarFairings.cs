using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AtHangar
{
	public class HangarFairings : Hangar
	{
		[KSPField(isPersistant = true)]
		public int CrewCapacity = 0;

		bool launch_in_progress;
		[KSPField] string FxGroup = "decouple";
		FXGroup FX;

		public override string GetInfo()
		{
			var info = base.GetInfo();
			parse_launch_velocity();
			if(launchVelocity != Vector3.zero)
				info += string.Format("Jettison Velocity: {0:F1}m/s\n", launchVelocity.magnitude);
			return info;
		}

		protected override void early_setup(StartState state)
		{
			base.early_setup(state);
			NoGUI = true; 
			LaunchWithPunch   = true;
			part.stagingIcon  = "DECOUPLER_HOR";
			part.CrewCapacity = CrewCapacity;
			FX = part.findFxGroup(FxGroup);
		}

		IEnumerator<YieldInstruction> update_crew_capacity()
		{
			this.Log("starting update_crew_capacity");//debug
			if(!HighLogic.LoadedSceneIsEditor) yield break;
			while(true)
			{
				this.Log("updating crew capacity: Storage {0}", Storage);//debug
				var vsl = Storage != null && Storage.VesselsDocked > 0 ? 
					Storage.GetConstructs()[0] : null;
				this.Log("updating crew capacity: vsl {0}, capacity {1}, saved {2}", //debug
					vsl, vsl == null? -1 : vsl.CrewCapacity, CrewCapacity);
				var capacity = vsl != null? vsl.CrewCapacity : 0;
				if(capacity != part.partInfo.partPrefab.CrewCapacity)
					update_crew_capacity(capacity);
				yield return new WaitForSeconds(0.1f);
			}
		}

		void update_crew_capacity(int capacity)
		{
			part.partInfo.partPrefab.CrewCapacity = part.CrewCapacity = CrewCapacity = capacity;
			this.Log("prefab.CrewCapacity {0}", part.partInfo.partPrefab.CrewCapacity);//debug
			ShipConstruction.ShipManifest = HighLogic.CurrentGame.CrewRoster.DefaultCrewForVessel(ShipConstruction.ShipConfig);
			CMAssignmentDialog.Instance.RefreshCrewLists(ShipConstruction.ShipManifest, false, true);
			Utils.UpdateEditorGUI();
		}

		protected override void start_coroutines()
		{
			base.start_coroutines();
			StartCoroutine(update_crew_capacity());
		}

		protected override void on_vessel_launch(StoredVessel sv)
		{
			sv.crew.Clear();
			sv.crew.AddRange(part.protoModuleCrew);
			//transfer the target and controls
			var this_vsl = vessel.BackupVessel();
			sv.vessel.targetInfo   = this_vsl.targetInfo;
			sv.vessel.ctrlState    = this_vsl.ctrlState;
			sv.vessel.actionGroups = this_vsl.actionGroups;
			//transfer the flight plan
			if(vessel.patchedConicSolver.maneuverNodes.Count > 0)
			{
				var nearest_node = vessel.patchedConicSolver.maneuverNodes[0];
				var new_orbit = sv.vessel.orbitSnapShot.Load();
				var vvel = new_orbit.getOrbitalVelocityAtUT(nearest_node.UT).xzy;
				var vpos = new_orbit.getPositionAtUT(nearest_node.UT).xzy;
				nearest_node.nodeRotation = Quaternion.LookRotation(vvel, Vector3d.Cross(-vpos, vvel));
				nearest_node.DeltaV = nearest_node.nodeRotation.Inverse() * (nearest_node.nextPatch.getOrbitalVelocityAtUT(nearest_node.UT).xzy-vvel);
				sv.vessel.flightPlan.ClearData();
				vessel.patchedConicSolver.Save(sv.vessel.flightPlan);
				vessel.patchedConicSolver.maneuverNodes.Clear();
				vessel.patchedConicSolver.flightPlan.Clear();
			}
			//playe FX
			if(FX != null) 
			{
				FX.Burst();
				//the audio is played anyway, so the delay is needed for particle emitters, 
				//but I don't think such FX are apropriate here
				//				var delay = FX.audio != null && FX.audio.clip != null? FX.audio.clip.length : 0.5f;
				//				yield return new WaitForSeconds(delay);
			}
			//turn fairings off
			Storage.enabled = Storage.isEnabled = false;
			enabled = isEnabled = false;
		}

		IEnumerator<YieldInstruction> delayed_launch()
		{
			//check state
			if(!HighLogic.LoadedSceneIsFlight) yield break;
			if(Storage == null || Storage.VesselsDocked == 0) yield break;
			if(gates_state != AnimatorState.Opened && !hangar_gates.Playing) yield break;
			//set the flag and wait for the doors to open
			launch_in_progress = true;
			while(hangar_gates.Playing) yield return null;
			//activate the hangar, get the vessel from the storage, set its crew
			Activate();
			//try to restore vessel and check the result
			TryRestoreVessel(Storage.GetVessels()[0]);
		}

		[KSPEvent(guiActive = true, guiName = "Jettison Payload", guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 4)]
		public void LaunchVessel()
		{
			if(!enabled || launch_in_progress || hangar_gates == null) return;
			Open();	StartCoroutine(delayed_launch());
		}

		[KSPAction("Jettison Payload")]
		public void LaunchVesselAction(KSPActionParam param)
		{ LaunchVessel(); }

		public override void OnActive()
		{ 
			if(!HighLogic.LoadedSceneIsFlight) return;
			LaunchVessel(); 
		}
	}
}

