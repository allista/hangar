using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AtHangar
{
	class WheelFrictionChanger
	{ 
		public readonly WheelCollider Collider;
		readonly float forwardStiffness, sidewaysStiffness;
		public int LastID;

		public WheelFrictionChanger(Wheel w)
		{ 
			Collider = w.whCollider;
			forwardStiffness  = Collider.forwardFriction.stiffness; 
			sidewaysStiffness = Collider.sidewaysFriction.stiffness; 
		}

		public void SetStiffness(float f, float s)
		{
			WheelFrictionCurve ff = Collider.forwardFriction;
			ff.stiffness = f; Collider.forwardFriction = ff;
			WheelFrictionCurve sf = Collider.sidewaysFriction;
			sf.stiffness = s; Collider.sidewaysFriction = sf;
			Utils.Log("{0} wheel: fs {1}; sf {2}", Collider.GetInstanceID(), f, s);//debug
		}

		public void RestoreWheel()
		{ SetStiffness(forwardStiffness, sidewaysStiffness); }
	}

	public class WheelUpdater : PartModule
	{
		ModuleWheel module;
		readonly HashSet<uint> trigger_objects = new HashSet<uint>();
		readonly List<WheelFrictionChanger> saved_wheels = new List<WheelFrictionChanger>();
		IEnumerator<YieldInstruction> collision_checker;

		public override void OnAwake() 
		{ 
			collision_checker = check_collisions();
			StartCoroutine(collision_checker);
		}

		void OnDestroy() 
		{ 
			RestoreWheels(); 
			StopCoroutine(collision_checker);
		}

		public void StiffenWheels() 
		{ saved_wheels.ForEach(w => w.SetStiffness(1, 1)); }

		public void RestoreWheels()
		{ saved_wheels.ForEach(w => w.RestoreWheel()); }

		public void RegisterTrigger(uint id) 
		{ 
			bool new_trigger = trigger_objects.Add(id);
			if(!setup()) return;
			if(new_trigger)	StiffenWheels();
		}

		bool setup()
		{
			if(module != null) return true;
			module = part.Modules.OfType<ModuleWheel>().FirstOrDefault();
			if(module == null) return false;
			module.wheels.ForEach(w => saved_wheels.Add(new WheelFrictionChanger(w)));
			return true;
		}

		IEnumerator<YieldInstruction> check_collisions()
		{
			WheelHit hit;
			while(true)
			{
				yield return new WaitForSeconds(0.1f);
				if(!setup()) continue;
				foreach(WheelFrictionChanger wc in saved_wheels)
				{
					if(!wc.Collider.GetGroundHit(out hit)) continue;
					Utils.Log("Detected ground hit of the wheel {0} with the game object {1}", wc.Collider.GetInstanceID(), hit.collider.gameObject.GetInstanceID());//debug
					//check object id
					var obj = hit.collider.gameObject;
					int id = obj.GetInstanceID();
					if(id == wc.LastID) continue;
					wc.LastID = id;
					//check part
					Part other_part = obj.GetComponentInChildren<Part>();
					if(other_part != null)
					{
						Utils.Log("Other_part: {0}, {1}", other_part.name, other_part.flightID);//debug
						Utils.Log("Is trigger: {0}", trigger_objects.Contains(other_part.flightID));//debug
					}
					else Utils.Log("No part was found");
					if(other_part != null && trigger_objects.Contains(other_part.flightID)) 
						wc.SetStiffness(1, 1);
					else wc.RestoreWheel();
				}
			}
		}
	}
}

