using System.Linq;

namespace AtHangar
{
	public abstract class AnimatedConverterBase : PartModule
	{
		[KSPField(isPersistant = false)] public string Title;
		[KSPField(isPersistant = false)] public string StartEventGUIName = "Start Conversion";
		[KSPField(isPersistant = false)] public string StopEventGUIName = "Stop Conversion";
		[KSPField(isPersistant = false)] public string ActionGUIName = "Toggle Conversion";

		[KSPField(isPersistant = true)] public bool Converting;
		[KSPField] public float EnergyConsumption = 50f; // electric charge per second

		[KSPField] public string AnimatorID = "_none_";
		protected BaseHangarAnimator animator;
		protected KSPParticleEmitter emitter;
		protected readonly int[] base_emission = new int[2];

		protected ResourcePump socket;

		public override string GetInfo()
		{ 
			var info = "";
			if(Title != string.Empty) info += Title+" Converter:\n";
			info += string.Format("Energy Consumption: {0} el.u/sec\n", EnergyConsumption); 
			return info;
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			socket = part.CreateSocket();
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
			var title = Title != string.Empty? Title+": " : "";
			Events["StartConversion"].guiName   = title+StartEventGUIName;
			Events["StopConversion"].guiName    = title+StopEventGUIName;
			Actions["ToggleConversion"].guiName = title+ActionGUIName;
		}

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
			else animator.Close();
		}

		[KSPEvent (guiActive = true, guiName = "Start Conversion", active = true)]
		public void StartConversion()
		{
			if(!can_convert(true)) return;
			Converting = true;
			animator.Open();
			update_events();
		}

		[KSPEvent (guiActive = true, guiName = "Stop Conversion", active = true)]
		public void StopConversion()
		{
			Converting = false;
			on_stop_conversion();
			animator.Close();
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

