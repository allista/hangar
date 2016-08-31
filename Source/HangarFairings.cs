//   HangarFairings.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI;
using KSP.UI.Screens;
using AT_Utils;

namespace AtHangar
{
	public class HangarFairings : Hangar, IPartCostModifier, IPartMassModifier, IMultipleDragCube
	{
		[KSPField] public string  Fairings          = "fairings";
		[KSPField] public float   FairingsDensity   = 0.5f; //t/m3
		[KSPField] public float   FairingsCost      = 20f;  //credits per fairing
		[KSPField] public Vector3 BaseCoMOffset     = Vector3.zero;
		[KSPField] public Vector3 JettisonDirection = Vector3.up;
		[KSPField] public float   JettisonForce     = 50f;
		[KSPField] public double  DebrisLifetime    = 600;
		Transform[] fairings;

		[KSPField(isPersistant = true)] public float debris_cost, debris_mass = -1f;

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

		#region IPart*Modifiers
		public virtual float GetModuleCost(float defaultCost, ModifierStagingSituation situation) { return jettisoned ? debris_cost : 0f; }
		public virtual ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

		public virtual float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return jettisoned ? debris_mass : 0f; }
		public virtual ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
		#endregion

		#region IMultipleDragCube
		static readonly string[] cube_names = {"Fairing", "Clean"};
		public string[] GetDragCubeNames() { return cube_names; }

		public void AssumeDragCubePosition(string anim) 
		{
			fairings = part.FindModelTransforms(Fairings);
			if(fairings == null) return;
			if(anim == "Fairing")
				fairings.ForEach(f => f.gameObject.SetActive(true));
			else 
				fairings.ForEach(f => f.gameObject.SetActive(false));
		}
		public bool UsesProceduralDragCubes() { return false; }
		#endregion

		public void UpdateCoMOffset(Vector3 CoMOffset)
		{
			BaseCoMOffset = CoMOffset;
			if(jettisoned) part.CoMOffset = BaseCoMOffset;
		}

		protected override void early_setup(StartState state)
		{
			base.early_setup(state);
			NoGUI = true; 
			LaunchWithPunch = true;
			part.CrewCapacity = CrewCapacity;
			Events["EditName"].active = false;
			FX = part.findFxGroup(FxGroup);
			fairings = part.FindModelTransforms(Fairings);
			if(fairings != null && jettisoned)
			{
				part.stagingIcon = string.Empty;
				fairings.ForEach(f => f.gameObject.SetActive(false));
				part.DragCubes.SetCubeWeight("Fairing ", 0f);
				part.DragCubes.SetCubeWeight("Clean ", 1f);
				part.CoMOffset = BaseCoMOffset;
			}
			else
			{
				part.stagingIcon = "DECOUPLER_HOR";
				fairings.ForEach(f => f.gameObject.SetActive(true));
				part.DragCubes.SetCubeWeight("Fairing ", 1f);
				part.DragCubes.SetCubeWeight("Clean ", 0f);
			}
			JettisonDirection.Normalize();
			vessel.SpawnCrew();
		}

		IEnumerator<YieldInstruction> update_crew_capacity()
		{
			if(!HighLogic.LoadedSceneIsEditor || Storage == null) yield break;
			while(!Storage.Ready) yield return null;
			while(true)
			{
				var vsl = Storage.TotalVesselsDocked > 0 ? Storage.GetConstructs()[0] : null;
				var capacity = vsl != null? vsl.CrewCapacity : 0;
				if(part.partInfo != null && part.partInfo.partPrefab != null &&
				   capacity != part.partInfo.partPrefab.CrewCapacity)
					update_crew_capacity(capacity);
				yield return new WaitForSeconds(0.1f);
			}
		}

