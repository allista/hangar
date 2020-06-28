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
        private const string DEBRIS_PART = "GenericDebris";

        [KSPField(isPersistant = true)] public string original_part_name = string.Empty;
        [KSPField(isPersistant = true)] public string debris_transform_name = string.Empty;
        [KSPField(isPersistant = true)] public float saved_cost, saved_mass = -1f;
        [KSPField(isPersistant = true)] public Vector3 local_scale = Vector3.one;
        [KSPField(isPersistant = true)] public Quaternion local_rotation = Quaternion.identity;
        [KSPField(isPersistant = true)] public float selfDestruct = -1;
        [KSPField(isPersistant = true)] public float selfDestructPower;

        public Transform actual_object;

        public Rigidbody Rigidbody => part != null ? part.Rigidbody : null;

        #region IPart*Modifiers
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => saved_cost - defaultCost;

        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.CONSTANTLY;

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => saved_mass - defaultMass;

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.CONSTANTLY;
        #endregion

        public void DetectCollisions(bool detect)
        {
            if(part == null)
                return;
            part.SetDetectCollisions(detect);
        }

        public override void OnStart(StartState state)
        {
            if(actual_object == null
               && !string.IsNullOrEmpty(original_part_name)
               && !string.IsNullOrEmpty(debris_transform_name))
            {
                var info = PartLoader.getPartInfoByName(original_part_name);
                if(info == null)
                {
                    this.Log("WARNING: {} part was not found in the database!", original_part_name);
                    return;
                }
                actual_object = info.partPrefab.FindModelTransform(debris_transform_name);
                if(actual_object == null)
                {
                    this.Log("WARNING: {} part does not have {} transform!", original_part_name, debris_transform_name);
                    return;
                }
                var debris_part_model = part.transform.Find("model");
                actual_object = Instantiate(actual_object.gameObject).transform;
                actual_object.parent = debris_part_model;
                actual_object.localPosition = Vector3.zero;
                actual_object.localRotation = local_rotation;
                debris_part_model.localScale = local_scale;
                debris_part_model.hasChanged = true;
                part.transform.hasChanged = true;
            }
            StartCoroutine(update_drag_cubes());
            if(selfDestruct > 0)
                StartCoroutine(self_destruct());
        }

        private const int skip_updates = 10;

        private IEnumerator<YieldInstruction> update_drag_cubes()
        {
            if(!HighLogic.LoadedSceneIsFlight)
                yield break;
            for(var i = skip_updates; i > 0; i--)
                yield return new WaitForFixedUpdate();
            part.DragCubes.ClearCubes();
            part.DragCubes.Cubes.Add(DragCubeSystem.Instance.RenderProceduralDragCube(part));
            part.DragCubes.ResetCubeWeights();
            part.DragCubes.ForceUpdate(true, true, true);
        }

        private IEnumerator<YieldInstruction> self_destruct()
        {
            var endUT = Planetarium.GetUniversalTime() + selfDestruct;
            while(Planetarium.GetUniversalTime() < endUT)
                yield return null;
            FXMonger.Explode(part, vessel.GetWorldPos3D(), selfDestructPower);
            yield return null;
            part.Die();
        }

        public static Debris SetupOnTransform(
            Part original_part,
            Transform debris_transform,
            float mass,
            float cost,
            double lifetime
        )
        {
            //get the part form DB
            var info = PartLoader.getPartInfoByName(DEBRIS_PART);
            if(info == null)
                return null;
            //set part's transform and parent the debris model to the part
            var part = Instantiate(info.partPrefab);
            var partTransform = part.transform;
            partTransform.position = debris_transform.position;
            partTransform.rotation = original_part.transform.rotation;
            //copy the model and resize it
            var debris_object = Instantiate(debris_transform.gameObject).transform;
            var debris_part_model = part.transform.Find("model");
            var orig_part_model = original_part.transform.Find("model");
            debris_object.parent = debris_part_model;
            debris_object.localPosition = Vector3.zero;
            debris_object.rotation = debris_transform.rotation;
            debris_part_model.localScale = Vector3.Scale(debris_part_model.localScale, orig_part_model.localScale);
            debris_part_model.hasChanged = true;
            part.transform.hasChanged = true;
            //initialize the part
            part.gameObject.SetActive(true);
            part.physicalSignificance = Part.PhysicalSignificance.NONE;
            part.PromoteToPhysicalPart();
            part.Rigidbody.mass = mass;
            part.orgPos = Vector3.zero;
            part.orgRot = Quaternion.identity;
            //initialize Debris module
            var debris = part.Modules.GetModule<Debris>();
            if(debris == null)
            {
                Utils.Log("WARNING: {} part does not have Debris module!", DEBRIS_PART);
                Destroy(part.gameObject);
                return null;
            }
            debris.actual_object = debris_object;
            debris.saved_cost = cost;
            debris.saved_mass = part.Rigidbody.mass;
            debris.original_part_name = original_part.partInfo.name;
            debris.debris_transform_name = debris_transform.name;
            debris.local_rotation = debris_object.localRotation;
            debris.local_scale = debris_object.parent.localScale;
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
            part.Rigidbody.velocity = original_part.Rigidbody.velocity
                                      + Vector3.Cross(
                                          original_part.Rigidbody.worldCenterOfMass - part.Rigidbody.worldCenterOfMass,
                                          part.Rigidbody.angularVelocity);
            //setup discovery info
            vessel.DiscoveryInfo.SetLastObservedTime(Planetarium.GetUniversalTime());
            vessel.DiscoveryInfo.SetUnobservedLifetime(lifetime);
            vessel.DiscoveryInfo.SetUntrackedObjectSize(UntrackedObjectClass.A);
            vessel.DiscoveryInfo.SetLevel(DiscoveryLevels.Owned);
            //inform the game about the new vessel
            GameEvents.onNewVesselCreated.Fire(vessel);
            //return the part
            return debris;
        }
    }
}
