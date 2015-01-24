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
using UnityEngine;

namespace AtHangar
{
	/// <summary>
	/// Unlike FXModuleAnimateThrottle this animator plays its animation continuosly
	/// in loop, but derives animation's speed from engine's throttle
	/// </summary>
	public class LoopAnimatedThrottle : HangarAnimator
	{
		#region Engines Wrapper
		IEngineStatus   engineS;
		ModuleEngines   engine;
		ModuleEnginesFX engineFX;
		bool eFX;

		float currentThrottle         { get { return eFX? engineFX.currentThrottle : engine.currentThrottle; }}
		float engineAccelerationSpeed { get { return eFX? engineFX.engineAccelerationSpeed : engine.engineAccelerationSpeed; }}
		float engineDecelerationSpeed { get { return eFX? engineFX.engineDecelerationSpeed : engine.engineDecelerationSpeed; }}
		bool  useEngineResponseTime   { get { return eFX? engineFX.useEngineResponseTime : engine.useEngineResponseTime; }}
		#endregion

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			//find the first engine
			engineS  = part.Modules.OfType<IEngineStatus>().FirstOrDefault();
			engine   = engineS as ModuleEngines;
			engineFX = engineS as ModuleEnginesFX;
			if(engine == null && engineFX == null)
			{ enabled = false; isEnabled = false; return; }
			eFX = engineFX != null;
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
			if(speed_multiplier == 0 && !engineS.isOperational) return;
			update_speed_multiplier();
			base.Update();
		}

		void update_speed_multiplier()
		{
			var target = engineS.isOperational ? currentThrottle : 0;
			if(useEngineResponseTime)
			{
				var speed = speed_multiplier < target? engineAccelerationSpeed : engineDecelerationSpeed;
				speed_multiplier = Mathf.Lerp(speed_multiplier, target, speed * TimeWarp.fixedDeltaTime);
			}
			else speed_multiplier = target;
		}
	}
}

