//   SingleUseGrappleNode.cs
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
	public class SingleUseGrappleNode : PartModule
	{
		public enum State { Idle, Armed, Docked, Fixed };

		[KSPField(isPersistant = true, guiName = "State", guiActive = true)] 
		public State state;
		uint dockedPartUId;

		[KSPField] public float GrappleEnergyConsumption = 1;
		[KSPField] public float GrappleForce = 50;
		[KSPField] public float GrappleRange = 5;
		[KSPField] public float DockRange = 0.8f;
		[KSPField] public float DockMaxVel = 2f;
		float DockRangeSqr, GrappleRangeSqr;
		ResourcePump socket;
		bool can_dock;

		[KSPField] public string GrappleTransforms = "grapple";
		List<Transform> grapple_transforms = new List<Transform>();

		[KSPField] public string FixAnimatorID = "_none_";
		MultiAnimator fixAnimator;

		[KSPField] public string ArmAnimatorID = "_none_";
		MultiAnimator armAnimator;

		DockedVesselInfo this_vessel;
		DockedVesselInfo docked_vessel;
		SimpleWarning warning;

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
		}

		void OnDestroy()
		{
			Destroy(warning);
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
			if(this.state != State.Idle)
			{
				if(armAnimator != null) 
					armAnimator.Open();
			}
			if(is_docked) 
				StartCoroutine(CallbackUtil.DelayedCallback(3, reinforce_grapple_joint));
			if(this.state == State.Fixed)
			{
				if(fixAnimator != null)
					fixAnimator.Open();
				disable_decoupling();
			}
			//initialize socket
			if(GrappleEnergyConsumption > 0) 
				socket = part.CreateSocket();
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			//node and value names are compatible with ModuleGrappleNode
			var vinfo = node.GetNode("DOCKEDVESSEL");
			if(vinfo != null)
			{
				this_vessel = new DockedVesselInfo();
				this_vessel.Load(vinfo);
			}
			vinfo = node.GetNode("DOCKEDVESSEL_Other");
			if(vinfo != null)
			{
				docked_vessel = new DockedVesselInfo();
				docked_vessel.Load(vinfo);
			}
			if(node.HasValue("dockUId"))
				dockedPartUId = uint.Parse(node.GetValue("dockUId"));
			GrappleRangeSqr = GrappleRange*GrappleRange;
			DockRangeSqr = DockRange*DockRange;

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
			if(docked_vessel != null)
				docked_vessel.Save(node.AddNode("DOCKEDVESSEL"));
			if(this_vessel != null)
				this_vessel.Save(node.AddNode("THISVESSEL"));
		}

		#region Grappling
		Part FindContactParts()
		{
			if(part.packed) return null;
			can_dock = true;
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
					can_dock &= sqr_range < DockRangeSqr;
				}
			}
			var p = (parts.Count == num_grapples && 
			         new HashSet<Part>(parts).Count == 1? 
			         parts[0] : null);
			if(p != null && p.vessel.isEVA) p = null;
			can_dock &= p != null;
			return p;
		}

		void AddForceAlongGrapples(Part other, float force)
		{
			var num_grapples = grapple_transforms.Count;
			force /= num_grapples*2;
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
		public void DockToVessel(Part other)
		{
			this.Log("Docking to vessel: {}", other.vessel.vesselName);
			var old_vessel = vessel;
			dockedPartUId = other.flightID;
			this_vessel = new DockedVesselInfo();
			this_vessel.name = vessel.vesselName;
			this_vessel.vesselType = vessel.vesselType;
			this_vessel.rootPartUId = vessel.rootPart.flightID;
			docked_vessel = new DockedVesselInfo();
			docked_vessel.name = other.vessel.vesselName;
			docked_vessel.vesselType = other.vessel.vesselType;
			docked_vessel.rootPartUId = other.vessel.rootPart.flightID;
			GameEvents.onActiveJointNeedUpdate.Fire(vessel);
			GameEvents.onActiveJointNeedUpdate.Fire(other.vessel);
			other.vessel.SetRotation(other.vessel.transform.rotation);
			vessel.SetRotation(vessel.transform.rotation);
			vessel.SetPosition(vessel.transform.position, true);
			vessel.IgnoreGForces(10);
			if(Vessel.GetDominantVessel(vessel, other.vessel) == vessel)
				other.Couple(part);
			else part.Couple(other);
			reinforce_grapple_joint();
			//switch vessel if needed
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
			//untarget docked vessels
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
				if (other.vessel.targetObject.GetVessel() == part.vessel)
					other.vessel.targetObject = null;
			}
			//toggle events
			Events["ToggleArming"].active = false;
			Events["Decouple"].active = true;
			Events["FixGrapple"].active = true;
			state = State.Docked;
			GameEvents.onVesselWasModified.Fire(vessel);
		}

		[KSPEvent(guiName = "Decouple", active = false, guiActive = true, 
		          guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 2f)]
		public void Decouple()
		{
			var dockedPart = vessel[dockedPartUId];
			if(dockedPart == null) return;
			var parent = part.parent;
			var old_vessel = vessel;
			var referenceTransformId = vessel.referenceTransformId;
			if(parent != dockedPart)
				dockedPart.Undock(docked_vessel);
			else part.Undock(this_vessel);
			AddForceAlongGrapples(dockedPart, -GrappleForce);
			if(old_vessel == FlightGlobals.ActiveVessel)
			{
				if(old_vessel[referenceTransformId] == null)
					StartCoroutine(CallbackUtil.DelayedCallback(1, () => FlightGlobals.ForceSetActiveVessel(vessel)));
			}
			Events["ToggleArming"].active = true;
			Events["Decouple"].active = false;
			Events["FixGrapple"].active = false;
			if(armAnimator != null) 
				armAnimator.Close();
			state = State.Idle;
		}

		void reinforce_grapple_joint()
		{
			var dockedPart = vessel[dockedPartUId];
			if(dockedPart == null) return;
			if(part.parent == dockedPart && part.attachJoint != null) 
				part.attachJoint.SetUnbreakable(true, part.rigidAttachment);
			else if(dockedPart.parent == part && dockedPart.attachJoint != null) 
				dockedPart.attachJoint.SetUnbreakable(true, dockedPart.rigidAttachment);
			else 
				this.Log("Unable to find attachJoint when grappled. This should never heppen.\n" +
				         "part parent {}\n" +
				         "dockedPart {}, parent {}", 
				         part.parent, dockedPart, dockedPart != null? dockedPart.parent : null);
		}

		//deprecated part conversion
		[KSPEvent(guiName = "Force Decouple", active = false, guiActive = true, 
		          guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 2f)]
		public void ForceDecouple()
		{
			Events["ForceDecouple"].active = false;
			if(fixAnimator != null) fixAnimator.Close();
			Decouple();
		}
		#endregion

		#region Fixing
		bool is_docked
		{ 
			get 
			{ 
				if(vessel == null) return false; 
				return vessel[dockedPartUId] != null;
			} 
		}

		void disable_decoupling()
		{
			Events["Decouple"].active = false;
			Events["ToggleArming"].active = false;
			Events["FixGrapple"].active = false;
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
			disable_decoupling();
			state = State.Fixed;
			Utils.Message("The grapple was fixed permanently");
		}

		[KSPEvent(guiActive = true, guiName = "Fix Grapple Permanently", active = false)]
		public void FixGrapple()
		{ 
			if(!is_docked) 
			{ Utils.Message("Nothing to fix to"); return; }
			if(fixAnimator != null && fixAnimator.Playing)
			{ Utils.Message("Already working..."); return; }
			warning.Show(true);
		}
		#endregion

		#if DEBUG
		[KSPEvent(guiName = "Try Fix Grapple", guiActive = true, guiActiveEditor = true, active = true)]
		public void TryFixGrapple()
		{ if(fixAnimator != null) fixAnimator.Toggle(); }

		[KSPEvent(guiName = "Try Arm Grapple", guiActive = true, guiActiveEditor = true, active = true)]
		public void TryArmGrapple()
		{ if(armAnimator != null) armAnimator.Toggle(); }

        [KSPEvent(guiName = "Spawn Asteroid", guiActive = true, active = true)]
        public void MakeAsteroid()
        {
            var obt = Orbit.CreateRandomOrbitNearby(vessel.orbit);
            var seed = (uint)UnityEngine.Random.Range(0, int.MaxValue);
            var ast = DiscoverableObjectsUtil.SpawnAsteroid("Ast. N"+seed, obt, seed, UntrackedObjectClass.E, 5e5, 1e6);
            ast.vesselRef.DiscoveryInfo.SetLevel(DiscoveryLevels.Owned);
        }
		#endif

		Part target;
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
						var rel_vel = Vector3.Dot(part.Rigidbody.velocity-target.Rigidbody.velocity, 
						                          (part.Rigidbody.position-target.Rigidbody.position).normalized);
						if(Mathf.Abs(rel_vel) < DockMaxVel) DockToVessel(target);
					}
				}
				else
				{
					target = null;
					can_dock = false;
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
					socket.RequestTransfer(GrappleEnergyConsumption*TimeWarp.fixedDeltaTime);
					if(!socket.TransferResource() || socket.PartialTransfer)
					{
						target = null;
						state = State.Idle;
						if(armAnimator != null) armAnimator.Close();
					}
				}
			}
		}

		public void OnGUI() 
		{ 
			if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
			Styles.Init();
			warning.Draw("This will fix the grapple permanently. " +
				"You will not be able to decouple it ever again. " +
				"Are you sure you want to continue?");
			if(warning.Result == SimpleDialog.Answer.Yes) 
			{
				if(fixAnimator != null) fixAnimator.Open();
				StartCoroutine(delayed_disable_decoupling());
			}
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

