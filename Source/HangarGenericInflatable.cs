//   HangarGenericInflatable.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

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

	public class ControllableModuleBase : PartModule, IControllableModule
	{
		virtual public bool CanEnable() { return true; }
		virtual public bool CanDisable() { return true; }

		virtual public void Enable(bool enable) 
		{ enabled = isEnabled = enable; }
	}

	public class GasCompressor : ConfigNodeObject
	{
		new public const string NODE_NAME = "COMPRESSOR";
		[Persistent] public float ConversionRate = 0.05f;
		[Persistent] public float ConsumptionRate = 1.0f;
		public float OutputFraction { get; private set; }
		readonly ResourcePump socket;
		readonly Part part;

		public GasCompressor(Part p) 
		{ part = p; socket = p.CreateSocket(); }

		public float CompressGas()
		{
			if(part.vessel == null || part.vessel.mainBody == null) return 0;
			if(!part.vessel.mainBody.atmosphere) return 0;
			var pressure = FlightGlobals.getStaticPressure(part.vessel.altitude, part.vessel.mainBody);
			if(pressure < 1e-6) return 0;
			socket.RequestTransfer(ConsumptionRate*TimeWarp.fixedDeltaTime);
			if(!socket.TransferResource()) return 0 ;
			OutputFraction = socket.Ratio;
			return (float)(pressure * ConversionRate * socket.Result);
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
		readonly SimpleDialog warning = new SimpleDialog();
		bool try_deflate;

		//modules and nodes
		readonly List<IControllableModule> controlled_modules = new List<IControllableModule>();
		readonly List<AnimatedNode> animated_nodes = new List<AnimatedNode>();

		//compressor
		public ConfigNode ModuleConfig;
		public GasCompressor Compressor { get; protected set; }
		bool has_compressed_gas { get { return CompressedGas >= InflatableVolume; } }
		public FXGroup fxSndCompressor;
		bool play_compressor;

		//metric and scale
		Part   prefab = null;
		Metric prefab_metric;
		Metric part_metric;
		protected float volume_scale = 1;

		//state
		const int skip_fixed_frames = 5;
		bool just_loaded = false;

		#region Info
		public override string GetInfo()
		{ 
			if(!init_compressor()) return "";
			string info = "Compressor:\n";
			info += string.Format("Pump Rate: {0}/sec\n", 
				Utils.formatVolume(Compressor.ConversionRate*Compressor.ConsumptionRate));
			info += string.Format("Energy Consumption: {0}/sec\n", Compressor.ConsumptionRate);
			return info;
		}
		#endregion
		
		#region Startup
		protected virtual void onPause()
		{
			if(fxSndCompressor.audio == null) return;
			if(fxSndCompressor.audio.isPlaying) fxSndCompressor.audio.Pause();
		}

		protected virtual void onUnpause()
		{
			if(fxSndCompressor.audio == null) return;
			if(play_compressor) fxSndCompressor.audio.Play();
		}

		public override void OnAwake()
		{
			base.OnAwake();
			GameEvents.onGamePause.Add(onPause);
			GameEvents.onGameUnpause.Add(onUnpause);
			GameEvents.onEditorShipModified.Add(UpdateGUI);
		}

		public void OnDestroy()
		{
			GameEvents.onGamePause.Remove(onPause);
			GameEvents.onGameUnpause.Remove(onUnpause);
			GameEvents.onEditorShipModified.Remove(UpdateGUI);
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			if(state == StartState.None) return;
			//get sound effects
			if(CompressorSound != string.Empty)
			{
				Utils.createFXSound(part, fxSndCompressor, CompressorSound, true);
				fxSndCompressor.audio.volume = GameSettings.SHIP_VOLUME * SoundVolume;
			}
			//get controlled modules
			foreach(string module_name in ControlledModules.Split(' '))
			{
				if(module_name == "") continue;
				if(!part.Modules.Contains(module_name))
				{
					this.Log("OnStart: {0} does not contain {1} module.", part.name, module_name);
					continue;
				}
				var modules = new List<IControllableModule>();
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
						else this.Log("OnStart: {0} is not a ControllableModule. Skipping it.", pm.moduleName);
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
					this.Log("OnStart: no transform '{0}' in {1}", node_name, part.name);
					continue;
				}
				AttachNode node = part.findAttachNode(node_name);
				if(node == null) node = part.srfAttachNode.id == node_name? part.srfAttachNode : null;
				if(node == null)
				{
					this.Log("OnStart: no node '{0}' in {1}", node_name, part.name);
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
			init_compressor();
			//ignore DragMultiplier as Drag is changed with volume
			DragMultiplier = 1f;
			//prevent accidental looping of animation
			Loop = false;
			//update part, GUI and set the flag
			UpdatePart();
			ToggleEvents();
			StartCoroutine(SlowUpdate());
			isEnabled = false;
			just_loaded = true;
		}

		bool init_compressor()
		{
			Compressor = null;
			if(ModuleConfig == null) 
			{ this.Log("ModuleConfig is null. THIS SHOULD NEVER HAPPEN!"); return false; }
			if(ModuleConfig.HasNode(GasCompressor.NODE_NAME)) 
			{
				Compressor = new GasCompressor(part);
				Compressor.Load(ModuleConfig.GetNode(GasCompressor.NODE_NAME));
				return true;
			}
			return false;
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			//only save config for the first time
			if(ModuleConfig == null) ModuleConfig = node;
			if(!node.HasValue("SavedState"))
				State = PackedByDefault? AnimatorState.Closed : AnimatorState.Opened;
		}

		//workaround for ConfigNode non-serialization
		public byte[] _module_config;
		public void OnBeforeSerialize()
		{ _module_config = ConfigNodeWrapper.SaveConfigNode(ModuleConfig); }
		public void OnAfterDeserialize() 
		{ ModuleConfig = ConfigNodeWrapper.RestoreConfigNode(_module_config); }
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

		#if DEBUG
		public override void Update()
		{ 
			base.Update();
			animated_nodes.ForEach(n => n.DrawAnchor());
		}
		#endif

		protected virtual void UpdatePart() 
		{ 
			animated_nodes.ForEach(n => n.UpdateNode());
			if(prefab_metric.Empty) return;
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
			isEnabled = true;
		}

		IEnumerator<YieldInstruction> SlowUpdate()
		{
			while(true)
			{
				//update GUI
				CompressedGasDisplay = string.Format("{0:P1}", CompressedGas/InflatableVolume);
				//update sounds
				if(fxSndCompressor.audio != null)
				{
					if(play_compressor)
					{
						fxSndCompressor.audio.pitch = 0.5f + 0.5f*Compressor.OutputFraction;
						if(!fxSndCompressor.audio.isPlaying)
							fxSndCompressor.audio.Play();
					}
					else fxSndCompressor.audio.Stop();
				}
				//wait
				yield return new WaitForSeconds(0.5f);
			}
		}

		public void UpdateGUI(ShipConstruct ship) { UpdatePart(); }

		public void OnGUI() 
		{ 
			if(Event.current.type != EventType.Layout) return;
			Styles.Init();
			while(try_deflate)
			{
				if(has_compressed_gas) { deflate(); break; }
				if(Compressor == null)
					warning.Show("The hangar is not equipped with a compressor. " +
						"You will not be able to inflate the hangar again. " +
						"Are you sure you want to deflate the hangar?");
				else if(!part.vessel.mainBody.atmosphere)
					warning.Show("There's no atmosphere here. " +
						"You will not be able to inflate the hangar again." +
						"Are you sure you want to deflate the hangar?");
				else { deflate(); break; }
				if(warning.Result == SimpleDialog.Answer.None) break;
				if(warning.Result == SimpleDialog.Answer.Yes) deflate();
				try_deflate = false;
				break;
			}
		}
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
			if(HighLogic.LoadedSceneIsFlight) CompressedGas = 0;
			StartCoroutine(DelayedEnableModules(true));
			Open(); ToggleEvents();
		}

		[KSPEvent (guiActiveEditor = true, guiActive = true, guiName = "Deflate", active = false)]
		public void Deflate()	
		{ 
			if(State != AnimatorState.Opened) return;
			if(!CanDisableModules()) return;
			try_deflate = true;
		}

		void deflate()
		{
			StartCoroutine(DelayedEnableModules(false));
			Close(); ToggleEvents();
			try_deflate = false;
		}

		[KSPAction("Inflate")]
		public void InflateAction(KSPActionParam param) { Inflate(); }
		#endregion
	}
}