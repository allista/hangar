using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AtHangar
{
	class WheelFrictionInfo
	{ 
		readonly Wheel wheel;
		readonly float forwardStiffness, sidewaysStiffness;

		public WheelFrictionInfo(Wheel w)
		{ 
			wheel = w;
			forwardStiffness  = w.whCollider.forwardFriction.stiffness; 
			sidewaysStiffness = w.whCollider.sidewaysFriction.stiffness; 
		}

		public void SetStiffness(float f, float s)
		{
			if(wheel.whCollider == null) return;
			WheelFrictionCurve ff = wheel.whCollider.forwardFriction;
			ff.stiffness = f; wheel.whCollider.forwardFriction = ff;
			WheelFrictionCurve sf = wheel.whCollider.sidewaysFriction;
			sf.stiffness = s; wheel.whCollider.sidewaysFriction = sf;
		}

		public void RestoreWheel()
		{ SetStiffness(forwardStiffness, sidewaysStiffness); }
	}

	public class WheelUpdater : PartModule
	{
		ModuleWheel module;
		readonly HashSet<uint> trigger_objects = new HashSet<uint>();
		readonly List<WheelFrictionInfo> saved_wheels = new List<WheelFrictionInfo>();
		int last_id;

		#region Methods
		public override void OnStart(StartState state) { setup(); }

		void OnDestroy() { RestoreWheels(); }

		bool setup()
		{
			if(module != null) return true;
			module = part.Modules.OfType<ModuleWheel>().FirstOrDefault();
			if(module == null) return false;
			foreach(Wheel w in module.wheels)
				saved_wheels.Add(new WheelFrictionInfo(w));
			return true;
		}

		public void StiffenWheels() 
		{ foreach(WheelFrictionInfo wi in saved_wheels) wi.SetStiffness(1, 1); }

		public void RestoreWheels()
		{ foreach(WheelFrictionInfo wi in saved_wheels) wi.RestoreWheel(); }

		public void RegisterTrigger(uint id) 
		{ 
			bool new_trigger = trigger_objects.Add(id);
			if(!setup()) return;
			if(new_trigger)	StiffenWheels();
		}

		void OnCollisionEnter(Collision collision) 
		{
			if(!setup()) return;
			//check object id
			int id = collision.gameObject.GetInstanceID();
			if(id == last_id) return;
			last_id = id;
			//check part
			Part other_part = collision.gameObject.GetComponents<Part>().FirstOrDefault();
			if(other_part != null && trigger_objects.Contains(other_part.flightID)) StiffenWheels();
			else RestoreWheels();
		}
		#endregion
	}
}

