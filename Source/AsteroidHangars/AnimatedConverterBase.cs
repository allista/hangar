using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public abstract class AnimatedConverterBase : PartModule
	{
		[KSPField] public string Title               = "Converter";
		[KSPField] public string StartEventGUIName   = "Start";
		[KSPField] public string StopEventGUIName    = "Stop";
		[KSPField] public string ActionGUIName       = "Toggle";
		[KSPField] public float  RatesMultiplier     = 1f;
		[KSPField] public float  EnergyRateThreshold = 0.1f; // relative energy consumtion rate threshold

		[KSPField(isPersistant = true)] public bool Converting;
		[KSPField(guiActiveEditor = true, guiName = "Energy Consumption", guiUnits = "ec/sec")] 
		public float EnergyConsumption = 50f;

		[KSPField(guiActive = true, guiName = "Rate", guiFormat = "n1", guiUnits = "%")]
		public float RateDisplay;
		protected float rate, last_rate;

		[KSPField] public string AnimatorID = "_none_";
		protected BaseHangarAnimator animator;
		protected KSPParticleEmitter emitter;
		protected float base_animation_speed = 1f;
		protected readonly int[] base_emission = new int[2];

		protected ResourcePump socket;
		protected readonly List<AnimatedConverterBase> other_converters = new List<AnimatedConverterBase>();

		public override string GetInfo()
		{ 
			var info = "";
			info += Title+":\n";
			info += string.Format("Energy Consumption: {0:F2}/sec\n", EnergyConsumption*RatesMultiplier);
			info += string.Format("Minimum Rate: {0}\n", Utils.formatPercent(EnergyRateThreshold)); 
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

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			socket = part.CreateSocket();
			//get other converters
			if(AnimatorID != "_none_")
				other_converters.AddRange(from c in part.Modules.OfType<AnimatedConverterBase>()
										  where c != this && c.AnimatorID == AnimatorID select c);
			//initialize Animator
			part.force_activate();
			emitter  = part.GetComponentsInChildren<KSPParticleEmitter>().FirstOrDefault();
			if(emitter != null) 
			{
				base_emission[0] = emitter.minEmission;
				base_emission[1] = emitter.maxEmission;
			}
			animator = part.GetAnimator(AnimatorID);
			if(animator is HangarAnimator)
			{
				var an = animator as HangarAnimator;
				base_animation_speed = an.ForwardSpeed;
			}
			//setup GUI fields
			Fields["EnergyConsumption"].guiName = Title+" Uses";
			Fields["RateDisplay"].guiName       = Title+" Rate";
			Events["StartConversion"].guiName   = StartEventGUIName+" "+Title;
			Events["StopConversion"].guiName    = StopEventGUIName+" "+Title;
			Actions["ToggleConversion"].guiName = ActionGUIName+" "+Title;
			update_events();
			StartCoroutine(slow_update());
		}

		public virtual void SetRatesMultiplier(float mult)
		{ RatesMultiplier = mult; }

		protected abstract bool can_convert(bool report = false);
		protected abstract bool convert();
		protected abstract void on_start_conversion();
		protected abstract void on_stop_conversion();

		public void FixedUpdate()
		{ if(Converting && !convert()) StopConversion(); }

		IEnumerator<YieldInstruction> slow_update()
		{
			while(true)
			{
				if(rate != last_rate)
				{
					RateDisplay = rate*100f;
					if(emitter != null)
					{
						emitter.minEmission = (int)Mathf.Ceil(base_emission[0]*rate);
						emitter.maxEmission = (int)Mathf.Ceil(base_emission[1]*rate);
					}
					if(animator is HangarAnimator)
					{
						var an = animator as HangarAnimator;
						an.ForwardSpeed = base_animation_speed*rate;
					}
					last_rate = rate;
				}
				yield return new WaitForSeconds(0.5f);
			}
		}

		#region Events & Actions
		protected virtual void update_events()
		{
			Events["StartConversion"].active = !Converting;
			Events["StopConversion"].active  =  Converting;
			if(emitter != null)
			{
				emitter.emit = Converting;
				emitter.enabled = Converting;
			}
			if(Converting) animator.Open();
			else if(!some_working) animator.Close();
		}

		[KSPEvent (guiActive = true, guiName = "Start Conversion", active = true)]
		public void StartConversion()
		{
			if(!can_convert(true)) return;
			Converting = true;
			update_events();
		}

		[KSPEvent (guiActive = true, guiName = "Stop Conversion", active = true)]
		public void StopConversion()
		{
			Converting = false;
			on_stop_conversion();
			update_events();
		}

		[KSPAction("Toggle Conversion")]
		public void ToggleConversion(KSPActionParam param)
		{
			if(Converting) StopConversion();
			else StartConversion();
		}
		#endregion
	}
}

