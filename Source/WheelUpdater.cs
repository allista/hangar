using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
		int last_id;

		void OnDestroy() { RestoreWheels(); }

		public void StiffenWheels() 
		{ saved_wheels.ForEach(w => w.SetStiffness(1, 1)); 
			part.SetHighlightColor(XKCDColors.LightSeaGreen);//debug
			part.SetHighlight(true); }//debug

		public void RestoreWheels()
		{ saved_wheels.ForEach(w => w.RestoreWheel()); 
			part.SetHighlightDefault(); }//debug

		public void RegisterTrigger(uint id) 
		{ 
			bool new_trigger = trigger_objects.Add(id);
			if(!setup()) return;
			if(new_trigger)	StiffenWheels();
		}

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
			Utils.Log("Detected ground hit of the wheel {0} with the game object {1}, {2}", 
				part.flightID, id, collision.gameObject.name);//debug
			//check part
			Part other_part = collision.gameObject.GetComponent<Part>();
			if(other_part != null && trigger_objects.Contains(other_part.flightID)) 
				StiffenWheels();
			else RestoreWheels();
		}
	}
}

