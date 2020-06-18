//   HangarFairings.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AT_Utils;
using KSP.UI;
using KSP.UI.Screens;
using UnityEngine;
using Random = System.Random;

namespace AtHangar
{
    public class HangarFairings : Hangar, IPartCostModifier, IMultipleDragCube
    {
        [KSPField] public string Fairings = "fairings";
        [KSPField] public float FairingsDensity = 0.5f; //t/m3
        [KSPField] public float FairingsCost = 20f;  //credits per fairing
        [KSPField] public Vector3 BaseCoMOffset = Vector3.zero;
        [KSPField] public Vector3 JettisonDirection = Vector3.up;
        [KSPField] public float JettisonForce = 50f;
        [KSPField] public float JettisonTorque = 5f;
        [KSPField] public double DebrisLifetime = 600;
        [KSPField] public float DestroyDebrisIn = 10;
        [KSPField] public string DecoupleNodes = "";

        [KSPField(isPersistant = true,
            guiName = "Debris destruction",
            guiActive = true,
            guiActiveEditor = true)]
        [UI_Toggle(scene = UI_Scene.All, enabledText = "Armed", disabledText = "Disarmed")]
        public bool DebrisAutoDestroy;

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Jettison Power",
            guiFormat = "P0")]
        [UI_FloatEdit(scene = UI_Scene.All, minValue = 0, maxValue = 2)]
        public float JettisonPower = 1;

        private readonly List<Transform> fairings = new List<Transform>();
        private readonly List<AttachNode> decoupleNodes = new List<AttachNode>();

        [KSPField(isPersistant = true)] public float debris_cost, debris_mass = -1f;

        [KSPField] public string FxGroup = "decouple";
        private FXGroup FX;

        [KSPField(isPersistant = true)]
        public int CrewCapacity;

        [KSPField(isPersistant = true)]
        public bool jettisoned, launch_in_progress;

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
        public virtual float GetModuleCost(float defaultCost, ModifierStagingSituation situation) { return jettisoned ? -debris_cost : 0f; }
        public virtual ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

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
            if(fairings.Count == 0) return;
            if(anim == "Fairing")
                fairings.ForEach(f => f.gameObject.SetActive(true));
            else
                fairings.ForEach(f => f.gameObject.SetActive(false));
        }
        public bool UsesProceduralDragCubes() => false;
        public bool IsMultipleCubesActive => true;
        #endregion

        public void UpdateCoMOffset(Vector3 CoMOffset)
        {
            BaseCoMOffset = CoMOffset;
            if(jettisoned) part.CoMOffset = BaseCoMOffset;
        }

        protected override Vector3 launchVelocity => base.launchVelocity * JettisonPower;

        private void find_fairings()
        {
            fairings.Clear();
            foreach(var fairing in Utils.ParseLine(Fairings, Utils.Comma))
            {
                var transforms = part.FindModelTransforms(fairing);
                if(transforms != null) fairings.AddRange(transforms);
            }
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
            find_fairings();
            if(fairings.Count > 0)
            {
                if(jettisoned)
                {
                    fairings.ForEach(f => f.gameObject.SetActive(false));
                    part.DragCubes.SetCubeWeight("Fairing ", 0f);
                    part.DragCubes.SetCubeWeight("Clean ", 1f);
                    part.CoMOffset = BaseCoMOffset;
                    part.stagingIcon = string.Empty;
                    stagingToggleEnabledEditor = false;
                    stagingToggleEnabledFlight = false;
                    Events["ToggleStaging"].advancedTweakable = true;
                    SetStaging(false);
                    part.UpdateStageability(true, true);
                }
                else
                {
                    fairings.ForEach(f => f.gameObject.SetActive(true));
                    part.DragCubes.SetCubeWeight("Fairing ", 1f);
                    part.DragCubes.SetCubeWeight("Clean ", 0f);
                    part.stagingIcon = "DECOUPLER_HOR";
                    stagingToggleEnabledEditor = true;
                    stagingToggleEnabledFlight = true;
                    Events["ToggleStaging"].advancedTweakable = false;
                    part.UpdateStageability(true, true);
                }
            }
            else this.Log("No Fairings transforms found with the name: {}", Fairings);
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

        private bool store_payload_resources(PackedVessel payload)
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
                if(res == null)
                    continue;
                payload_resources.Add(new PayloadRes(res));
                resources.TransferResource(r, -res.amount);
                res_mass += res.amount * res.info.density;
            }
            payload.mass -= (float)res_mass;
            Storage.UpdateParams();
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

        private bool restore_payload_resources(PackedVessel payload)
        {
            if(payload_resources.Count == 0) return true;
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
            return true;
        }

        private bool clear_payload_resources()
        {
            if(payload_resources.Count == 0) return true;
            if(Storage != null && Storage.Ready && Storage.VesselsCount > 0) return false;
            payload_resources.ForEach(r => part.Resources.Remove(r.name));
            payload_resources.Clear();
            return true;
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
            ShipConstruction.ShipManifest = HighLogic.CurrentGame.CrewRoster.DefaultCrewForVessel(ShipConstruction.ShipConfig);
            if(CrewAssignmentDialog.Instance != null)
                CrewAssignmentDialog.Instance.RefreshCrewLists(ShipConstruction.ShipManifest, false, true);
            Utils.UpdateEditorGUI();
        }

        private struct ForceTarget
        {
            private static readonly Random rnd = new Random();
            public readonly Vector3 pos;
            public readonly Vector3 force;
            public readonly Rigidbody target;
            public readonly float add_torque;

            public ForceTarget(Rigidbody target, Vector3 force, Vector3 pos, float add_torque = 0)
            {
                this.add_torque = add_torque;
                this.target = target;
                this.force = force;
                this.pos = pos;
            }

            public void Apply(Rigidbody counterpart)
            {
                target.AddForceAtPosition(force, pos, ForceMode.Force);
                counterpart.AddForceAtPosition(-force, pos, ForceMode.Force);
                if(!(add_torque > 0))
                    return;
                var rnd_torque = new Vector3((float)rnd.NextDouble() - 0.5f,
                    (float)rnd.NextDouble() - 0.5f,
                    (float)rnd.NextDouble() - 0.5f);
                target.AddRelativeTorque(rnd_torque * add_torque, ForceMode.VelocityChange);
            }
        }

        protected override IEnumerable<YieldInstruction> before_vessel_launch(PackedVessel vsl)
        {
            if(fairings.Count == 0 || jettisoned) yield break;
            //store crew
            vsl.crew.Clear();
            vsl.crew.AddRange(part.protoModuleCrew);
            //decouple surface attached parts and decoupleNodes
            var decouple = new List<Part>();
            foreach(var p in part.children)
            {
                if(p.srfAttachNode != null &&
                   p.srfAttachNode.attachedPart == part)
                    decouple.Add(p);
            }
            foreach(var node in decoupleNodes)
            {
                if(node.attachedPart == null)
                    continue;
                if(node.attachedPart == part.parent)
                    decouple.Add(part);
                else
                    decouple.Add(node.attachedPart);
                disable_decouplers(node.id);
            }
            var jettison = new List<ForceTarget>(decouple.Count);
            var jettisonPower = Utils.ClampL(JettisonPower, 0.01f);
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
                    var force = (pos - part.Rigidbody.worldCenterOfMass).normalized * (Utils.ClampH(force_target.mass, 1) * jettisonForce);
                    jettison.Add(new ForceTarget(force_target.Rigidbody, force, pos));
                }
                yield return null;
            }
            //spawn debris
            debris.Clear();
            debris_cost = 0;
            var debrisDestroyCountdown = Utils.ClampL(DestroyDebrisIn, 1);
            foreach(var f in fairings)
            {
                var d = Debris.SetupOnTransform(part, f, FairingsDensity, FairingsCost, DebrisLifetime);
                var force = f.TransformDirection(JettisonDirection) * jettisonForce;
                var pos = d.Rigidbody.worldCenterOfMass;
                jettison.Add(new ForceTarget(d.Rigidbody, force, pos, jettisonTorque));
                if(DebrisAutoDestroy)
                    d.selfDestruct = debrisDestroyCountdown;
                d.DetectCollisions(false);
                d.vessel.IgnoreGForces(10);
                debris_cost += FairingsCost;
                debris.Add(d);
            }
            //apply force to spawned/decoupled objects
            jettison.ForEach(j => j.Apply(part.Rigidbody));
            //update drag cubes
            part.DragCubes.SetCubeWeight("Fairing ", 0f);
            part.DragCubes.SetCubeWeight("Clean ", 1f);
            part.DragCubes.ForceUpdate(true, true, true);
            //this event is catched by FlightLogger
            StartCoroutine(CallbackUtil.DelayedCallback(5, update_debris_after_launch));
            GameEvents.onStageSeparation.Fire(new EventReport(FlightEvents.STAGESEPARATION, part, null, null, StageManager.CurrentStage, string.Empty));
            if(FX != null) FX.Burst();
            if(DebrisAutoDestroy && vessel.Parts.Count == 1 && vessel.Parts.First() == part)
                StartCoroutine(self_destruct(debrisDestroyCountdown));
            jettisoned = true;
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
            debris_mass = 0;
            debris.ForEach(d =>
            {
                if(d == null || d.Rigidbody == null)
                    return;
                if(DebrisAutoDestroy)
                    d.selfDestructPower = explosionPower(d.Rigidbody);
                debris_mass += d.Rigidbody.mass;
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
                        if(vsl.patchedConicSolver.maneuverNodes.Count == 0) return;
                        var nearest_node = vsl.patchedConicSolver.maneuverNodes[0];
                        var o = nearest_node.patch;
                        var norm = o.GetOrbitNormal().normalized;
                        var prograde = o.getOrbitalVelocityAtUT(nearest_node.UT);
                        var orbitalDeltaV = orbitalVelocityAfterNode - prograde;
                        prograde.Normalize();
                        var radial = Vector3d.Cross(prograde, norm).normalized;
                        nearest_node.DeltaV = new Vector3d(Vector3d.Dot(orbitalDeltaV, radial),
                                                           Vector3d.Dot(orbitalDeltaV, norm),
                                                           Vector3d.Dot(orbitalDeltaV, prograde));
                        vsl.patchedConicSolver.UpdateFlightPlan();
                    }));
            }
            //disable storage, launch event and action
            Storage.EnableModule(false);
            Events["LaunchVessel"].active = Actions["LaunchVesselAction"].active = false;
            //update CoM and crew capacity
            part.CoMOffset = BaseCoMOffset;
            update_crew_capacity(0);
            base.on_vessel_launched(vsl);
        }

        private IEnumerator<YieldInstruction> delayed_launch()
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

        [KSPEvent(guiActive = true, guiName = "Show Payload", guiActiveUnfocused = true, externalToEVAOnly = false, unfocusedRange = 300)]
        public void ShowPayload()
        {
            if(Storage.VesselsCount <= 0)
                return;
            if(highlighted_content == null)
                HighlightContentTemporary(Storage.GetVessels()[0], 5, ContentState.Fits);
            else
                SetHighlightedContent(null);
        }

        [KSPEvent(guiActive = true, guiName = "Jettison Payload", guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 4)]
        public void LaunchVessel()
        {
            if(launch_in_progress) return;
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
                    if(res != null) payload_resources.Add(res);
                }
            }
        }

