using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AtHangar
{
	public class HangarFairings : Hangar
	{
		[KSPField(isPersistant = true)]
		public bool staged;
		bool launch_in_progress;

		[KSPField(isPersistant = true)]
		public int CrewCapacity = 0;

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
		}

		IEnumerator<YieldInstruction> update_crew_capacity()
		{
			this.Log("starting update_crew_capacity");
			if(!HighLogic.LoadedSceneIsEditor) yield break;
			while(true)
			{
				this.Log("updating crew capacity: Storage {0}", Storage);
				var vsl = Storage != null && Storage.VesselsDocked > 0 ? 
					Storage.GetConstructs()[0] : null;
				this.Log("updating crew capacity: vsl {0}, capacity {1}, saved {2}", 
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
			this.Log("prefab.CrewCapacity {0}", part.partInfo.partPrefab.CrewCapacity);
			ShipConstruction.ShipManifest = HighLogic.CurrentGame.CrewRoster.DefaultCrewForVessel(ShipConstruction.ShipConfig);
			CMAssignmentDialog.Instance.RefreshCrewLists(ShipConstruction.ShipManifest, false, true);
			Utils.UpdateEditorGUI();
		}

		protected override void start_coroutines()
		{
			base.start_coroutines();
			StartCoroutine(update_crew_capacity());
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
			var vsl = Storage.GetVessels()[0];
			vsl.crew.Clear();
			vsl.crew.AddRange(part.protoModuleCrew);
			//try to restore vessel and check the result
			TryRestoreVessel(vsl);
			staged = Storage.VesselsDocked == 0;
			Events["LaunchVessel"].active = Actions["LaunchVesselAction"].active = !staged;
			launch_in_progress = false;
		}

		[KSPEvent(guiActive = true, guiName = "Jettison Content", guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 4)]
		public void LaunchVessel()
		{
			if(staged || launch_in_progress || hangar_gates == null) return;
			Open();	StartCoroutine(delayed_launch());
		}

		[KSPAction("Jettison Content")]
		public void LaunchVesselAction(KSPActionParam param)
		{ LaunchVessel(); }

		public override void OnActive()
		{ 
			if(!HighLogic.LoadedSceneIsFlight) return;
			LaunchVessel(); 
		}
	}
}

