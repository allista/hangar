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
		ModuleEngines engine;

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			//find the engine
			engine = part.Modules.OfType<ModuleEngines>()
				.FirstOrDefault(e => e.engineID == engineID);
			if(engine == null)
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
			if(speed_multiplier.Equals(0) && !engine.isOperational) return;
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
}