		void update_crew_capacity(int capacity)
		{
			part.partInfo.partPrefab.CrewCapacity = part.CrewCapacity = CrewCapacity = capacity;
			ShipConstruction.ShipConfig = EditorLogic.fetch.ship.SaveShip();
			ShipConstruction.ShipManifest = HighLogic.CurrentGame.CrewRoster.DefaultCrewForVessel(ShipConstruction.ShipConfig);
			if(CrewAssignmentDialog.Instance != null)
				CrewAssignmentDialog.Instance.RefreshCrewLists(ShipConstruction.ShipManifest, false, true);
			Utils.UpdateEditorGUI();
		}

		protected override void start_coroutines()
		{
			base.start_coroutines();
			StartCoroutine(update_crew_capacity());
		}

		protected override void before_vessel_launch()
		{
			if(fairings == null || jettisoned) return;
			launched_vessel.crew.Clear();
			launched_vessel.crew.AddRange(part.protoModuleCrew);
			debris = new List<Part>();
			debris_cost = 0;
			debris_mass = 0;
			foreach(var f in fairings)
			{
				var d = Debris.SetupOnTransform(vessel, part, f, FairingsDensity, FairingsCost, DebrisLifetime);
				var force = f.TransformDirection(JettisonDirection) * JettisonForce * 0.5f;
				var pos = d.Rigidbody.worldCenterOfMass;
				d.SetDetectCollisions(false);
				d.Rigidbody.AddForceAtPosition(force, pos, ForceMode.Force);
				part.Rigidbody.AddForceAtPosition(-force, pos, ForceMode.Force);
				debris_mass += d.Rigidbody.mass;
				debris_cost += FairingsCost;
				debris.Add(d);
			}
			part.CoMOffset = BaseCoMOffset;
			part.DragCubes.SetCubeWeight("Fairing ", 0f);
			part.DragCubes.SetCubeWeight("Clean ", 1f);
			part.DragCubes.ForceUpdate(true, true, true);
			//this event is catched by FlightLogger
			GameEvents.onStageSeparation.Fire(new EventReport(FlightEvents.STAGESEPARATION, part, null, null, StageManager.CurrentStage, string.Empty));
			if(FX != null) FX.Burst();
			jettisoned = true;
		}

		protected override void disable_collisions(bool disable = true)
		{
			base.disable_collisions(disable);
			if(debris != null) debris.ForEach(p => p.SetDetectCollisions(!disable));
		}

		protected override void on_vessel_positioned()
		{
			//transfer the target and controls
			var this_vsl = vessel.BackupVessel();
			launched_vessel.proto_vessel.targetInfo   = this_vsl.targetInfo;
			launched_vessel.proto_vessel.ctrlState    = this_vsl.ctrlState;
			launched_vessel.proto_vessel.actionGroups = this_vsl.actionGroups;
			//transfer the flight plan
			if(vessel.patchedConicSolver != null &&
				vessel.patchedConicSolver.maneuverNodes.Count > 0)
			{
				var nearest_node = vessel.patchedConicSolver.maneuverNodes[0];
				var new_orbit = launched_vessel.proto_vessel.orbitSnapShot.Load();
				var vvel = new_orbit.getOrbitalVelocityAtUT(nearest_node.UT).xzy;
				var vpos = new_orbit.getPositionAtUT(nearest_node.UT).xzy;
				nearest_node.nodeRotation = Quaternion.LookRotation(vvel, Vector3d.Cross(-vpos, vvel));
				nearest_node.DeltaV = nearest_node.nodeRotation.Inverse() * (nearest_node.nextPatch.getOrbitalVelocityAtUT(nearest_node.UT).xzy-vvel);
				launched_vessel.proto_vessel.flightPlan.ClearData();
				vessel.patchedConicSolver.Save(launched_vessel.proto_vessel.flightPlan);
				vessel.patchedConicSolver.maneuverNodes.Clear();
				vessel.patchedConicSolver.flightPlan.Clear();
			}
			//turn everything off
			Storage.enabled = Storage.isEnabled = false;
			Events["LaunchVessel"].active = Actions["LaunchVesselAction"].active = false;
		}

