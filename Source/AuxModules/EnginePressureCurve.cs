//   EnginePressureCurve.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class EnginePressureCurve : PartModule
	{
		[KSPField] public string engineID;
		[KSPField] public FloatCurve PressureCurve;

		[KSPField(guiActive = true, guiName = "Pressure Factor", guiFormat = "P1")]
		public float PressureFactor = 1f;

		#if DEBUG
		[KSPField(guiActive = true, guiName = "Pressure", guiFormat = "F3")]
		public float pressure;
		#endif

		EngineWrapper engine;

		public override void OnAwake()
		{
			base.OnAwake();
			if(PressureCurve == null) 
				PressureCurve = new FloatCurve();
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			//find the engine
			var engines = part.Modules.OfType<IEngineStatus>();
			foreach(var e in engines)
			{
				var _e = new EngineWrapper(e);
				if(_e.Valid && _e.engineID == engineID)
				{ engine = _e; break; }
			}
			if(engine == null || !engine.Valid)
			{ enabled = false; isEnabled = false; return; }
		}

		public override void OnFixedUpdate()
		{
			base.OnFixedUpdate();
			if(engine.finalThrust <= 0 || part.Rigidbody == null) return;
			pressure = (float)vessel.staticPressure;//debug
			PressureFactor = PressureCurve.Evaluate((float)vessel.staticPressure);
			if(PressureFactor <= 0) return;
			var transforms = engine.thrustTransforms;
			var num_transforms = transforms.Count;
			var thrust = engine.finalThrust / (float)num_transforms * (1 - PressureFactor);
			for(int i = 0; i < num_transforms; i++)
			{
				var transform = transforms[i];
				part.Rigidbody.AddForceAtPosition(transform.forward * thrust, transform.position, ForceMode.Force);
			}
		}
	}
}