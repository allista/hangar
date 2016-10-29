//   Debris.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
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
			var resizer = original_part.Modules.GetModule<AnisotropicPartResizer>();
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
			var debris = part.Modules.GetModule<Debris>();
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
}

