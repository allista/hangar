//This code is based on the code from KAS plugin. KASModuleAttachCore class.
using System;
using System.Collections.Generic;
using System.Text;
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
//		private float relaxationTime  = 6f;
		
		
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
//			StopCoroutine("relax_joint");
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
		
//		private void set_break_force(float bf)
//		{
//			try	
//			{
//				StaticAttach.fixedJoint.breakForce  = bf;
//				StaticAttach.fixedJoint.breakTorque = bf;
//			}
//			catch { return; }
//		}
//		
//		IEnumerator<YieldInstruction> relax_joint()
//		{
//			if(!StaticAttach.fixedJoint) yield break;
//			//wait for vessel loading
//			while(true)
//			{
//				bool parts_inited = true;
//				foreach(Part p in vessel.parts)
//				{
//					if(!p.started)
//					{
//						parts_inited = false;
//						break;
//					}
//				}
//				if(parts_inited) break;
//				yield return null;
//			}
//			//relax joint
//			double time = 0;
//			double startTime = Planetarium.GetUniversalTime();
//			float endBreakForce = vessel.GetTotalMass()*4;
//			float breakForce = endBreakForce*1000;
//			float deltaBreakForce = breakForce-endBreakForce;
//			while((time=Planetarium.GetUniversalTime()-startTime) <= relaxationTime)
//			{
//				breakForce = endBreakForce+deltaBreakForce*(float)(1-time/relaxationTime);
//				Debug.Log("relaxing joint: "+breakForce);
//				set_break_force(breakForce);
//				yield return new WaitForSeconds(0.1f);
//			}
//			Debug.Log("relaxing joint: "+endBreakForce);
//			set_break_force(endBreakForce);
//		}
		
		[KSPEvent (guiActive = true, guiName = "Attach anchor", active = true)]
        public void Attach()
        {
			if(!CanAttach()) { Detach(); return; }
			
//			StopCoroutine("relax_joint");
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
			
			isAttached = true;
			ToggleAttachButton();
//			StartCoroutine(relax_joint());
        }
		
		[KSPEvent (guiActive = true, guiName = "Detach anchor", active = false)]
        public void Detach()
        {
//			StopCoroutine("relax_joint");
            if(StaticAttach.fixedJoint) Destroy(StaticAttach.fixedJoint);
            if(StaticAttach.connectedGameObject) Destroy(StaticAttach.connectedGameObject);
            StaticAttach.fixedJoint = null;
            StaticAttach.connectedGameObject = null;
			
			isAttached = false;
			ToggleAttachButton();
        }
		
		[KSPAction("Attach anchor")]
        public void AttachAction(KSPActionParam param) { Attach(); }
		
		[KSPAction("Detach anchor")]
        public void DetachAction(KSPActionParam param) { Detach(); }
    }
}