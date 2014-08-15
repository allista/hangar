#if DEBUG
using UnityEngine;
using System.Collections.Generic;

namespace AtHangar
{
	public class ModuleWheelDebug : ModuleWheel
	{
		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			List<WheelCollider> colliders = new List<WheelCollider>(gameObject.GetComponents<WheelCollider>());
			colliders.AddRange(gameObject.GetComponentsInChildren<WheelCollider>());
			foreach(WheelCollider c in colliders)
				Utils.Log("collider {0}", c.name);
		}

		void OnCollisionEnter(Collision collision) 
		{
			Utils.Log("Collision detected with {0}", collision.gameObject.name);
			Utils.Log("Collider is {0}", collision.collider);
			foreach(ContactPoint c in collision.contacts)
			{
				Utils.Log("Contact point: this collider {0}; other collider {1}", c.thisCollider, c.otherCollider); 
				Utils.Log("other collider tag: {0}", c.otherCollider.tag);
				Utils.Log("Other pmat: bounce {0}, dynFric {1}, statFric {2}", 
					c.otherCollider.material.bounciness, 
					c.otherCollider.material.dynamicFriction, 
					c.otherCollider.material.staticFriction);
				Utils.Log("Other pmat: bounce-comb {0}, Fric-comb {1}, dynFric2 {2}, statFric2 {3}, FricDir2 {4}", 
					c.otherCollider.material.bounceCombine, 
					c.otherCollider.material.frictionCombine, 
					c.otherCollider.material.dynamicFriction2,
					c.otherCollider.material.staticFriction2,
					c.otherCollider.material.frictionDirection2);
				foreach(Wheel w in wheels)
				{
					Utils.Log("Wheel {0}", w.wheelName);
					Utils.Log("forwardFriction.stiffness {0}, sidewaysFriction.stiffness {1}",
						w.whCollider.forwardFriction.stiffness, w.whCollider.sidewaysFriction.stiffness);
					Utils.Log("forwardFriction.asymptote {0}, sidewaysFriction.asymptote {1}",
						w.whCollider.forwardFriction.asymptoteValue, w.whCollider.sidewaysFriction.asymptoteValue);
					Utils.Log("forwardFriction.a-slip {0}, sidewaysFriction.a-slip {1}",
						w.whCollider.forwardFriction.asymptoteSlip, w.whCollider.sidewaysFriction.asymptoteSlip);
					Utils.Log("forwardFriction.extremum {0}, sidewaysFriction.extremum {1}",
						w.whCollider.forwardFriction.extremumValue, w.whCollider.sidewaysFriction.extremumValue);
					Utils.Log("forwardFriction.e-slip {0}, sidewaysFriction.e-slip {1}",
						w.whCollider.forwardFriction.extremumSlip, w.whCollider.sidewaysFriction.extremumSlip);
				}
			}
		}
	}
}
#endif