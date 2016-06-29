//   WheelUpdater.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using UnityEngine;
using System.Collections.Generic;
using AT_Utils;

namespace AtHangar
{
	class WheelFrictionChanger
	{ 
		public readonly WheelCollider Collider;
		readonly WheelFrictionCurve forwardFriction, sidewaysFriction;

		public WheelFrictionChanger(Wheel w)
		{ 
			Collider = w.whCollider;
			forwardFriction  = Collider.forwardFriction; 
			sidewaysFriction = Collider.sidewaysFriction; 
		}

		public void SetFriction(float val)
		{
			var ff = Collider.forwardFriction;
			ff.stiffness = val; 
			Collider.forwardFriction = ff;
			var sf = Collider.sidewaysFriction;
			sf.stiffness = val; 
			Collider.sidewaysFriction = sf;
			#if DEBUG
			Utils.Log("WheelFriction [{0}]: fF {1} sF {2} feS {3}, feV {4} faS {5} faV {6}", 
					  Collider.GetInstanceID(), 
					  Collider.forwardFriction.stiffness, 
					  Collider.sidewaysFriction.stiffness,
					  Collider.forwardFriction.extremumSlip,
					  Collider.forwardFriction.extremumValue,
					  Collider.forwardFriction.asymptoteSlip,
					  Collider.forwardFriction.asymptoteValue);
			#endif
		}

		public void RestoreWheel()
		{ 
			Collider.forwardFriction = forwardFriction;
			Collider.sidewaysFriction = sidewaysFriction;
		}
	}

	public class WheelUpdater : PartModule
	{
		ModuleWheel module;
		readonly List<WheelFrictionChanger> saved_wheels = new List<WheelFrictionChanger>();
		int last_id;

		void OnDestroy() { RestoreWheels(); }

		public void StiffenWheels(float val) 
		{ saved_wheels.ForEach(w => w.SetFriction(val)); }

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
			var other_part = collision.gameObject.GetComponent<Part>();
			if(other_part != null && other_part.HasModule<HangarMachinery>()) StiffenWheels(1);
			else RestoreWheels();
		}
	}
}

