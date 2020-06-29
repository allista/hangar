//   HangarFairings.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AT_Utils;
using KSP.UI;
using UnityEngine;
using Random = System.Random;

namespace AtHangar
{
    public class HangarFairings : Hangar, IPartCostModifier, IMultipleDragCube
    {
        [KSPField] public string Fairings = "fairings";
        [KSPField] public float FairingsCost = 20f; //credits per fairing
        [KSPField] public float BaseSpecificMass = 0.5f; // by default the base has 50% of the total mass
        [KSPField] public Vector3 BaseCoMOffset = Vector3.zero;
        [KSPField] public Vector3 JettisonDirection = Vector3.up;
        [KSPField] public Vector3 JettisonForcePos = Vector3.zero;
        [KSPField] public float JettisonForce = 50f;
        [KSPField] public float MinJettisonPower = 0.01f;
        [KSPField] public float JettisonTorque;
        [KSPField] public double DebrisLifetime = 600;
        [KSPField] public string DecoupleNodes = "";

        [KSPField(isPersistant = true,
            guiName = "Debris Destruction In",
            guiActive = true,
            guiActiveEditor = true,
            guiUnits = "s")]
        [UI_FloatRange(scene = UI_Scene.All, minValue = 0, maxValue = 60, stepIncrement = 1)]
        public float DestroyDebrisIn;

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Jettison Power",
            guiFormat = "P0")]
        [UI_FloatRange(scene = UI_Scene.All, minValue = 0, maxValue = 2, stepIncrement = 0.01f)]
        public float JettisonPower = 1;

        private readonly List<Transform> fairings = new List<Transform>();
        private readonly Dictionary<Transform, float> fairingsSpecificMass = new Dictionary<Transform, float>();
        private readonly List<AttachNode> decoupleNodes = new List<AttachNode>();

        [KSPField(isPersistant = true)] public float debris_cost, debris_mass = -1f;

        [KSPField] public string FxGroup = "decouple";
        private FXGroup FX;

        [KSPField(isPersistant = true)] public int CrewCapacity;

        [KSPField(isPersistant = true)] public bool jettisoned, launch_in_progress;

        private readonly List<Debris> debris = new List<Debris>();

        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local"),
         SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        private class PayloadRes : ConfigNodeObject
        {
            [Persistent] public string name = "";
            [Persistent] public double amount;
            [Persistent] public double maxAmount;

            public PayloadRes() { }

            public PayloadRes(PartResource res)
            {
                name = res.resourceName;
                amount = res.amount;
                maxAmount = res.maxAmount;
            }

            public void ApplyTo(PartResource res)
            {
                if(name != res.resourceName)
                    return;
                res.amount = amount;
                res.maxAmount = maxAmount;
            }
        }

        private readonly List<PayloadRes> payload_resources = new List<PayloadRes>();

        public override string GetInfo()
        {
            var info = base.GetInfo();
            if(!LaunchVelocity.IsZero())
                info += $"Jettison Velocity: {LaunchVelocity.magnitude:F1}m/s\n";
            return info;
        }

        #region IPart*Modifiers
        public float GetModuleCost(float defaultCost, ModifierStagingSituation situation) =>
            jettisoned ? -debris_cost : 0f;

        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.CONSTANTLY;

        public override float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            var dm = base.GetModuleMass(defaultMass, sit);
            return dm + (jettisoned ? -debris_mass : 0f);
        }
        #endregion

        #region IMultipleDragCube
        private static readonly string[] cube_names = { "Fairing", "Clean" };
        public string[] GetDragCubeNames() => cube_names;

        public void AssumeDragCubePosition(string anim)
        {
            find_fairings();
            if(fairings.Count == 0)
                return;
            if(anim == "Fairing")
                fairings.ForEach(f => f.gameObject.SetActive(true));
            else
                fairings.ForEach(f => f.gameObject.SetActive(false));
        }

        public bool UsesProceduralDragCubes() => false;
        public bool IsMultipleCubesActive => true;
        #endregion

        public void UpdateBaseCoMOffset(Vector3 CoMOffset)
        {
            BaseCoMOffset = CoMOffset;
            if(jettisoned)
                part.UpdateCoMOffset(BaseCoMOffset);
        }

        protected override Vector3 launchVelocity => base.launchVelocity * JettisonPower;

        private bool find_fairings()
        {
            fairings.Clear();
            fairingsSpecificMass.Clear();
            var totalFairingsMass = 0f;
            var numEqualMassFairings = 0;
            foreach(var fairing in Utils.ParseLine(Fairings, Utils.Comma))
            {
                var name_mass = fairing.Split(new[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
                var fairingName = name_mass[0];
                var fairingMass = -1f;
                if(name_mass.Length > 1)
                    float.TryParse(name_mass[1], out fairingMass);
                var transforms = part.FindModelTransforms(fairingName);
                if(transforms != null)
                {
                    fairings.AddRange(transforms);
                    foreach(var t in transforms)
                    {
                        fairingsSpecificMass.Add(t, fairingMass);
                        if(fairingMass > 0)
                            totalFairingsMass += fairingMass;
                        else
                            numEqualMassFairings += 1;
                    }
                }
                else
                    this.Warning($"Unable to find fairings transform(s): {fairingName}");
            }
            if(fairings.Count == 0)
            {
                this.ConfigurationInvalid($"No fairings were found or configured");
                return false;
            }
            if(totalFairingsMass >= 1)
            {
                this.ConfigurationInvalid($"Total specific mass of the {part.Title()} is greater than 1");
                return false;
            }
            if(numEqualMassFairings == 0)
                BaseSpecificMass = 1 - totalFairingsMass;
            if(BaseSpecificMass < 0.1f)
                this.Warning("Specific mass of the base is less then 10%");
            // calculate specific masses of fairings that didn't provide them
            // ReSharper disable once InvertIf
            if(numEqualMassFairings > 0)
            {
                var fairingMass = (1 - BaseSpecificMass - totalFairingsMass) / numEqualMassFairings;
                foreach(var t in fairingsSpecificMass.Keys.ToArray())
                {
                    if(fairingsSpecificMass[t] < 0)
                        fairingsSpecificMass[t] = fairingMass;
                }
            }
            return true;
        }

        protected override void on_storage_add(HangarStorage new_storage)
        {
            base.on_storage_add(new_storage);
            new_storage.OnVesselStored += on_ship_stored;
            new_storage.OnVesselRemoved += on_ship_removed;
            new_storage.OnVesselUnfittedAdded += on_ship_stored;
            new_storage.OnVesselUnfittedRemoved += on_ship_removed;
            new_storage.OnStorageEmpty += on_storage_empty;
        }

        [SuppressMessage("ReSharper", "DelegateSubtraction")]
        protected override void on_storage_remove(HangarStorage old_storage)
        {
            base.on_storage_remove(old_storage);
            old_storage.OnVesselStored -= on_ship_stored;
            old_storage.OnVesselRemoved -= on_ship_removed;
            old_storage.OnVesselUnfittedAdded -= on_ship_stored;
            old_storage.OnVesselUnfittedRemoved -= on_ship_removed;
            old_storage.OnStorageEmpty -= on_storage_empty;
        }

        private void disable_decouplers(string nodeId)
        {
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach(var m in part.FindModulesImplementing<ModuleDecouplerBase>())
            {
                if(m.explosiveNodeID == nodeId)
                    m.EnableModule(false);
            }
        }

        protected override void early_setup(StartState state)
        {
            base.early_setup(state);
            NoGUI = true;
            LaunchWithPunch = true;
            PayloadFixedInFlight = true;
            update_crew_capacity(CrewCapacity);
            Events["EditName"].active = false;
            FX = part.findFxGroup(FxGroup);
            //setup fairings
            if(!find_fairings())
                return;
            if(jettisoned)
            {
                fairings.ForEach(f => f.gameObject.SetActive(false));
                part.DragCubes.SetCubeWeight("Fairing ", 0f);
                part.DragCubes.SetCubeWeight("Clean ", 1f);
                part.UpdateCoMOffset(BaseCoMOffset);
                part.stagingIcon = string.Empty;
                SetStaging(false);
                part.UpdateStageability(true, true);
            }
            else
            {
                fairings.ForEach(f => f.gameObject.SetActive(true));
                part.DragCubes.SetCubeWeight("Fairing ", 1f);
                part.DragCubes.SetCubeWeight("Clean ", 0f);
                part.stagingIcon = "DECOUPLER_HOR";
                part.UpdateStageability(true, true);
            }
            update_PAW();
            //setup attach nodes
            decoupleNodes.Clear();
            foreach(var nodeID in Utils.ParseLine(DecoupleNodes, Utils.Comma))
            {
                var node = part.FindAttachNode(nodeID);
                if(node == null)
                    continue;
                decoupleNodes.Add(node);
                if(jettisoned)
                    disable_decouplers(node.id);
            }
            JettisonDirection.Normalize();
            if(vessel != null)
                vessel.SpawnCrew();
        }

        [SuppressMessage("ReSharper", "DelegateSubtraction")]
        public override void OnDestroy()
        {
            base.OnDestroy();
            // ReSharper disable once InvertIf
            if(Storage != null)
            {
                Storage.OnVesselStored -= on_ship_stored;
                Storage.OnVesselRemoved -= on_ship_removed;
                Storage.OnStorageEmpty -= on_storage_empty;
            }
        }

        protected override bool can_store_vessel(Vessel vsl) => false;

        protected override bool can_store_packed_vessel(PackedVessel vsl, bool in_flight)
        {
            if(!base.can_store_packed_vessel(vsl, in_flight))
                return false;
            // ReSharper disable once InvertIf
            if(Storage.VesselsCount > 0)
            {
                Utils.Message("Payload is already stored");
                return false;
            }
            return true;
        }

        private void store_payload_resources(PackedVessel payload)
        {
            if(payload_resources.Count > 0)
                return;
            var res_mass = 0.0;
            var resources = payload.resources;
            foreach(var r in resources.resourcesNames)
            {
                if(part.Resources.Contains(r))
                    continue;
                if(Globals.Instance.ResourcesBlacklist.IndexOf(r) >= 0)
                    continue;
                var res = part.Resources.Add(r,
                    resources.ResourceAmount(r),
                    resources.ResourceCapacity(r),
                    true,
                    true,
                    true,
                    true,
                    PartResource.FlowMode.Both);
                if(res == null)
                    continue;
                payload_resources.Add(new PayloadRes(res));
                resources.TransferResource(r, -res.amount);
                res_mass += res.amount * res.info.density;
            }
            payload.mass -= (float)res_mass;
            Storage.UpdateParams();
        }

        public void ResetPayloadResources()
        {
            if(payload_resources.Count == 0)
                return;
            foreach(var r in payload_resources)
            {
                var res = part.Resources.Get(r.name);
                if(res != null)
                    r.ApplyTo(res);
            }
        }

        private void restore_payload_resources(PackedVessel payload)
        {
            if(payload_resources.Count == 0)
                return;
            if(HighLogic.LoadedSceneIsEditor)
                ResetPayloadResources();
            var res_mass = 0.0;
            foreach(var r in payload_resources)
            {
                var res = part.Resources.Get(r.name);
                if(res == null)
                    continue;
                res_mass += res.amount * res.info.density;
                payload.resources.TransferResource(r.name, res.amount);
                part.Resources.Remove(res);
            }
            payload.mass += (float)res_mass;
            payload_resources.Clear();
            Storage.UpdateParams();
        }

        private void clear_payload_resources()
        {
            if(payload_resources.Count == 0)
                return;
            if(Storage != null && Storage.Ready && Storage.VesselsCount > 0)
                return;
            payload_resources.ForEach(r => part.Resources.Remove(r.name));
            payload_resources.Clear();
        }

        private void on_ship_stored(PackedVessel pc)
        {
            update_crew_capacity(pc.CrewCapacity);
            store_payload_resources(pc);
        }

        private void on_ship_removed(PackedVessel pc)
        {
            if(HighLogic.LoadedSceneIsEditor)
                update_crew_capacity(0);
            restore_payload_resources(pc);
        }

        private void on_storage_empty()
        {
            if(HighLogic.LoadedSceneIsEditor)
                update_crew_capacity(0);
            clear_payload_resources();
        }

        private void update_crew_capacity(int capacity)
        {
            part.CrewCapacity = CrewCapacity = capacity;
            if(part.partInfo != null && part.partInfo.partPrefab != null)
                part.partInfo.partPrefab.CrewCapacity = part.CrewCapacity;
            if(!HighLogic.LoadedSceneIsEditor)
                return;
            ShipConstruction.ShipConfig = EditorLogic.fetch.ship.SaveShip();
            ShipConstruction.ShipManifest =
                HighLogic.CurrentGame.CrewRoster.DefaultCrewForVessel(ShipConstruction.ShipConfig);
            if(CrewAssignmentDialog.Instance != null)
                CrewAssignmentDialog.Instance.RefreshCrewLists(ShipConstruction.ShipManifest, false, true);
            Utils.UpdateEditorGUI();
        }

        private void update_PAW()
        {
            if(jettisoned)
            {
                stagingToggleEnabledEditor = false;
                stagingToggleEnabledFlight = false;
                Events[nameof(ToggleStaging)].active = false;
                Events[nameof(LaunchVessel)].active = false;
                Events[nameof(ShowPayload)].active = false;
                Actions[nameof(LaunchVesselAction)].active = false;
                Fields[nameof(JettisonPower)].guiActive = false;
                Fields[nameof(DestroyDebrisIn)].guiActive = false;
            }
            else
            {
                stagingToggleEnabledEditor = true;
                stagingToggleEnabledFlight = true;
                Events[nameof(ToggleStaging)].active = true;
                Events[nameof(ToggleStaging)].advancedTweakable = false;
                Events[nameof(LaunchVessel)].active = true;
                Events[nameof(ShowPayload)].active = true;
                Actions[nameof(LaunchVesselAction)].active = true;
                Fields[nameof(JettisonPower)].guiActive = true;
                Fields[nameof(DestroyDebrisIn)].guiActive = true;
            }
        }

        private readonly struct ForceTarget
        {
            private static readonly Random rnd = new Random();
            private readonly Vector3 pos;
            private readonly Vector3 force;
            private readonly Rigidbody target;
            private readonly float add_torque;

            public ForceTarget(Rigidbody target, Vector3 force, Vector3 pos, float add_torque = 0)
            {
                this.add_torque = add_torque;
                this.target = target;
                this.force = target.transform.InverseTransformDirection(force);
                this.pos = target.transform.InverseTransformPoint(pos);
            }

            public void Apply(Rigidbody counterpart)
            {
                if(counterpart == null || target == null)
                    return;
                var forceW = target.transform.TransformDirection(force);
                var posW = target.transform.TransformPoint(pos);
                target.AddForceAtPosition(forceW, posW, ForceMode.Force);
                counterpart.AddForceAtPosition(-forceW, posW, ForceMode.Force);
                if(add_torque <= 0)
                    return;
                var rnd_torque = new Vector3((float)rnd.NextDouble() - 0.5f,
                    (float)rnd.NextDouble() - 0.5f,
                    (float)rnd.NextDouble() - 0.5f);
                target.AddRelativeTorque(rnd_torque * add_torque, ForceMode.VelocityChange);
            }
        }

        protected override IEnumerable<YieldInstruction> before_vessel_launch(PackedVessel vsl)
        {
            if(fairings.Count == 0 || jettisoned)
                yield break;
            //store crew
            vsl.crew.Clear();
            vsl.crew.AddRange(part.protoModuleCrew);
            //decouple surface attached parts and decoupleNodes
            var decouple = part.children
                .Where(p => p.srfAttachNode?.attachedPart == part)
                .ToList();
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach(var node in decoupleNodes)
            {
                if(node.attachedPart == null)
                    continue;
                decouple.Add(node.attachedPart == part.parent ? part : node.attachedPart);
                disable_decouplers(node.id);
            }
            var jettison = new List<ForceTarget>(decouple.Count);
            var jettisonPower = JettisonPower <= 1
                ? Mathf.LerpUnclamped(MinJettisonPower, 1, JettisonPower)
                : JettisonPower;
            var jettisonForce = JettisonForce * jettisonPower / 2;
            var jettisonTorque = JettisonTorque * jettisonPower;
            foreach(var p in decouple)
            {
                var force_target = p;
                if(p == part)
                    force_target = part.parent;
                p.decouple();
                if(force_target.Rigidbody != null)
                {
                    var pos = force_target.Rigidbody.worldCenterOfMass;
                    var minForce = (float)(force_target == part.parent
                                       ? vessel.totalMass - part.MassWithChildren()
                                       : force_target.MassWithChildren())
                                   / TimeWarp.fixedDeltaTime;
                    var force = (pos - part.Rigidbody.worldCenterOfMass).normalized
                                * Utils.Clamp(jettisonForce, minForce, minForce * 10);
                    jettison.Add(new ForceTarget(force_target.Rigidbody, force, pos));
                }
                yield return null;
                force_target.vessel.IgnoreGForces(10);
            }
            //apply force to decoupled parts and wait for them to clear away
            if(jettison.Count > 0)
            {
                FX?.Burst();
                jettison.ForEach(j => j.Apply(part.Rigidbody));
                yield return new WaitForSeconds(3);
            }
            //spawn debris
            jettison.Clear();
            debris.Clear();
            debris_cost = 0;
            debris_mass = 0;
            var partMass = part.Rigidbody.mass - vsl.mass - part.resourceMass;
            var debrisDestroyCountdown = Utils.ClampL(DestroyDebrisIn, 1);
            foreach(var f in fairings)
            {
                var m = partMass * fairingsSpecificMass[f];
                var d = Debris.SetupOnTransform(part, f, m, FairingsCost, DebrisLifetime);
                var force = f.TransformDirection(JettisonDirection) * jettisonForce;
                var pos = d.Rigidbody.worldCenterOfMass;
                if(!JettisonForcePos.IsZero())
                    pos += f.TransformVector(JettisonForcePos);
                jettison.Add(new ForceTarget(d.Rigidbody, force, pos, jettisonTorque));
                if(DestroyDebrisIn > 0)
                    d.selfDestruct = debrisDestroyCountdown;
                d.DetectCollisions(false);
                d.vessel.IgnoreGForces(10);
                debris_cost += FairingsCost;
                debris_mass += d.Rigidbody.mass;
                if(DestroyDebrisIn > 0)
                    d.selfDestructPower = explosionPower(d.Rigidbody);
                debris.Add(d);
            }
            vessel.IgnoreGForces(10);
            //apply force to spawned debris
            jettison.ForEach(j => j.Apply(part.Rigidbody));
            //update drag cubes
            part.DragCubes.SetCubeWeight("Fairing ", 0f);
            part.DragCubes.SetCubeWeight("Clean ", 1f);
            part.DragCubes.ForceUpdate(true, true, true);
            //this event is catched by FlightLogger
            StartCoroutine(CallbackUtil.DelayedCallback(10, update_debris_after_launch));
            GameEvents.onStageSeparation.Fire(new EventReport(FlightEvents.STAGESEPARATION,
                part,
                vsl.name,
                vessel.GetDisplayName(),
                vessel.currentStage,
                $"{vsl.name} separated from {vessel.GetDisplayName()}"));
            FX?.Burst();
            if(DestroyDebrisIn > 0 && vessel.Parts.Count == 1 && vessel.Parts.First() == part)
                StartCoroutine(self_destruct(debrisDestroyCountdown));
            part.UpdateCoMOffset(Vector3.Lerp(
                BaseCoMOffset,
                part.CoMOffset,
                vsl.mass / (part.Rigidbody.mass - debris_mass)));
            jettisoned = true;
            update_PAW();
        }

        private IEnumerator<YieldInstruction> self_destruct(float countdown)
        {
            var endUT = Planetarium.GetUniversalTime() + countdown;
            while(Planetarium.GetUniversalTime() < endUT)
                yield return null;
            FXMonger.Explode(part, vessel.GetWorldPos3D(), explosionPower(part.Rigidbody));
            yield return null;
            part.Die();
        }

        private static float explosionPower(Rigidbody rb) => Utils.Clamp(rb.mass - 0.1f, 0, 5);

        private void update_debris_after_launch()
        {
            debris.ForEach(d =>
            {
                if(d == null || d.Rigidbody == null)
                    return;
                d.DetectCollisions(true);
            });
            debris.Clear();
        }

        private ConfigNode flightPlanNode;
        private Vector3d orbitalVelocityAfterNode;

        protected override void on_vessel_loaded(Vessel vsl)
        {
            base.on_vessel_loaded(vsl);
            //transfer the target and controls
            vsl.protoVessel.targetInfo = vessel.BackupVessel().targetInfo;
            vsl.ResumeTarget();
            vsl.ctrlState.CopyFrom(vessel.ctrlState);
            vsl.ActionGroups.CopyFrom(vessel.ActionGroups);
            //save the flight plan
            flightPlanNode = null;
            if(vessel.patchedConicSolver != null
               && vessel.patchedConicSolver.maneuverNodes.Count > 0)
            {
                flightPlanNode = new ConfigNode();
                vessel.patchedConicSolver.Save(flightPlanNode);
                var nearest_node = vessel.patchedConicSolver.maneuverNodes[0];
                orbitalVelocityAfterNode = nearest_node.nextPatch.getOrbitalVelocityAtUT(nearest_node.UT);
                vessel.flightPlanNode.ClearData();
                vessel.patchedConicSolver.maneuverNodes.Clear();
            }
            //turn controls off
            vessel.ctrlState.Neutralize();
        }

        protected override void process_on_vessel_launched_data(BaseEventDetails data)
        {
            base.process_on_vessel_launched_data(data);
            data.Set<bool>("fromFairings", true);
        }

        protected override void on_vessel_launched(Vessel vsl)
        {
            part.UpdateCoMOffset(BaseCoMOffset);
            FlightInputHandler.ResumeVesselCtrlState(vsl);
            //transfer the flight plan
            if(flightPlanNode != null && vsl.patchedConicSolver != null)
            {
                var max_tries = 10;
                vsl.flightPlanNode = flightPlanNode;
                vsl.patchedConicSolver.Load(flightPlanNode);
                vsl.patchedConicSolver.UpdateFlightPlan();
                vsl.StartCoroutine(CallbackUtil.WaitUntil(
                    () => vsl.patchedConicSolver.maneuverNodes.Count > 0 || max_tries-- < 0,
                    () =>
                    {
                        if(vsl.patchedConicSolver.maneuverNodes.Count == 0)
                            return;
                        var nearest_node = vsl.patchedConicSolver.maneuverNodes[0];
                        var o = nearest_node.patch;
                        var orbitalDeltaV = orbitalVelocityAfterNode - o.getOrbitalVelocityAtUT(nearest_node.UT);
                        nearest_node.DeltaV = Utils.Orbital2NodeDeltaV(o, orbitalDeltaV, nearest_node.UT);
                        vsl.patchedConicSolver.UpdateFlightPlan();
                    }));
            }
            //disable storage, launch event and action
            Storage.EnableModule(false);
            update_crew_capacity(0);
            base.on_vessel_launched(vsl);
        }

        private IEnumerator<YieldInstruction> delayed_launch()
        {
            //check state
            if(!HighLogic.LoadedSceneIsFlight || Storage == null || !Storage.Ready)
                yield break;
            if(Storage.VesselsCount == 0)
            {
                Utils.Message("No payload");
                yield break;
            }
            if(gates_state != AnimatorState.Opened && hangar_gates != null && !hangar_gates.Playing)
                yield break;
            //set the flag and wait for the doors to open
            Events[nameof(ShowPayload)].active = false;
            launch_in_progress = true;
            if(hangar_gates != null)
                while(hangar_gates.Playing)
                    yield return null;
            //activate the hangar, get the vessel from the storage, set its crew
            Activate();
            //try to restore vessel and check the result
            if(!TryRestoreVessel(Storage.GetVessels()[0]))
            {
                //if jettisoning has failed, deactivate the part
                part.deactivate();
                Events[nameof(ShowPayload)].active = true;
            }
            //otherwise on resume the part is activated automatically
            launch_in_progress = false;
        }

        [KSPEvent(guiActive = true,
            guiName = "Show Payload",
            guiActiveUnfocused = true,
            externalToEVAOnly = false,
            unfocusedRange = 300)]
        public void ShowPayload()
        {
            if(Storage.VesselsCount <= 0)
                return;
            if(highlighted_content == null)
                HighlightContentTemporary(Storage.GetVessels()[0], 5, ContentState.Fits);
            else
                SetHighlightedContent(null);
        }

        [KSPEvent(guiActive = true,
            guiName = "Jettison Payload",
            guiActiveUnfocused = true,
            externalToEVAOnly = true,
            unfocusedRange = 4)]
        public void LaunchVessel()
        {
            if(launch_in_progress)
                return;
            Open();
            StartCoroutine(delayed_launch());
        }

        [KSPAction("Jettison Payload")]
        public void LaunchVesselAction(KSPActionParam param) => LaunchVessel();

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
            // ReSharper disable once InvertIf
            if(payload_node != null)
            {
                foreach(var rn in payload_node.GetNodes("RESOURCE"))
                {
                    var res = ConfigNodeObject.FromConfig<PayloadRes>(rn);
                    if(res != null)
                        payload_resources.Add(res);
                }
            }
        }
    }

    public class HangarFairingsUpdater : ModuleUpdater<HangarFairings>
    {
        protected override void on_rescale(ModulePair<HangarFairings> mp, Scale scale)
        {
            mp.module.JettisonForce = mp.base_module.JettisonForce * scale.absolute.volume;
            mp.module.FairingsCost = mp.base_module.FairingsCost * scale.absolute.volume;
            mp.module.UpdateBaseCoMOffset(scale.ScaleVector(mp.base_module.BaseCoMOffset));
            if(HighLogic.LoadedSceneIsEditor)
                mp.module.ResetPayloadResources();
        }
    }
}