		IEnumerator<YieldInstruction> delayed_launch()
		{
			//check state
			if(!HighLogic.LoadedSceneIsFlight || Storage == null || !Storage.Ready) yield break;
			if(Storage.GetVessels().Count == 0) 
			{
				Utils.Message("No payload");
				yield break;
			}
			if(gates_state != AnimatorState.Opened && 
			   hangar_gates != null && !hangar_gates.Playing) yield break;
			//set the flag and wait for the doors to open
			launch_in_progress = true;
			if(hangar_gates != null)
				while(hangar_gates.Playing) 
					yield return null;
			//activate the hangar, get the vessel from the storage, set its crew
			Activate();
			//try to restore vessel and check the result
			TryRestoreVessel(Storage.GetVessels()[0]);
			//if jettisoning has failed, deactivate the part
			//otherwise on resume the part is activated automatically
			if(Storage.enabled) part.deactivate();
			else { while(launched_vessel != null) yield return null; }
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

		#if DEBUG
//		Vector3d last_pos = Vector3d.zero;
//		Vector3d last_opos = Vector3d.zero;
//		public override void FixedUpdate()
//		{
//			base.FixedUpdate();
//			if(debris != null && debris.Count > 0)
//			{
//				var d = debris[0];
//				var delta = (d.vessel.CoM-last_pos).magnitude;
//				var odelta = (d.orbit.pos-last_opos).magnitude;
//				this.Log("delta pos:  {}\n" +
//				         "delta orb:  {}\n" +
//				         "pos-CB - orb: {}\n" +
//				         "orbit:\n{}\n" +
//				         "driver.offsetByFrame {}, was {}\n" +
//				         "driver.localCoM {}", 
//				         delta.ToString("F3"), 
//				         odelta.ToString("F3"),
//				         (d.vessel.CoMD-d.vessel.mainBody.position).xzy-d.orbit.pos,
//				         d.orbit, d.vessel.orbitDriver.offsetPosByAFrame, d.vessel.orbitDriver.wasOffsetPosByAFrame,
//				         d.vessel.orbitDriver.localCoM);
//				last_pos = d.vessel.CoM;
//				last_opos = d.orbit.pos;
//			}
//		}
		#endif
	}

	public class Debris : PartModule, IPartCostModifier, IPartMassModifier
	{
		const string DEBRIS_PART = "GenericDebris";

		[KSPField(isPersistant = true)] public string original_part_name = string.Empty;
		[KSPField(isPersistant = true)] public string debris_transform_name = string.Empty;
		[KSPField(isPersistant = true)] public float  saved_cost, saved_mass = -1f;
		[KSPField(isPersistant = true)] public float  size = -1f, aspect = -1f;
		[KSPField(isPersistant = true)] public Quaternion local_rotation = Quaternion.identity;

		public Transform model;

		#region IPart*Modifiers
		public virtual float GetModuleCost(float defaultCost, ModifierStagingSituation sit) { return saved_cost-defaultCost; }
		public virtual ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

		public virtual float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return saved_mass-defaultMass; }
		public virtual ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
		#endregion

		public override void OnStart(StartState state)
		{
			if(model == null &&	!string.IsNullOrEmpty(original_part_name) && !string.IsNullOrEmpty(debris_transform_name))
			{
				var info = PartLoader.getPartInfoByName(original_part_name);
				if(info == null) 
				{ 
					this.Log("WARNING: {} part was not found in the database!", original_part_name);
					return;
				}
				model = info.partPrefab.FindModelTransform(debris_transform_name);
				if(model == null) 
				{ 
					this.Log("WARNING: {} part does not have {} transform!", original_part_name, debris_transform_name);
					return;
				}
				model = Instantiate(model.gameObject).transform;
				var base_model = part.transform.Find("model");
				model.parent = base_model;
				model.localPosition = Vector3.zero;
				model.localRotation = local_rotation;
				model.parent.localScale = Scale.ScaleVector(base_model.localScale, size, aspect);
				model.parent.hasChanged = true;
				part.transform.hasChanged = true;
			}
			StartCoroutine(update_drag_cubes());
		}

