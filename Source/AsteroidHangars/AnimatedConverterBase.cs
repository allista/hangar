using System;
using System.Linq;
using System.Collections.Generic;

namespace AtHangar
{
	public abstract class AnimatedConverterBase : PartModule
	{
		[KSPField] public string Title             = "Converter";
		[KSPField] public string StartEventGUIName = "Start";
		[KSPField] public string StopEventGUIName  = "Stop";
		[KSPField] public string ActionGUIName     = "Toggle";
		[KSPField] public float  RatesMultiplier   = 1f;

		[KSPField(isPersistant = true)] public bool Converting;
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Energy Consumption", guiUnits = "ec/sec")] 
		public float EnergyConsumption = 50f;

		[KSPField] public string AnimatorID = "_none_";
		protected BaseHangarAnimator animator;
		protected KSPParticleEmitter emitter;
		protected readonly int[] base_emission = new int[2];

		protected ResourcePump socket;
		protected readonly List<AnimatedConverterBase> other_converters = new List<AnimatedConverterBase>();

		public override string GetInfo()
		{ 
			var info = "";
			info += Title+":\n";
			info += string.Format("Energy Consumption: {0}/sec\n", EnergyConsumption*RatesMultiplier); 
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
			animator = part.GetAnimator(AnimatorID);
			if(emitter != null) 
			{
				base_emission[0] = emitter.minEmission;
				base_emission[1] = emitter.maxEmission;
			}
			//setup GUI fields
			Fields["EnergyConsumption"].guiName = Title+" Requires";
			Events["StartConversion"].guiName   = StartEventGUIName+" "+Title;
			Events["StopConversion"].guiName    = StopEventGUIName+" "+Title;
			Actions["ToggleConversion"].guiName = ActionGUIName+" "+Title;
			update_events();
		}

		public virtual void SetRatesMultiplier(float mult)
		{ RatesMultiplier = mult; }

		protected abstract bool can_convert(bool report = false);
		protected abstract bool convert();
		protected abstract void on_start_conversion();
		protected abstract void on_stop_conversion();

		public void FixedUpdate()
		{ if(Converting && !convert()) StopConversion(); }

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

