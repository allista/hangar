//   LoopAnimateThrottle.cs
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
	/// <summary>
	/// Unlike FXModuleAnimateThrottle this animator plays its animation continuosly
	/// in loop, but derives animation's speed from engine's throttle
	/// </summary>
	public class LoopAnimatedThrottle : HangarAnimator
	{
		[KSPField] public string engineID;
		EngineWrapper engine;

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
			//no interface
			OpenEventGUIName  = string.Empty;
			CloseEventGUIName = string.Empty;
			ActionGUIName     = string.Empty;
			//no energy consumption
			EnergyConsumption = 0f;
			//force loop
			Loop = true;
			//force drag
			DragMultiplier = 1f;
			//start animation
			update_speed_multiplier();
			Open();
		}

		public override void Update()
		{
			if(speed_multiplier == 0 && !engine.isOperational) return;
			update_speed_multiplier();
			base.Update();
		}

		void update_speed_multiplier()
		{
			var target = engine.isOperational ? engine.currentThrottle : 0;
			if(engine.useEngineResponseTime)
			{
				var speed = speed_multiplier < target? engine.engineAccelerationSpeed : engine.engineDecelerationSpeed;
				speed_multiplier = Mathf.Lerp(speed_multiplier, target, speed * TimeWarp.fixedDeltaTime);
			}
			else speed_multiplier = target;
		}
	}

	public class EngineWrapper
	{
		readonly IEngineStatus   engineS;
		readonly ModuleEngines   engine;
		readonly ModuleEnginesFX engineFX;
		readonly bool eFX;

		public bool  Valid { get { return engine != null || engineFX != null; } }
		public bool  isOperational { get { return engineS.isOperational; } }
		public string engineID { get { return eFX? engineFX.engineID : engine.engineID; }}
		public float currentThrottle         { get { return eFX? engineFX.currentThrottle : engine.currentThrottle; }}
		public float finalThrust             { get { return eFX? engineFX.finalThrust : engine.finalThrust; }}
		public float engineAccelerationSpeed { get { return eFX? engineFX.engineAccelerationSpeed : engine.engineAccelerationSpeed; }}
		public float engineDecelerationSpeed { get { return eFX? engineFX.engineDecelerationSpeed : engine.engineDecelerationSpeed; }}
		public bool  useEngineResponseTime   { get { return eFX? engineFX.useEngineResponseTime : engine.useEngineResponseTime; }}
		public List<Transform> thrustTransforms { get { return eFX? engineFX.thrustTransforms : engine.thrustTransforms; }}

		public float minThrust
		{ 
			get { return eFX? engineFX.minThrust : engine.minThrust; }
			set { if(eFX) engineFX.minThrust = value; else engine.minThrust = value; }
		}

		public float maxThrust
		{ 
			get { return eFX? engineFX.maxThrust : engine.maxThrust; }
			set { if(eFX) engineFX.maxThrust = value; else engine.maxThrust = value; }
		}

		public EngineWrapper(IEngineStatus engine_status)
		{
			engineS  = engine_status;
			engine   = engineS as ModuleEngines;
			engineFX = engineS as ModuleEnginesFX;
			eFX = engineFX != null;
		}
	}
}

