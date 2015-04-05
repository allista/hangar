using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class HangarFairings : Hangar, IPartCostModifier
	{
		[KSPField] public string  Fairings          = "fairings";
		[KSPField] public float   FairingsDensity   = 0.5f; //t/m3
		[KSPField] public float   FairingsCost      = 20f;  //credits per fairing
		[KSPField] public Vector3 JettisonDirection = Vector3.up;
		[KSPField] public float   JettisonForce     = 50f;
		[KSPField] public double  DebrisLifetime    = 600;
		Transform[] fairings;

		[KSPField] public string  FxGroup = "decouple";
		FXGroup FX;

		[KSPField(isPersistant = true)]
		public int CrewCapacity = 0;

		[KSPField(isPersistant = true)]
		public bool jettisoned, launch_in_progress;
		List<Part> debris;

		public override string GetInfo()
		{
			var info = base.GetInfo();
			if(LaunchVelocity != Vector3.zero)
				info += string.Format("Jettison Velocity: {0:F1}m/s\n", LaunchVelocity.magnitude);
			return info;
		}

		public float GetModuleCost(float default_cost) 
		{ 
			if(fairings != null && jettisoned) 
				return -fairings.Length * FairingsCost;
			return 0f;
		}

		protected override void early_setup(StartState state)
		{
			base.early_setup(state);
			NoGUI = true; 
			LaunchWithPunch   = true;
			part.stagingIcon  = "DECOUPLER_HOR";
			part.CrewCapacity = CrewCapacity;
			Events["EditName"].active = false;
			FX = part.findFxGroup(FxGroup);
			fairings = part.FindModelTransforms(Fairings);
			if(fairings != null && jettisoned)
				fairings.ForEach(f => f.gameObject.SetActive(false));
			JettisonDirection.Normalize();
		}

		IEnumerator<YieldInstruction> update_crew_capacity()
		{
			if(!HighLogic.LoadedSceneIsEditor || Storage == null) yield break;
			while(!Storage.Ready) yield return null;
			while(true)
			{
				var vsl = Storage.VesselsDocked > 0 ? Storage.GetConstructs()[0] : null;
				var capacity = vsl != null? vsl.CrewCapacity : 0;
				if(capacity != part.partInfo.partPrefab.CrewCapacity)
					update_crew_capacity(capacity);
				yield return new WaitForSeconds(0.1f);
			}
		}

		void update_crew_capacity(int capacity)
		{
			part.partInfo.partPrefab.CrewCapacity = part.CrewCapacity = CrewCapacity = capacity;
			ShipConstruction.ShipConfig = EditorLogic.fetch.ship.SaveShip();
			ShipConstruction.ShipManifest = HighLogic.CurrentGame.CrewRoster.DefaultCrewForVessel(ShipConstruction.ShipConfig);
			CMAssignmentDialog.Instance.RefreshCrewLists(ShipConstruction.ShipManifest, true, true);
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
			debris = new List<Part>();
			foreach(var f in fairings)
			{
				var d = Debris.SetupOnTransform(vessel, part, f, FairingsDensity, FairingsCost, DebrisLifetime);
				var force = f.TransformDirection(JettisonDirection) * JettisonForce * 0.5f;
				d.rigidbody.AddForceAtPosition(force, f.position, ForceMode.Force);
				part.rigidbody.AddForceAtPosition(-force, f.position, ForceMode.Force);
				debris.Add(d);
			}
			jettisoned = true;
		}

		protected override void disable_collisions(bool disable = true)
		{
			base.disable_collisions(disable);
			if(debris != null) debris.ForEach(p => p.SetDetectCollisions(!disable));
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
			if(vessel.patchedConicSolver != null &&
				vessel.patchedConicSolver.maneuverNodes.Count > 0)
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
			//this event is catched by FlightLogger
			GameEvents.onStageSeparation.Fire(new EventReport(FlightEvents.STAGESEPARATION, part, null, null, Staging.CurrentStage, string.Empty));
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
			//if jettisoning has failed, deactivate the part
			//otherwise on resume the part is activated automatically
			if(!jettisoned) part.deactivate();
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

	public class Debris : PartModule, IPartCostModifier
	{
		const string DEBRIS_PART = "GenericDebris";

		[KSPField(isPersistant = true)] public string original_part_name = string.Empty;
		[KSPField(isPersistant = true)] public string debris_transform_name = string.Empty;
		[KSPField(isPersistant = true)] public float  saved_cost, saved_mass = -1f;
		[KSPField(isPersistant = true)] public float  size = -1f, aspect = -1f;
		[KSPField(isPersistant = true)] public Quaternion local_rotation = Quaternion.identity;

		public Transform model;

		public float GetModuleCost(float default_cost) { return saved_cost; }

		public override void OnInitialize()
		{
			if(saved_mass < 0) saved_mass = part.mass;
			else part.mass = saved_mass;
			if(model == null &&	original_part_name != string.Empty && debris_transform_name != string.Empty)
			{
				var info = PartLoader.getPartInfoByName(original_part_name);
				if(info == null) 
				{ 
					this.Log("WARNING: {0} part was not found in the database!", original_part_name);
					return;
				}
				var original_part = (Part)Instantiate(info.partPrefab);
				model = original_part.FindModelTransform(debris_transform_name);
				if(model == null) 
				{ 
					this.Log("WARNING: {0} part does not have {1} transform!", original_part_name, debris_transform_name);
					return;
				}
				var base_model = part.transform.GetChild(0);
				model.SetParent(base_model);
				model.localRotation = local_rotation;
				base_model.localScale = PartUpdaterBase.ScaleVector(Vector3.one, size, aspect);
				base_model.hasChanged = true;
				Destroy(original_part.gameObject);
			}
		}

		public static Part SetupOnTransform(Vessel original_vessel, Part original_part, 
		                                    Transform debris_transform, 
		                                    float density, float cost, double lifetime)
		{
			//get the part form DB
			var info = PartLoader.getPartInfoByName(DEBRIS_PART);
			if(info == null) return null;
			var part = (Part)Instantiate(info.partPrefab);
			//set part's transform and parent the debris to the part
			part.transform.position = debris_transform.position;
			part.transform.rotation = original_part.transform.rotation;
			debris_transform.parent = part.transform.GetChild(0);
			debris_transform.localPosition = Vector3.zero;
			//initialize the part
			part.gameObject.SetActive(true);
			part.physicalSignificance = Part.PhysicalSignificance.NONE;
			part.PromoteToPhysicalPart();
			part.rigidbody.SetDensity(density);
			part.mass   = part.rigidbody.mass;
			part.orgPos = Vector3.zero;
			part.orgRot = Quaternion.identity;
			//set part's velocities
			part.rigidbody.angularVelocity = original_part.rigidbody.angularVelocity;
			part.rigidbody.velocity = original_part.rigidbody.velocity + 
				Vector3.Cross(original_vessel.CurrentCoM - original_part.rigidbody.worldCenterOfMass, 
				              part.rigidbody.angularVelocity);
			//initialize Debris module
			var debris = part.GetModule<Debris>();
			if(debris == null) 
			{ 
				Utils.Log("WARNING: {0} part does not have Debris module!", DEBRIS_PART);
				Destroy(part.gameObject); return null; 
			}
			debris.saved_cost = cost;
			debris.original_part_name = original_part.partInfo.name;
			debris.debris_transform_name = debris_transform.name;
			debris.model = debris_transform;
			debris.local_rotation = debris_transform.localRotation;
			var resizer = original_part.GetModule<HangarPartResizer>();
			if(resizer != null)
			{
				debris.size = resizer.size/resizer.orig_size;
				debris.aspect = resizer.aspect;
			}
			//initialize the vessel
			var vessel = part.gameObject.AddComponent<Vessel>();
			vessel.name = vessel.vesselName = "Debris";
			vessel.id = Guid.NewGuid();
			vessel.Initialize();
			//setup ids and flag
			part.flightID = original_part.flightID;
			part.missionID = original_part.missionID;
			part.launchID = original_part.launchID;
			part.flagURL = original_part.flagURL;
			//setup discovery info
			vessel.DiscoveryInfo.SetLastObservedTime(Planetarium.GetUniversalTime());
			vessel.DiscoveryInfo.SetUnobservedLifetime(lifetime);
			vessel.DiscoveryInfo.SetUntrackedObjectSize(UntrackedObjectClass.A);
			vessel.DiscoveryInfo.SetLevel(DiscoveryLevels.Owned);
			//inform the game about the new vessel
			GameEvents.onNewVesselCreated.Fire(vessel);
			//return the part
			return part;
		}
	}
}