		const int skip_updates = 10;
		IEnumerator<YieldInstruction> update_drag_cubes()
		{
			if(!HighLogic.LoadedSceneIsFlight) yield break;
			for(int i = skip_updates; i > 0; i--) yield return new WaitForFixedUpdate();
			part.DragCubes.ClearCubes();
			part.DragCubes.Cubes.Add(DragCubeSystem.Instance.RenderProceduralDragCube(part));
			part.DragCubes.ResetCubeWeights();
			part.DragCubes.ForceUpdate(true, true, true);
		}

		public static Part SetupOnTransform(Vessel original_vessel, Part original_part, 
		                                    Transform debris_transform, 
		                                    float density, float cost, double lifetime)
		{
			//get the part form DB
			var info = PartLoader.getPartInfoByName(DEBRIS_PART);
			if(info == null) return null;
			//set part's transform and parent the debris model to the part
			var part = Instantiate(info.partPrefab);
			part.transform.position = debris_transform.position;
			part.transform.rotation = original_part.transform.rotation;
			//copy the model and resize it
			var model = Instantiate(debris_transform.gameObject).transform;
			var base_model = part.transform.Find("model");
			model.parent = base_model;
			model.localPosition = Vector3.zero;
			model.rotation = debris_transform.rotation;
			var resizer = original_part.GetModule<AnisotropicPartResizer>();
			if(resizer != null) model.parent.localScale = resizer.scale.ScaleVector(base_model.localScale);
			part.transform.hasChanged = true;
			//initialize the part
			part.gameObject.SetActive(true);
			part.physicalSignificance = Part.PhysicalSignificance.NONE;
			part.PromoteToPhysicalPart();
			part.Rigidbody.SetDensity(density);
			part.orgPos = Vector3.zero;
			part.orgRot = Quaternion.identity;
			//initialize Debris module
			var debris = part.GetModule<Debris>();
			if(debris == null) 
			{ 
				Utils.Log("WARNING: {} part does not have Debris module!", DEBRIS_PART);
				Destroy(part.gameObject); return null; 
			}
			debris.model = model;
			debris.saved_cost = cost;
			debris.saved_mass = part.Rigidbody.mass;
			debris.original_part_name = original_part.partInfo.name;
			debris.debris_transform_name = debris_transform.name;
			debris.local_rotation = model.localRotation;
			if(resizer != null)
			{
				var scale = resizer.scale;
				debris.size = scale;
				debris.aspect = scale.aspect;
			}
			debris_transform.gameObject.SetActive(false);
			//initialize the vessel
			var vessel = part.gameObject.AddComponent<Vessel>();
			vessel.name = vessel.vesselName = "Debris";
			vessel.id = Guid.NewGuid();
			vessel.Initialize();
			//setup ids and flag
			part.flightID = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);
			part.missionID = original_part.missionID;
			part.launchID = original_part.launchID;
			part.flagURL = original_part.flagURL;
			//set part's velocities
			part.Rigidbody.angularVelocity = original_part.Rigidbody.angularVelocity;
			part.Rigidbody.velocity = original_part.Rigidbody.velocity + 
				Vector3.Cross(original_vessel.CurrentCoM - original_part.Rigidbody.worldCenterOfMass, 
				              part.Rigidbody.angularVelocity);
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

	public class HangarFairingsUpdater : ModuleUpdater<HangarFairings>
	{
		protected override void on_rescale(ModulePair<HangarFairings> mp, Scale scale)
		{ 
			mp.module.JettisonForce = mp.base_module.JettisonForce * scale.absolute.cube * scale.absolute.aspect;
			mp.module.FairingsCost  = mp.base_module.FairingsCost * scale.absolute.cube * scale.absolute.aspect;
			mp.module.UpdateCoMOffset(scale.ScaleVector(mp.base_module.BaseCoMOffset));
		}
	}
}

