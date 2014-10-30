using UnityEngine;
using System.Collections.Generic;

namespace AtHangar
{
	class WheelFrictionChanger
	{ 
		public readonly WheelCollider Collider;
		readonly float forwardStiffness, sidewaysStiffness;

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
		}

		public void RestoreWheel()
		{ SetStiffness(forwardStiffness, sidewaysStiffness); }
	}

	public class WheelUpdater : PartModule
	{
		ModuleWheel module;
		readonly List<WheelFrictionChanger> saved_wheels = new List<WheelFrictionChanger>();
		int last_id;

		void OnDestroy() { RestoreWheels(); }

		public void StiffenWheels() 
		{ saved_wheels.ForEach(w => w.SetStiffness(1, 1)); }

		public void RestoreWheels()
		{ saved_wheels.ForEach(w => w.RestoreWheel()); }

		bool setup()
		{
			if(module != null) return true;
			module = part.GetModule<ModuleWheel>();
			if(module == null) return false;
			module.wheels.ForEach(w => saved_wheels.Add(new WheelFrictionChanger(w)));
			return true;
		}

		void OnCollisionEnter(Collision collision) 
		{
			if(!setup()) return;
			//check object id
			int id = collision.gameObject.GetInstanceID();
			if(id == last_id) return;
			last_id = id;
			//check part
			Part other_part = collision.gameObject.GetComponent<Part>();
			if(other_part != null && other_part.HasModule<Hangar>()) StiffenWheels();
			else RestoreWheels();
		}
	}
}

