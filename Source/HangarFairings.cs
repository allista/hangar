using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class HangarFairings : Hangar
	{
		[KSPField] public string  Fairings = "fairings";
		[KSPField] public float   FairingsDensity = 0.1f;
		[KSPField] public Vector3 JettisonDirection = Vector3.up;
		[KSPField] public float   JettisonForce  = 100f;
		[KSPField] public double  DebrisLifetime = 600;
		Transform[] fairings;

		[KSPField] public string  FxGroup = "decouple";
		FXGroup FX;

		[KSPField(isPersistant = true)]
		public int CrewCapacity = 0;

		[KSPField(isPersistant = true)]
		public bool jettisoned, launch_in_progress;

		public override string GetInfo()
		{
			var info = base.GetInfo();
			if(LaunchVelocity != Vector3.zero)
				info += string.Format("Jettison Velocity: {0:F1}m/s\n", LaunchVelocity.magnitude);
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
			fairings = part.FindModelTransforms(Fairings);
			if(fairings != null && jettisoned)
				fairings.ForEach(f => f.gameObject.SetActive(false));
			JettisonDirection.Normalize();
		}

		IEnumerator<YieldInstruction> update_crew_capacity()
		{
			if(!HighLogic.LoadedSceneIsEditor) yield break;
			this.Log("starting update_crew_capacity");//debug
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

		void jettison_fairings()
		{
			if(fairings == null || jettisoned) return;
			if(FX != null) FX.Burst();
			foreach(var f in fairings)
			{
				var rb = f.gameObject.AddComponent<Rigidbody>();
				rb.angularVelocity = part.rigidbody.angularVelocity;
				rb.velocity = part.rigidbody.velocity + 
					Vector3.Cross(vessel.CurrentCoM - part.rigidbody.worldCenterOfMass, 
					              rb.angularVelocity);
				rb.SetDensity(FairingsDensity);
				rb.useGravity = true;
				f.parent = null;
				var force = f.TransformDirection(JettisonDirection) * JettisonForce * 0.5f;
				rb.AddForceAtPosition(force, f.position, ForceMode.Force);
				part.rigidbody.AddForceAtPosition(-force, f.position, ForceMode.Force);
				Debris.SetupOnGO(f.gameObject, "FairingsDebris", DebrisLifetime);
			}
			jettisoned = true;
		}

		protected override void on_vessel_launch(StoredVessel sv)
		{
			sv.crew.Clear();
			sv.crew.AddRange(part.protoModuleCrew);
			//transfer the target and controls
			var this_vsl = vessel.BackupVessel();
			sv.proto_vessel.targetInfo   = this_vsl.targetInfo;
			sv.proto_vessel.ctrlState    = this_vsl.ctrlState;
			sv.proto_vessel.actionGroups = this_vsl.actionGroups;
			//transfer the flight plan
			if(vessel.patchedConicSolver.maneuverNodes.Count > 0)
			{
				var nearest_node = vessel.patchedConicSolver.maneuverNodes[0];
				var new_orbit = sv.proto_vessel.orbitSnapShot.Load();
				var vvel = new_orbit.getOrbitalVelocityAtUT(nearest_node.UT).xzy;
				var vpos = new_orbit.getPositionAtUT(nearest_node.UT).xzy;
				nearest_node.nodeRotation = Quaternion.LookRotation(vvel, Vector3d.Cross(-vpos, vvel));
				nearest_node.DeltaV = nearest_node.nodeRotation.Inverse() * (nearest_node.nextPatch.getOrbitalVelocityAtUT(nearest_node.UT).xzy-vvel);
				sv.proto_vessel.flightPlan.ClearData();
				vessel.patchedConicSolver.Save(sv.proto_vessel.flightPlan);
				vessel.patchedConicSolver.maneuverNodes.Clear();
				vessel.patchedConicSolver.flightPlan.Clear();
			}
			jettison_fairings();
			//turn everything off
			Storage.enabled = Storage.isEnabled = false;
			Events["LaunchVessel"].active = Actions["LaunchVesselAction"].active = false;
		}

		IEnumerator<YieldInstruction> delayed_launch()
		{
			//check state
			if(!HighLogic.LoadedSceneIsFlight) yield break;
			if(Storage == null || Storage.VesselsDocked == 0) 
			{
				ScreenMessager.showMessage("No payload");
				yield break;
			}
			if(gates_state != AnimatorState.Opened && !hangar_gates.Playing) yield break;
			//set the flag and wait for the doors to open
			launch_in_progress = true;
			while(hangar_gates.Playing) yield return null;
			//activate the hangar, get the vessel from the storage, set its crew
			Activate();
			//try to restore vessel and check the result
			TryRestoreVessel(Storage.GetVessels()[0]);
			launch_in_progress = false;
		}

		[KSPEvent(guiActive = true, guiName = "Jettison Payload", guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 4)]
		public void LaunchVessel()
		{
			if(launch_in_progress) return;
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

	public class Debris : Vessel
	{
		IEnumerator<YieldInstruction> check_distance()
		{
			while(true)
			{
				var fg = FlightGlobals.fetch;
				if(fg == null) { Die(); yield break; }
				if(fg.activeVessel == null) { Die(); yield break; }
				if(Vector3.Distance(fg.activeVessel.transform.position, transform.position) > unloadDistance*0.9f)
				{ Die(); yield break; }
				yield return new WaitForSeconds(0.1f);
			}
		}

		public void Setup(string vessel_name, double lifetime)
		{
			name = vesselName = vessel_name;
			Initialize();
			DiscoveryInfo.SetLastObservedTime(Planetarium.GetUniversalTime());
			DiscoveryInfo.SetUnobservedLifetime(lifetime);
			DiscoveryInfo.SetUntrackedObjectSize(UntrackedObjectClass.A);
			DiscoveryInfo.SetLevel(DiscoveryLevels.None);
			StartCoroutine(check_distance());
		}

		public static Debris SetupOnGO(GameObject host, string part_prefab, double lifetime)
		{
			var part = host.AddComponent<Part>();
			part.partInfo = PartLoader.getPartInfoByName(part_prefab);
			part.srfAttachNode = new AttachNode();
			part.attachRules = AttachRules.Parse("0,0,0,0,0");
			part.mass = host.rigidbody.mass;
			var debris = host.AddComponent<Debris>();
			debris.Setup(HangarGUI.ParseCamelCase(part_prefab), lifetime);
			return debris;
		}
	}
}

