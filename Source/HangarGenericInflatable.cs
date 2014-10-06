using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AtHangar
{
	public interface IControllableModule
	{
		bool CanEnable();
		bool CanDisable();
		void Enable(bool enable);
	}

	public class GasCompressor
	{
		public const string NODE_NAME = "COMPRESSOR";
		public float ConversionRate { get; set; }
		public float ConsumptionRate { get; set; }
		public float OutputFraction { get; private set; }
		Part part;

		public GasCompressor(Part p) { part = p; ConsumptionRate = 1.0f; ConversionRate = 0.05f; }

		public float CompressGas()
		{
			if(part.vessel == null || part.vessel.mainBody == null) return 0;
			if(!part.vessel.mainBody.atmosphere) return 0;
			double pressure = FlightGlobals.getStaticPressure(part.vessel.altitude, part.vessel.mainBody);
			if(pressure < 1e-6) return 0;
			var request  = ConsumptionRate*(TimeWarp.fixedDeltaTime);
			var consumed = part.RequestResource("ElectricCharge", request);
			OutputFraction = consumed/request;
			return (float)(pressure*ConversionRate*consumed);
		}

		public void Load(ConfigNode node)
		{ 
			if(node.HasValue("ConversionRate"))
				ConversionRate = float.Parse(node.GetValue("ConversionRate"));
			if(node.HasValue("ConsumptionRate"))
				ConsumptionRate = float.Parse(node.GetValue("ConsumptionRate"));
		}
	}

	public class HangarGenericInflatable : HangarAnimator
	{
		//configuration
		[KSPField(isPersistant = false)] public string ControlledModules;
		[KSPField(isPersistant = false)] public string AnimatedNodes;
		[KSPField(isPersistant = false)] public bool   PackedByDefault = true;
		[KSPField(isPersistant = false)] public float  InflatableVolume;
		[KSPField(isPersistant = true)]	 public float  CompressedGas = -1f;
		[KSPField(isPersistant = false)] public string CompressorSound = "Hangar/Sounds/Compressor";
		[KSPField(isPersistant = false)] public string InflationSound = "Hangar/Sounds/Inflate";
		[KSPField(isPersistant = false)] public float  SoundVolume = 0.2f;
		//GUI
		[KSPField (guiName = "Compressed Gas", guiActive=true)] public string CompressedGasDisplay;

		//modules and nodes
		readonly List<IControllableModule> controlled_modules = new List<IControllableModule>();
		readonly List<AnimatedNode> animated_nodes = new List<AnimatedNode>();

		//compressor
		public ConfigNode CompressorConfig = null;
		public GasCompressor Compressor { get; protected set; }
		bool has_compressed_gas { get { return CompressedGas >= InflatableVolume; } }
		public FXGroup fxSndCompressor;
		bool play_compressor;

		//metric and scale
		Part   prefab = null;
		Metric prefab_metric = null;
		Metric part_metric = null;
		protected float volume_scale = 1;

		//state
		const int skip_fixed_frames = 5;
		ModuleGUIState gui_state;
		bool just_loaded = false;

		#region Info
		public override string GetInfo()
		{ 
			if(Compressor == null) return "";
			string info = "Compressor:\n";
			info += string.Format("Rate: {0}/el.u.\n", Utils.formatVolume(Compressor.ConversionRate));
			info += string.Format("Power Consumption: {0} el.u./sec\n", Compressor.ConsumptionRate);
			return info;
		}

		IEnumerator<YieldInstruction> UpdateStatus()
		{
			while(true)
			{
				CompressedGasDisplay = string.Format("{0:F1}%", CompressedGas/InflatableVolume*100);
				yield return new WaitForSeconds(0.5f);
			}
		}
		#endregion
		
		#region Startup
		public override void OnAwake()
		{
			base.OnAwake();
			GameEvents.onEditorShipModified.Add(UpdateGUI);
			//this is nesessary as KSP initializes the node with an empty ConfigNode() somewhere
			if(CompressorConfig != null && 
				CompressorConfig.name != GasCompressor.NODE_NAME)
				CompressorConfig = null;
		}
		void OnDestroy() { GameEvents.onEditorShipModified.Remove(UpdateGUI); }

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			if(state == StartState.None) return;
			//get sound effects
			Utils.createFXSound(part, fxSndCompressor, CompressorSound, true);
			fxSndCompressor.audio.volume = GameSettings.SHIP_VOLUME * SoundVolume;
			//get controlled modules
			foreach(string module_name in ControlledModules.Split(' '))
			{
				if(module_name == "") continue;
				if(!part.Modules.Contains(module_name))
				{
					Utils.Log("HangarGenericInflatable.OnStart: {0} does not contain {1} module.", part.name, module_name);
					continue;
				}
				List<IControllableModule> modules = new List<IControllableModule>();
				foreach(PartModule pm in part.Modules) 
				{ 
					if(pm.moduleName == module_name) 
					{
						var controllableModule = pm as IControllableModule;
						if(controllableModule != null) 
						{
							modules.Add(controllableModule); 
							if(State != AnimatorState.Opened)
								controllableModule.Enable(false);
						}
						else Utils.Log("HangarGenericInflatable.OnStart: {0} is not a ControllableModule. Skipping it.", pm.moduleName);
					}
				}
				controlled_modules.AddRange(modules);
			}
			//get animated nodes
			foreach(string node_name in AnimatedNodes.Split(' '))
			{
				if(node_name == "") continue;
				Transform node_transform = part.FindModelTransform(node_name);
				if(node_transform == null) 
				{
					Utils.Log("HangarGenericInflatable.OnStart: no transform '{0}' in {1}", node_name, part.name);
					continue;
				}
				AttachNode node = part.findAttachNode(node_name);
				if(node == null) node = part.srfAttachNode.id == node_name? part.srfAttachNode : null;
				if(node == null)
				{
					Utils.Log("HangarGenericInflatable.OnStart: no node '{0}' in {1}", node_name, part.name);
					continue;
				}
				var a_node = new AnimatedNode(node, node_transform, part);
				animated_nodes.Add(a_node);
			}
			//calculate prefab metric
			prefab = part.partInfo.partPrefab;
			prefab_metric = new Metric(prefab);
			//get compressed gas for the first time
			if(CompressedGas < 0) CompressedGas = InflatableVolume;
			//load compressor
			if(CompressorConfig != null)
			{
				Compressor = new GasCompressor(part);
				Compressor.Load(CompressorConfig);
			}
			//ignore DragMultiplier as Drag is changed with volume
			DragMultiplier = 1f;
			//update part, GUI and set the flag
			UpdatePart();
			ToggleEvents();
			StartCoroutine(UpdateStatus());
			gui_state = this.DeactivateGUI();
			just_loaded = true;
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			if(node.HasNode(GasCompressor.NODE_NAME)) 
			{
				CompressorConfig = node.GetNode(GasCompressor.NODE_NAME);
				//for part info only
				Compressor = new GasCompressor(part);
				Compressor.Load(CompressorConfig);
			}
			if(!node.HasValue("SavedState"))
				State = PackedByDefault? AnimatorState.Closed : AnimatorState.Opened;
		}
		#endregion

		#region Updates
		public override void FixedUpdate()
		{
			base.FixedUpdate();
			if(State == AnimatorState.Opening  || 
				State == AnimatorState.Closing ||
				just_loaded) 
			{ 
				if(just_loaded)
				{
					StartCoroutine(FirstTimeUpdateNodes());
					just_loaded = false;
				}
				else 
				{
					UpdatePart();
					part.BreakConnectedStruts();
				}
			}
			if(Compressor != null && !has_compressed_gas)
			{
				CompressedGas += Compressor.CompressGas();
				if(has_compressed_gas) 
				{ play_compressor = false; ToggleEvents(); }
				else play_compressor = Compressor.OutputFraction > 0;
			}
		}

		public override void Update()
		{ 
			base.Update();
			if(play_compressor)
			{
				fxSndCompressor.audio.pitch = 0.5f + 0.5f*Compressor.OutputFraction;
				if(!fxSndCompressor.audio.isPlaying)
					fxSndCompressor.audio.Play();
			}
			else fxSndCompressor.audio.Stop();
			#if DEBUG
			animated_nodes.ForEach(n => n.DrawAnchor());
			#endif
		}

		protected virtual void UpdatePart() 
		{ 
			animated_nodes.ForEach(n => n.UpdateNode());
			if(prefab_metric == null) return;
			part_metric  = new Metric(part);
			volume_scale = part_metric.volume/prefab_metric.volume;
			part.buoyancy       = prefab.buoyancy*volume_scale;
			part.maximum_drag   = prefab.maximum_drag*volume_scale;
			part.angularDrag    = prefab.angularDrag*volume_scale;
			part.crashTolerance = prefab.crashTolerance*Mathf.Pow(volume_scale, 1/3f);
		}

		IEnumerator<YieldInstruction> FirstTimeUpdateNodes()
		{
			for(int i = 0; i < skip_fixed_frames; i++)
			{
				yield return new WaitForFixedUpdate();
				UpdatePart();
			}
			if(State == AnimatorState.Opened) EnableModules(true);
			if(gui_state == null) gui_state = this.SaveGUIState();
			this.ActivateGUI(gui_state);
		}

		public void UpdateGUI(ShipConstruct ship) { UpdatePart(); }
		#endregion

		#region Modules Control
		bool CanEnableModules() { return controlled_modules.All(m => m.CanEnable()); }
		bool CanDisableModules() { return controlled_modules.All(m => m.CanDisable()); }

		void EnableModules(bool enable) { controlled_modules.ForEach(m => m.Enable(enable)); }

		IEnumerator<YieldInstruction> DelayedEnableModules(bool enable)
		{
			if(!enable) EnableModules(enable);
			AnimatorState target_state = enable ? AnimatorState.Opened : AnimatorState.Closed;
			while(State != target_state) yield return null;
			UpdatePart();
			yield return new WaitForSeconds(0.5f);
			if(enable) EnableModules(enable);
		}
		#endregion

		#region Events and actions
		void ToggleEvents()
		{
			bool state = State == AnimatorState.Closed || State == AnimatorState.Closing;
			Events["Inflate"].active = state && has_compressed_gas;
			Events["Deflate"].active = !state;
		}

		[KSPEvent (guiActiveEditor = true, guiActive = true, guiName = "Inflate", active = true)]
		public void Inflate() 
		{ 
			if(!has_compressed_gas) return;
			if(State != AnimatorState.Closed) return;
			if(!CanEnableModules()) return;
			if(HighLogic.LoadedScene == GameScenes.FLIGHT) 
				CompressedGas = 0;
			StartCoroutine(DelayedEnableModules(true));
			Open(); ToggleEvents();
		}

		[KSPEvent (guiActiveEditor = true, guiActive = true, guiName = "Deflate", active = false)]
		public void Deflate()	
		{ 
			if(State != AnimatorState.Opened) return;
			if(!CanDisableModules()) return;
			StartCoroutine(DelayedEnableModules(false));
			Close(); ToggleEvents();
		}

		[KSPAction("Inflate")]
		public void InflateAction(KSPActionParam param) { Inflate(); }
		#endregion
	}
}