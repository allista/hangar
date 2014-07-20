//This code is based on the code from KAS plugin. KASModuleAttachCore class.
using System;
using System.Collections.Generic;
using UnityEngine;

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
		[KSPField (isPersistant = true)] float breakForce = 1e6f; //which is better: overkill or Kraken reaping out the anchor?
		
		//Sounds
        [KSPField] public string attachSndPath = "Hangar/Sounds/anchorAttach";
        [KSPField] public string detachSndPath = "Hangar/Sounds/anchorDetach";
        public FXGroup fxSndAttach, fxSndDetach;
		
		
		IEnumerator<YieldInstruction> check_joint()
		{
			while(true)
			{
				if(StaticAttach.connectedGameObject &&
				   !StaticAttach.connectedGameObject.GetComponent<FixedJoint>())
				{
					FlightScreenMessager.showMessage("The anchor was ripped of the ground.", 3);
					Detach();
				}
				yield return new WaitForSeconds(0.5f);
			}
		}
		
		public override void OnAwake()
		{
			base.OnAwake ();
			StartCoroutine(check_joint());
		}
		
		public override void OnStart(PartModule.StartState state)
		{
			base.OnStart (state);
			Utils.createFXSound(this.part, fxSndAttach, attachSndPath, false);
			Utils.createFXSound(this.part, fxSndDetach, detachSndPath, false);
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
			StopCoroutine("check_joint");
        }
		
		void OnPartPack() { DestroyAnchor(); }

        void OnPartUnpack() { if(isAttached) Attach(); }
		
		
		private void ToggleAttachButton()
		{
			Events["Attach"].active = !isAttached;
			Events["Detach"].active = isAttached;
		}
		
		private bool CanAttach()
		{
			//always check relative velocity and acceleration
			if(!vessel.Landed) 
			{
				FlightScreenMessager.showMessage("There's nothing to attach the anchor to", 3);
				return false;
			}
			if(vessel.GetSrfVelocity().magnitude > 1f) 
			{
				FlightScreenMessager.showMessage("Cannot attach the anchor while mooving quicker than 1m/s", 3);
				return false;
			}
			return true;
		}
		
		[KSPEvent (guiActive = true, guiName = "Attach anchor", active = true)]
        public void Attach()
        {
			if(!CanAttach()) { Detach(); return; }
			
            if(StaticAttach.connectedGameObject) Destroy(StaticAttach.connectedGameObject);
            GameObject obj = new GameObject("AnchorBody");
            obj.AddComponent<Rigidbody>();
            obj.rigidbody.isKinematic = true;
            obj.transform.position = this.part.transform.position;
            obj.transform.rotation = this.part.transform.rotation;
            StaticAttach.connectedGameObject = obj;

            if(StaticAttach.fixedJoint) Destroy(StaticAttach.fixedJoint);
            FixedJoint CurJoint = obj.AddComponent<FixedJoint>();
            CurJoint.breakForce = breakForce;
            CurJoint.breakTorque = breakForce;
            CurJoint.connectedBody = this.part.rigidbody;
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