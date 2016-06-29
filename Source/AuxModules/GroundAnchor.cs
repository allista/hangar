//   GroundAnchor.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

//This code is based on the code from KAS plugin. KASModuleAttachCore class.
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
	public class GroundAnchor : PartModule
	{
        // Static attach handling
        public StAttach StaticAttach;
        public struct StAttach
        {
            public FixedJoint fixedJoint;
            public GameObject connectedGameObject;
        }
		
		// State
		[KSPField (isPersistant = true)] bool isAttached = false;
		[KSPField (isPersistant = true)] const float breakForce = 1e6f; //which is better: overkill or Kraken reaping out the anchor?
		
		//Sounds
        [KSPField] public string attachSndPath = "Hangar/Sounds/anchorAttach";
        [KSPField] public string detachSndPath = "Hangar/Sounds/anchorDetach";
        public FXGroup fxSndAttach, fxSndDetach;
		
		IEnumerator<YieldInstruction> joint_checker;
		IEnumerator<YieldInstruction> check_joint()
		{
			while(true)
			{
				if(StaticAttach.connectedGameObject &&
				   !StaticAttach.connectedGameObject.GetComponent<FixedJoint>())
				{
					Utils.Message("The anchor was ripped of the ground.");
					Detach();
				}
				yield return new WaitForSeconds(0.5f);
			}
		}
		
		public override void OnAwake()
		{
			base.OnAwake();
			joint_checker = check_joint();
			StartCoroutine(joint_checker);
		}
		
		public override void OnStart(PartModule.StartState state)
		{
			base.OnStart(state);
			HangarUtils.createFXSound(part, fxSndAttach, attachSndPath, false);
			HangarUtils.createFXSound(part, fxSndDetach, detachSndPath, false);
		}
		
		void DestroyAnchor()
		{
			if(StaticAttach.connectedGameObject)
                Destroy(StaticAttach.connectedGameObject);
			StaticAttach.connectedGameObject = null;
		}
		
		void OnDestroy()
        {
			DestroyAnchor();
			StopCoroutine(joint_checker);
        }
		
		void OnPartPack() { DestroyAnchor(); }

        void OnPartUnpack() { if(isAttached) Attach(); }
		
		
		void ToggleAttachButton()
		{
			Events["Attach"].active = !isAttached;
			Events["Detach"].active = isAttached;
		}
		
		bool CanAttach()
		{
			//always check relative velocity and acceleration
			if(!vessel.Landed) 
			{
				Utils.Message("There's nothing to attach the anchor to");
				return false;
			}
			if(vessel.GetSrfVelocity().magnitude > 1f) 
			{
				Utils.Message("Cannot attach the anchor while mooving quicker than 1m/s");
				return false;
			}
			return true;
		}
		
		[KSPEvent (guiActive = true, guiName = "Attach anchor", active = true)]
        public void Attach()
        {
			if(!CanAttach()) { Detach(); return; }
			
            if(StaticAttach.connectedGameObject) Destroy(StaticAttach.connectedGameObject);
            var obj = new GameObject("AnchorBody");
            var rb = obj.AddComponent<Rigidbody>();
			rb.isKinematic = true;
            obj.transform.position = part.transform.position;
            obj.transform.rotation = part.transform.rotation;
            StaticAttach.connectedGameObject = obj;

            if(StaticAttach.fixedJoint) Destroy(StaticAttach.fixedJoint);
            FixedJoint CurJoint = obj.AddComponent<FixedJoint>();
            CurJoint.breakForce = breakForce;
            CurJoint.breakTorque = breakForce;
            CurJoint.connectedBody = part.Rigidbody;
            StaticAttach.fixedJoint = CurJoint;
			
			if(!isAttached) fxSndAttach.audio.Play();
			isAttached = true;
			ToggleAttachButton();
        }
		
		[KSPEvent (guiActive = true, guiName = "Detach anchor", active = false)]
        public void Detach()
        {
            if(StaticAttach.fixedJoint) Destroy(StaticAttach.fixedJoint);
            if(StaticAttach.connectedGameObject) Destroy(StaticAttach.connectedGameObject);
            StaticAttach.fixedJoint = null;
            StaticAttach.connectedGameObject = null;
			if(isAttached) fxSndDetach.audio.Play();
			isAttached = false;
			ToggleAttachButton();
        }
		
		[KSPAction("Attach anchor")]
        public void AttachAction(KSPActionParam param) { Attach(); }
		
		[KSPAction("Detach anchor")]
        public void DetachAction(KSPActionParam param) { Detach(); }
    }
}