#if DEBUG
        //        Vector3d last_pos = Vector3d.zero;
        //        Vector3d last_opos = Vector3d.zero;
        //        public override void FixedUpdate()
        //        {
        //            base.FixedUpdate();
        //            if(debris != null && debris.Count > 0)
        //            {
        //                var d = debris[0];
        //                var delta = (d.vessel.CoM-last_pos).magnitude;
        //                var odelta = (d.orbit.pos-last_opos).magnitude;
        //                this.Log("delta pos:  {}\n" +
        //                         "delta orb:  {}\n" +
        //                         "pos-CB - orb: {}\n" +
        //                         "orbit:\n{}\n" +
        //                         "driver.offsetByFrame {}, was {}\n" +
        //                         "driver.localCoM {}", 
        //                         delta.ToString("F3"), 
        //                         odelta.ToString("F3"),
        //                         (d.vessel.CoMD-d.vessel.mainBody.position).xzy-d.orbit.pos,
        //                         d.orbit, d.vessel.orbitDriver.offsetPosByAFrame, d.vessel.orbitDriver.wasOffsetPosByAFrame,
        //                         d.vessel.orbitDriver.localCoM);
        //                last_pos = d.vessel.CoM;
        //                last_opos = d.orbit.pos;
        //            }
        //        }
#endif
    }

    public class HangarFairingsUpdater : ModuleUpdater<HangarFairings>
    {
        protected override void on_rescale(ModulePair<HangarFairings> mp, Scale scale)
        {
            mp.module.JettisonForce = mp.base_module.JettisonForce * scale.absolute.volume;
            mp.module.JettisonTorque = mp.base_module.JettisonTorque * scale.absolute.volume;
            mp.module.FairingsCost = mp.base_module.FairingsCost * scale.absolute.volume;
            mp.module.UpdateCoMOffset(scale.ScaleVector(mp.base_module.BaseCoMOffset));
            if(HighLogic.LoadedSceneIsEditor) mp.module.ResetPayloadResources();
        }
    }
}

