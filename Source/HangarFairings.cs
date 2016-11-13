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

		class PayloadRes : ConfigNodeObject
		{ 
			[Persistent] public string name = ""; 
			[Persistent] public double amount = 0; 
			[Persistent] public double maxAmount = 0;

			public PayloadRes() {}
			public PayloadRes(PartResource res)
			{
				name = res.resourceName;
				amount = res.amount;
				maxAmount = res.maxAmount;
			}

			public void ApplyTo(PartResource res)
			{
				if(name == res.resourceName)
				{
					res.amount = amount;
					res.maxAmount = maxAmount;
				}
			}
		}
		readonly List<PayloadRes> payload_resources = new List<PayloadRes>();

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

		public override float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return jettisoned ? debris_mass : 0f; }
		public override ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
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
		public bool IsMultipleCubesActive { get { return true; } }
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
			update_crew_capacity(CrewCapacity);
			Events["EditName"].active = false;
			FX = part.findFxGroup(FxGroup);
			fairings = part.FindModelTransforms(Fairings);
			if(fairings != null)
			{
				if(jettisoned)
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
			}
			else this.Log("No Fairings transforms found with the name: {}", Fairings);
			JettisonDirection.Normalize();
			if(vessel != null) vessel.SpawnCrew();
			if(Storage != null)
			{
				Storage.OnConstructStored += on_ship_stored;
				Storage.OnConstructRemoved += on_ship_removed;
				Storage.OnVesselRemoved += on_ship_removed;
				Storage.OnStorageEmpty += on_storage_empty;
			}
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			if(Storage != null)
			{
				Storage.OnConstructStored -= on_ship_stored;
				Storage.OnConstructRemoved -= on_ship_removed;
				Storage.OnVesselRemoved -= on_ship_removed;
				Storage.OnStorageEmpty -= on_storage_empty;
			}
		}

		protected override bool try_store_vessel(PackedVessel v)
		{
			if(Storage.TotalVesselsDocked > 0)
			{
				Utils.Message("Payload is already stored");
				return false;
			}
			return base.try_store_vessel(v);
		}

		bool store_payload_resources(PackedVessel payload)
		{
			if(payload_resources.Count > 0) return false;
			var res_mass = 0.0;
			var resources = payload.resources;
			foreach(var r in resources.resourcesNames)
			{
				if(part.Resources.Contains(r)) continue;
				if(Globals.Instance.ResourcesBlacklist.IndexOf(r) >= 0) continue;
				var res = part.Resources.Add(r, resources.ResourceAmount(r), resources.ResourceCapacity(r), 
				                             true, true, true, true, PartResource.FlowMode.Both);
				if(res != null) 
				{
					payload_resources.Add(new PayloadRes(res));
					resources.TransferResource(r, -res.amount);
					res_mass += res.amount*res.info.density;
				}
			}
			payload.mass -= (float)res_mass;
			return true;
		}

		public void ResetPayloadResources()
		{
			if(payload_resources.Count == 0) return;
			foreach(var r in payload_resources)
			{
				var res = part.Resources.Get(r.name);
				if(res != null) r.ApplyTo(res);
			}
		}

		bool restore_payload_resources(PackedVessel payload)
		{
			if(payload_resources.Count == 0) return true;
			if(HighLogic.LoadedSceneIsEditor)
				ResetPayloadResources();
			var res_mass = 0.0;
			foreach(var r in payload_resources)
			{
				var res  = part.Resources.Get(r.name);
				if(res != null)
				{
					res_mass += res.amount * res.info.density;
					payload.resources.TransferResource(r.name, res.amount);
					part.Resources.Remove(res);
				}
			}
			payload.mass += (float)res_mass;
			payload_resources.Clear();
			return true;
		}

		bool clear_payload_resouces()
		{
			if(payload_resources.Count == 0) return true;
			if(Storage != null && Storage.Ready && Storage.ConstructsCount > 0) return false;
			payload_resources.ForEach(r => part.Resources.Remove(r.name));
			payload_resources.Clear();
			return true;
		}

		void on_ship_stored(PackedVessel pc)
		{
			update_crew_capacity(pc.CrewCapacity);
			store_payload_resources(pc);
		}

		void on_ship_removed(PackedVessel pc)
		{
			if(HighLogic.LoadedSceneIsEditor)
				update_crew_capacity(0);
			restore_payload_resources(pc);
		}

		void on_storage_empty()
		{
			if(HighLogic.LoadedSceneIsEditor)
				update_crew_capacity(0);
			clear_payload_resouces();
		}

		void update_crew_capacity(int capacity)
		{
			part.CrewCapacity = CrewCapacity = capacity;
			if(part.partInfo != null && part.partInfo.partPrefab != null)
				part.partInfo.partPrefab.CrewCapacity = part.CrewCapacity;
			if(HighLogic.LoadedSceneIsEditor)
			{
				ShipConstruction.ShipConfig = EditorLogic.fetch.ship.SaveShip();
				ShipConstruction.ShipManifest = HighLogic.CurrentGame.CrewRoster.DefaultCrewForVessel(ShipConstruction.ShipConfig);
				if(CrewAssignmentDialog.Instance != null)
					CrewAssignmentDialog.Instance.RefreshCrewLists(ShipConstruction.ShipManifest, false, true);
				Utils.UpdateEditorGUI();
			}
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

		protected override void disable_hangar_collisions(bool disable = true)
		{
			base.disable_hangar_collisions(disable);
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
			if(Storage.VesselsCount == 0) 
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
			if(Storage.VesselsCount > 0) part.deactivate();
			else { while(launched_vessel != null) yield return null; }
			update_crew_capacity(0);
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
			if(HighLogic.LoadedSceneIsFlight)
				LaunchVessel(); 
		}

		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
			var payload_node = node.AddNode("PAYLOAD_RESOURCES");
			payload_resources.ForEach(r => r.Save(payload_node.AddNode("RESOURCE")));
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			var payload_node = node.GetNode("PAYLOAD_RESOURCES");
			if(payload_node != null)
			{
				foreach(var rn in payload_node.GetNodes("RESOURCE"))
				{
					var res = ConfigNodeObject.FromConfig<PayloadRes>(rn);
					if(res != null) payload_resources.Add(res);
				}
			}
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

	public class HangarFairingsUpdater : ModuleUpdater<HangarFairings>
	{
		protected override void on_rescale(ModulePair<HangarFairings> mp, Scale scale)
		{ 
			mp.module.JettisonForce = mp.base_module.JettisonForce * scale.absolute.volume;
			mp.module.FairingsCost  = mp.base_module.FairingsCost * scale.absolute.volume;
			mp.module.UpdateCoMOffset(scale.ScaleVector(mp.base_module.BaseCoMOffset));
			if(HighLogic.LoadedSceneIsEditor) mp.module.ResetPayloadResources();
		}
	}
}

