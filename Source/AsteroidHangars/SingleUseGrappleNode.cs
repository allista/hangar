//   SingleUseGrappleNode.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
    public class SingleUseGrappleNode : PartModule
    {
        public enum State
        {
            Idle,
            Armed,
            Docked,
            Fixed,
            Broken
        }

        struct Contact
        {
            public Transform ori;
            public Vector3 point;
            public bool good;
        }

        [KSPField(isPersistant = true, guiName = "State", guiActive = true)]
        public State state;
        uint dockedPartUId;

        [KSPField] public float GrappleEnergyConsumption = 1;
        [KSPField] public float GrappleForce = 50;
        [KSPField] public float GrappleRange = 5;
        [KSPField] public float DockRange = 0.8f;
        [KSPField] public float DockMaxVel = 2f;
        float DockRangeSqr, GrappleRangeSqr;
        List<Contact> contacts = new List<Contact>();
        bool can_dock;
        Part target;

        [KSPField] public string GrappleTransforms = "grapple";
        List<Transform> grapple_transforms = new List<Transform>();

        [KSPField] public string FixAnimatorID = "_none_";
        MultiAnimator fixAnimator;

        [KSPField] public string ArmAnimatorID = "_none_";
        MultiAnimator armAnimator;

        DockedVesselInfo this_vessel;
        DockedVesselInfo docked_vessel;
        SimpleWarning warning;

        AttachNode grappleNode;
        Vector3 grapplePos, grappleOrt, grappleOrt2;

        ResourcePump socket;

        public override string GetInfo()
        {
            return string.Format(
                "Grapple Range: {0:F1} m\n" +
                "Grapple Force: {1:F1} kN\n" +
                "Docking Range: {2:F1} m\n" +
                "Energy Consumption: {3:F1} ec/s",
                GrappleRange, GrappleForce, DockRange, GrappleEnergyConsumption
            );
        }

        public override void OnAwake()
        {
            base.OnAwake();
            warning = gameObject.AddComponent<SimpleWarning>();
            warning.Message = "This will fix the grapple permanently. " +
                              "You will not be able to decouple it ever again. " +
                              "Are you sure you want to continue?";
            warning.yesCallback = () =>
            {
                if(fixAnimator != null) fixAnimator.Open();
                StartCoroutine(delayed_disable_decoupling());
            };
            GameEvents.onPartJointBreak.Add(onPartJointBreak);
        }

        void OnDestroy()
        {
            Destroy(warning);
            GameEvents.onPartJointBreak.Remove(onPartJointBreak);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            //get grapple transforms
            foreach(var grapple in Utils.ParseLine(GrappleTransforms, Utils.Whitespace))
                grapple_transforms.AddRange(part.FindModelTransforms(grapple));
            //initialize animators
            armAnimator = part.GetAnimator(ArmAnimatorID);
            fixAnimator = part.GetAnimator(FixAnimatorID);
            if(IsDocked)
            {
                var dockedPart = vessel[dockedPartUId];
                if(dockedPart == part.parent)
                    setup_grapple_node(dockedPart, part);
                else
                    setup_grapple_node(dockedPart, dockedPart);
            }
            else if(this.state > State.Armed)
                this.state = State.Armed;
            if(this.state != State.Idle)
            {
                if(armAnimator != null)
                    armAnimator.Open();
            }
            if(this.state == State.Fixed)
            {
                if(fixAnimator != null)
                    fixAnimator.Open();
            }
            update_part_menu();
            //initialize socket
            if(GrappleEnergyConsumption > 0)
                socket = part.CreateSocket();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            //node and value names are compatible with ModuleGrappleNode
            //deprecated config conversion
            var vinfo = node.GetNode("THISVESSEL") ?? node.GetNode("DOCKEDVESSEL");
            if(vinfo != null)
            {
                this_vessel = new DockedVesselInfo();
                this_vessel.Load(vinfo);
            }
            //deprecated config conversion
            vinfo = node.GetNode("DOCKEDVESSEL_Other") ?? node.GetNode("DOCKEDVESSEL");
            if(vinfo != null)
            {
                docked_vessel = new DockedVesselInfo();
                docked_vessel.Load(vinfo);
            }
            if(node.HasValue("dockUId"))
                dockedPartUId = uint.Parse(node.GetValue("dockUId"));
            GrappleRangeSqr = GrappleRange * GrappleRange;
            DockRangeSqr = DockRange * DockRange;
            //load grapple attach node
            if(HighLogic.LoadedSceneIsFlight)
            {
                grappleNode = new AttachNode();
                grappleNode.size = 1;
                grappleNode.id = "grapple";
                grappleNode.rigid = true;
                grappleNode.ResourceXFeed = true;
                grappleNode.attachMethod = AttachNodeMethod.FIXED_JOINT;
                grappleNode.breakingForce = grappleNode.breakingTorque = float.PositiveInfinity;
            }
            if(node.HasValue("grapplePos"))
                grapplePos = KSPUtil.ParseVector3(node.GetValue("grapplePos"));
            if(node.HasValue("grappleOrt"))
                grappleOrt = KSPUtil.ParseVector3(node.GetValue("grappleOrt"));
            if(node.HasValue("grappleOrt2"))
                grappleOrt2 = KSPUtil.ParseVector3(node.GetValue("grappleOrt2"));
            //deprecated config conversion
            if(node.HasValue("Fixed"))
            {
                if(bool.Parse(node.GetValue("Fixed")))
                    state = State.Fixed;
                else if(dockedPartUId > 0)
                    state = State.Docked;
                else
                {
                    armAnimator = part.GetAnimator(ArmAnimatorID);
                    if(armAnimator != null &&
                       armAnimator.State == AnimatorState.Opened)
                        state = State.Armed;
                    else state = State.Idle;
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.AddValue("dockUId", this.dockedPartUId);
            if(this_vessel != null)
                this_vessel.Save(node.AddNode("DOCKEDVESSEL"));
            if(docked_vessel != null)
                docked_vessel.Save(node.AddNode("DOCKEDVESSEL_Other"));
            if(this.grappleNode != null)
            {
                node.AddValue("grapplePos", KSPUtil.WriteVector(this.grappleNode.position));
                node.AddValue("grappleOrt", KSPUtil.WriteVector(this.grappleNode.orientation));
                node.AddValue("grappleOrt2", KSPUtil.WriteVector(this.grappleNode.secondaryAxis));
            }
        }

        void update_part_menu()
        {
            switch(state)
            {
            case State.Idle:
            case State.Armed:
                Events["Decouple"].active = false;
                Events["ToggleArming"].active = true;
                Events["FixGrapple"].active = false;
                break;
            case State.Docked:
                Events["Decouple"].active = true;
                Events["ToggleArming"].active = false;
                Events["FixGrapple"].active = true;
                break;
            case State.Fixed:
                Events["Decouple"].active = false;
                Events["ToggleArming"].active = false;
                Events["FixGrapple"].active = false;
                break;
            }
        }

        #region Grappling

        Part FindContactParts()
        {
            if(part.packed) return null;
            can_dock = true;
            contacts.Clear();
            var num_grapples = grapple_transforms.Count;
            var parts = new List<Part>();
            for(int i = 0; i < num_grapples; i++)
            {
                var grapple = grapple_transforms[i];
                RaycastHit hit;
                if(Physics.Raycast(grapple.position, grapple.forward * GrappleRange, out hit, GrappleRange, LayerUtil.DefaultEquivalent))
                {
                    var sqr_range = (grapple.position - hit.point).sqrMagnitude;
                    if(sqr_range < GrappleRangeSqr)
                        parts.Add(Part.GetComponentUpwards<Part>(hit.transform.gameObject));
                    var contact = new Contact { ori = grapple, point = hit.point, good = sqr_range < DockRangeSqr };
                    can_dock &= contact.good;
                    contacts.Add(contact);
                }
            }
            var p = (parts.Count == num_grapples &&
                    new HashSet<Part>(parts).Count == 1 ?
                     parts[0] : null);
            if(p != null && p.vessel.isEVA) p = null;
            can_dock &= p != null;
            return p;
        }

        void AddForceAlongGrapples(Part other, float force)
        {
            var num_grapples = grapple_transforms.Count;
            force /= num_grapples * 2;
            for(int i = 0; i < num_grapples; i++)
            {
                var grapple = grapple_transforms[i];
                part.AddForceAtPosition(grapple.forward * force, grapple.position);
                other.AddForceAtPosition(grapple.forward * -force, grapple.position);
            }
        }

        [KSPEvent(guiName = "Toggle Grapple", active = true, guiActive = true)]
        public void ToggleArming()
        {
            if(state == State.Idle || state == State.Armed)
            {
                if(armAnimator != null) armAnimator.Toggle();
                else state = State.Armed;
            }
        }

        #endregion

        #region Docking

        //failsafe in case DockedVesselInfo is lost
        void restore_docking_info(Part docked_part)
        {
            if(this_vessel == null)
            {
                this_vessel = new DockedVesselInfo();
                this_vessel.name = vessel.vesselName;
                this_vessel.vesselType = vessel.vesselType;
                this_vessel.rootPartUId = part.flightID;
            }
            if(docked_vessel == null && docked_part != null)
            {
                docked_vessel = new DockedVesselInfo();
                docked_vessel.name = Vessel.AutoRename(vessel, vessel.vesselName);
                docked_vessel.vesselType = vessel.vesselType;
                docked_vessel.rootPartUId = docked_part.flightID;
            }
        }

        void setup_grapple_node(Part other, Part host)
        {
            if(host == part)
            {
                grappleNode.attachedPart = other;
                grappleNode.owner = part;
                part.attachNodes.Add(grappleNode);
            }
            else
            {
                grappleNode.attachedPart = part;
                grappleNode.owner = other;
                other.attachNodes.Add(grappleNode);
            }
            if(grapplePos.IsZero())
            {
                var joint_pos = Vector3.zero;
                grapple_transforms.ForEach(t => joint_pos += t.position);
                grappleNode.position = host.partTransform.InverseTransformPoint(joint_pos / grapple_transforms.Count);
                grappleNode.orientation = host.partTransform.InverseTransformDirection(part.partTransform.TransformDirection(Vector3.up));
                grappleNode.secondaryAxis = host.partTransform.InverseTransformDirection(part.partTransform.TransformDirection(Vector3.back));
            }
            else
            {
                grappleNode.position = grapplePos;
                grappleNode.orientation = grappleOrt;
                grappleNode.secondaryAxis = grappleOrt2;
            }
            grappleNode.originalPosition = grappleNode.position;
            grappleNode.originalOrientation = grappleNode.orientation;
            grappleNode.originalSecondaryAxis = grappleNode.secondaryAxis;
        }

        public void DockToPart(Part other)
        {
            this.Log("Docking to vessel: {}", other.vessel.vesselName);
            var old_vessel = vessel;
            contacts.Clear();
            dockedPartUId = other.flightID;
            // save this vessel info
            this_vessel = new DockedVesselInfo();
            this_vessel.name = vessel.vesselName;
            this_vessel.vesselType = vessel.vesselType;
            this_vessel.rootPartUId = vessel.rootPart.flightID;
            // save other vessel info
            docked_vessel = new DockedVesselInfo();
            docked_vessel.name = other.vessel.vesselName;
            docked_vessel.vesselType = other.vessel.vesselType;
            docked_vessel.rootPartUId = other.vessel.rootPart.flightID;
            // reset vessels' position and rotation
            vessel.SetPosition(vessel.transform.position, true);
            vessel.SetRotation(vessel.transform.rotation);
            other.vessel.SetPosition(other.vessel.transform.position, true);
            other.vessel.SetRotation(other.vessel.transform.rotation);
            vessel.IgnoreGForces(10);
            other.vessel.IgnoreGForces(10);
            grapplePos = Vector3.zero;
            setup_grapple_node(other, part);
            PartJoint joint;
            if(Vessel.GetDominantVessel(vessel, other.vessel) == vessel)
            {
                other.Couple(part);
                joint = other.attachJoint;
            }
            else
            {
                part.Couple(other);
                joint = part.attachJoint;
            }
            joint.SetUnbreakable(true, true);
            // add fuel lookups
            part.fuelLookupTargets.Add(other);
            other.fuelLookupTargets.Add(part);
            GameEvents.onPartFuelLookupStateChange.Fire(new GameEvents.HostedFromToAction<bool, Part>(true, other, part));
            // switch vessel if needed
            if(old_vessel == FlightGlobals.ActiveVessel)
            {
                FlightGlobals.ForceSetActiveVessel(vessel);
                FlightInputHandler.SetNeutralControls();
            }
            else if(vessel == FlightGlobals.ActiveVessel)
            {
                vessel.MakeActive();
                FlightInputHandler.SetNeutralControls();
            }
            // untarget docked vessels
            if(FlightGlobals.fetch.VesselTarget != null)
            {
                if(FlightGlobals.fetch.VesselTarget.GetVessel() == other.vessel)
                    FlightGlobals.fetch.SetVesselTarget(null, false);
            }
            if(vessel.targetObject != null)
            {
                if(vessel.targetObject.GetVessel() == other.vessel)
                    vessel.targetObject = null;
            }
            if(other.vessel.targetObject != null)
            {
                if(other.vessel.targetObject.GetVessel() == part.vessel)
                    other.vessel.targetObject = null;
            }
            // update state and part menu
            state = State.Docked;
            update_part_menu();
            GameEvents.onVesselWasModified.Fire(vessel);
        }

        [KSPEvent(guiName = "Release Grapple", active = false, guiActive = true,
                  guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 2f)]
        public void Decouple()
        {
            var dockedPart = vessel[dockedPartUId];
            if(dockedPart != null)
            {
                restore_docking_info(dockedPart);
                var parent = part.parent;
                var old_vessel = vessel;
                var referenceTransformId = vessel.referenceTransformId;
                if(parent != dockedPart)
                {
                    dockedPart.Undock(docked_vessel);
                    dockedPart.attachNodes.Remove(grappleNode);
                }
                else
                {
                    part.Undock(this_vessel);
                    part.attachNodes.Remove(grappleNode);
                }
                grappleNode.attachedPart = null;
                grappleNode.owner = null;
                part.fuelLookupTargets.Remove(dockedPart);
                dockedPart.fuelLookupTargets.Remove(part);
                GameEvents.onPartFuelLookupStateChange.Fire(new GameEvents.HostedFromToAction<bool, Part>(true, part, dockedPart));
                AddForceAlongGrapples(dockedPart, -GrappleForce);
                if(old_vessel == FlightGlobals.ActiveVessel)
                {
                    if(old_vessel[referenceTransformId] == null)
                        StartCoroutine(CallbackUtil.DelayedCallback(1, () => FlightGlobals.ForceSetActiveVessel(vessel)));
                }
            }
            if(armAnimator != null)
                armAnimator.Close();
            state = State.Idle;
            update_part_menu();
        }

        void onPartJointBreak(PartJoint joint, float force)
        {
            if(state != State.Fixed || !IsDocked) return;
            var dockedPart = vessel[dockedPartUId];
            if(joint.Parent == part && joint.Child == dockedPart ||
               joint.Parent == dockedPart && joint.Child == part)
            {
                state = State.Broken;
                StartCoroutine(CallbackUtil.DelayedCallback(3, part.explode));
            }
        }

        #endregion

        #region Fixing

        public bool IsDocked
        {
            get
            {
                return vessel != null && vessel[dockedPartUId] != null;
            }
        }

        IEnumerator<YieldInstruction> delayed_disable_decoupling()
        {
            if(fixAnimator != null)
            {
                if(fixAnimator.State != AnimatorState.Opening)
                    yield break;
                while(fixAnimator.State != AnimatorState.Opened)
                    yield return new WaitForSeconds(0.5f);
            }
            state = State.Fixed;
            update_part_menu();
            Utils.Message("The grapple was fixed permanently");
        }

        [KSPEvent(guiActive = true, guiName = "Fix Grapple Permanently", active = false)]
        public void FixGrapple()
        {
            if(!IsDocked)
            {
                Utils.Message("Nothing to fix to");
                return;
            }
            if(fixAnimator != null && fixAnimator.Playing)
            {
                Utils.Message("Already working...");
                return;
            }
            warning.Show(true);
        }

        #endregion

#if DEBUG
        [KSPEvent(guiName = "Try Fix Grapple", guiActive = true, guiActiveEditor = true, active = true)]
        public void TryFixGrapple()
        {
            if(fixAnimator != null) fixAnimator.Toggle();
        }

        [KSPEvent(guiName = "Try Arm Grapple", guiActive = true, guiActiveEditor = true, active = true)]
        public void TryArmGrapple()
        {
            if(armAnimator != null) armAnimator.Toggle();
        }

        [KSPEvent(guiName = "Spawn Asteroid", guiActive = true, active = true)]
        public void MakeAsteroid()
        {
            var obt = Orbit.CreateRandomOrbitNearby(vessel.orbit);
            var seed = (uint)UnityEngine.Random.Range(0, int.MaxValue);
            var ast = DiscoverableObjectsUtil.SpawnAsteroid("Ast. N" + seed, obt, seed, UntrackedObjectClass.E, 5e5, 1e6);
            ast.vesselRef.DiscoveryInfo.SetLevel(DiscoveryLevels.Owned);
        }
#endif

        void Update()
        {
            if(state == State.Idle && armAnimator != null && armAnimator.State == AnimatorState.Opened)
                state = State.Armed;
            else if(state == State.Armed && armAnimator != null && armAnimator.State != AnimatorState.Opened)
                state = State.Idle;
            if(HighLogic.LoadedSceneIsFlight && !FlightDriver.Pause)
            {
                if(state == State.Armed)
                {
                    target = FindContactParts();
                    if(target != null && can_dock)
                    {
                        var rel_vel = Vector3.Dot(part.Rigidbody.velocity - target.Rigidbody.velocity,
                                                  (part.Rigidbody.position - target.Rigidbody.position).normalized);
                        if(Mathf.Abs(rel_vel) < DockMaxVel) DockToPart(target);
                    }
                }
                else
                {
                    target = null;
                    can_dock = false;
                    contacts.Clear();
                }
                if(IsDocked)
                {
                    var dockedPart = vessel[dockedPartUId];
                    var joint = part.parent == dockedPart ? part.attachJoint : dockedPart.attachJoint;
                    if(joint && !(float.IsPositiveInfinity(joint.Joint.breakForce) &&
                       float.IsPositiveInfinity(joint.Joint.breakTorque)))
                        joint.SetUnbreakable(true, true);
                }
            }
        }

        void FixedUpdate()
        {
            if(target != null)
            {
                AddForceAlongGrapples(target, GrappleForce);
                if(socket != null)
                {
                    socket.RequestTransfer(GrappleEnergyConsumption * TimeWarp.fixedDeltaTime);
                    if(!socket.TransferResource() || socket.PartialTransfer)
                    {
                        target = null;
                        state = State.Idle;
                        if(armAnimator != null) armAnimator.Close();
                    }
                }
            }
        }

        static Color good_contact_color = new Color(0, 1, 0, 0.2f);
        static Color bad_contact_color = new Color(1, 0, 0, 0.2f);

        void OnRenderObject()
        {
            for(int i = 0, contactsCount = contacts.Count; i < contactsCount; i++)
            {
                var contact = contacts[i];
                Utils.GLLine(contact.ori.position, contact.point,
                             contact.good ? good_contact_color : bad_contact_color);
            }
        }

        public void OnGUI()
        {
            if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
            Styles.Init();
#if DEBUG
            if(grappleNode != null && grappleNode.owner != null)
                Utils.GLDrawPoint(grappleNode.owner.transform.TransformPoint(grappleNode.position), Color.green, r: 0.3f);
#endif
        }
    }

    public class SingleUseGrappleNodeUpdater : ModuleUpdater<SingleUseGrappleNode>
    {
        protected override void on_rescale(ModulePair<SingleUseGrappleNode> mp, Scale scale)
        {
            var linear = scale.absolute * scale.absolute.aspect;
            mp.module.GrappleEnergyConsumption = mp.base_module.GrappleEnergyConsumption * linear;
            mp.module.GrappleForce = mp.base_module.GrappleForce * linear;
            mp.module.GrappleRange = mp.base_module.GrappleRange * linear;
            mp.module.DockRange = mp.base_module.DockRange * linear;
        }
    }
}

