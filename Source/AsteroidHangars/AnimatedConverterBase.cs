//   AnimatedConverterBase.cs
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
	public abstract class AnimatedConverterBase : PartModule
	{
		[KSPField] public string Title             = "Converter";
		[KSPField] public string StartEventGUIName = "Start";
		[KSPField] public string StopEventGUIName  = "Stop";
		[KSPField] public string ActionGUIName     = "Toggle";
		[KSPField] public float  RatesMultiplier   = 1f;
		[KSPField] public float  MinimumRate       = 0.1f;
		[KSPField] public float  Inertia           = 1f; //lerp fraction per second

		[KSPField(isPersistant = true)] public bool Converting;
		[KSPField(isPersistant = true)] public bool ShuttingOff;
		[KSPField(guiActiveEditor = true, guiName = "Energy Consumption", guiUnits = "ec/sec", guiFormat = "F2")]
		public float EnergyConsumption = 50f;
		protected float consumption_rate;

		[KSPField(isPersistant = true, guiActive = true, guiName = "Rate", guiFormat = "P1")]
		public float Rate;
		protected float last_rate;
		protected float next_rate = 1f;

		protected bool above_threshold { get { return consumption_rate >= MinimumRate || Rate >= 0.01f; }}

		//animation
		[KSPField] public string AnimatorID = "_none_";
		protected BaseHangarAnimator animator;
		protected KSPParticleEmitter emitter;
		protected float base_animation_speed = 1f;
		protected readonly int[] base_emission = new int[2];
		//sound
		[KSPField] public string Sound = string.Empty;
		[KSPField] public float  MaxDistance = 30f;
		[KSPField] public float  MaxVolume   = 1f;
		[KSPField] public float  MinVolume   = 0.1f;
		[KSPField] public float  MinPitch    = 0.1f;
		public FXGroup fxSound;

		protected ResourcePump socket;
		protected readonly List<AnimatedConverterBase> other_converters = new List<AnimatedConverterBase>();

		public override string GetInfo()
		{ 
			var info = "";
			info += Title+":\n";
			if(EnergyConsumption > 0)
				info += string.Format("Energy Consumption: {0:F2}/sec\n", EnergyConsumption*RatesMultiplier);
			info += string.Format("Minimum Rate: {0:P1}\n", MinimumRate); 
			return info;
		}

		protected bool some_working
		{
			get
			{
				var working = false;
				foreach(var c in other_converters)
				{ working |= c.Converting; if(working) break; }
				return working;
			}
		}

		protected virtual void onPause()
		{
			if(fxSound.audio == null) return;
			if(fxSound.audio.isPlaying) fxSound.audio.Pause();
		}

		protected virtual void onUnpause()
		{
			if(fxSound.audio == null) return;
			if(Converting) fxSound.audio.Play();
		}

		public override void OnAwake()
		{ 
			GameEvents.onGamePause.Add(onPause);
			GameEvents.onGameUnpause.Add(onUnpause);
		}

		public virtual void OnDestroy()
		{
			GameEvents.onGamePause.Remove(onPause);
			GameEvents.onGameUnpause.Remove(onUnpause);
		}

		public override void OnActive() 
		{ 
			if(!string.IsNullOrEmpty(part.stagingIcon)) 
				StartConversion(); 
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			socket = part.CreateSocket();
			//get other converters
			if(AnimatorID != "_none_")
				other_converters.AddRange(from c in part.Modules.OfType<AnimatedConverterBase>()
										  where c != this && c.AnimatorID == AnimatorID select c);
			//initialize Animator
			emitter = part.FindModelComponents<KSPParticleEmitter>().FirstOrDefault();
			if(emitter != null) 
			{
				base_emission[0] = emitter.minEmission;
				base_emission[1] = emitter.maxEmission;
			}
			animator = part.GetAnimator(AnimatorID);
			var an = animator as HangarAnimator;
			if(an != null) base_animation_speed = an.ForwardSpeed;
			//initialize sound
			if(Sound != string.Empty)
			{
				Utils.createFXSound(part, fxSound, Sound, true, MaxDistance);
				fxSound.audio.volume = GameSettings.SHIP_VOLUME * MaxVolume;
			}
			//setup GUI fields
			Fields["EnergyConsumption"].guiName = Title+" Uses";
			Fields["Rate"].guiName              = Title+" Rate";
			Events["StartConversion"].guiName   = StartEventGUIName+" "+Title;
			Events["StopConversion"].guiName    = StopEventGUIName+" "+Title;
			Actions["ToggleConversion"].guiName = ActionGUIName+" "+Title;
			update_events();
			StartCoroutine(slow_update());
		}

		public virtual void SetRatesMultiplier(float mult)
		{ RatesMultiplier = mult; }

		#region Conversion
		protected abstract bool can_convert(bool report = false);
		protected abstract bool convert();
		protected abstract void on_start_conversion();
		protected abstract void on_stop_conversion();

		protected bool consume_energy()
		{
			if(!ShuttingOff)
			{
				socket.RequestTransfer(RatesMultiplier*EnergyConsumption*TimeWarp.fixedDeltaTime);
				if(!socket.TransferResource()) return false;
				consumption_rate = socket.Ratio;
				if(consumption_rate < MinimumRate) 
				{
					ScreenMessager.showMessage("Not enough energy");
					socket.Clear();
				}
			} else consumption_rate = 0;
			update_rate(Mathf.Min(consumption_rate, next_rate));
			return true;
		}

		protected void update_rate(float new_rate)
		{ Rate = Mathf.Lerp(Rate, new_rate, Inertia*TimeWarp.fixedDeltaTime); }

		public void FixedUpdate()
		{ 
			if(!Converting) return;
			if(!convert()) 
			{
				Rate = 0;
				Converting = false; 
				ShuttingOff = false;
				on_stop_conversion();
				update_events();
			}
			update_sound_params();
		}
		#endregion

		IEnumerator<YieldInstruction> slow_update()
		{
			while(true)
			{
				if(Rate != last_rate)
				{
					if(emitter != null)
					{
						emitter.minEmission = (int)Mathf.Ceil(base_emission[0]*Rate);
						emitter.maxEmission = (int)Mathf.Ceil(base_emission[1]*Rate);
					}
					var an = animator as HangarAnimator;
					if(an != null) an.ForwardSpeed = base_animation_speed*Rate;
					last_rate = Rate;
				}
				yield return new WaitForSeconds(0.5f);
			}
		}

		#region Events & Actions
		void enable_emitter(bool enable = true)
		{
			if(emitter == null) return;
			emitter.emit = enable;
			emitter.enabled = enable;
		}

		void update_sound_params()
		{
			if(fxSound.audio == null) return;
			fxSound.audio.pitch = Mathf.Lerp(MinPitch, 1f, Rate);
			fxSound.audio.volume = GameSettings.SHIP_VOLUME * Mathf.Lerp(MinVolume, MaxVolume, Rate);
		}

		protected virtual void update_events()
		{
			Events["StartConversion"].active = !Converting;
			Events["StopConversion"].active  =  Converting;
			update_sound_params();
			if(Converting)
			{
				enable_emitter();
				animator.Open();
				if(fxSound.audio != null)
					fxSound.audio.Play();
			}
			else if(!some_working) 
			{
				enable_emitter(false);
				animator.Close();
				if(fxSound.audio != null)
					fxSound.audio.Stop();
			}
		}

		[KSPEvent (guiActive = true, guiName = "Start Conversion", active = true)]
		public void StartConversion()
		{
			if(!can_convert(true)) return;
			Converting = true;
			next_rate = 1f;
			on_start_conversion();
			update_events();
		}

		[KSPEvent (guiActive = true, guiName = "Stop Conversion", active = true)]
		public void StopConversion()
		{ ShuttingOff = true; }

		[KSPAction("Toggle Conversion")]
		public void ToggleConversion(KSPActionParam param)
		{
			if(Converting) StopConversion();
			else StartConversion();
		}
		#endregion
	}
}

