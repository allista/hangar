using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

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
		public float ConversionRate { get; private set; }
		public float ConsumptionRate { get; private set; }
		Part part;

		public GasCompressor(Part p) { part = p; ConsumptionRate = 1.0f; ConversionRate = 0.05f; }

		public float CompressGas()
		{
			if(part.vessel == null || part.vessel.mainBody == null) return 0;
			if(!part.vessel.mainBody.atmosphere) return 0;
			double pressure = FlightGlobals.getStaticPressure(part.vessel.altitude, part.vessel.mainBody);
			if(pressure < 1e-6) return 0;
			float consumed = ConsumptionRate*(TimeWarp.fixedDeltaTime);
			consumed = part.RequestResource("ElectricCharge", consumed);
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
		[KSPField(isPersistant = false)]
		public string ControlledModules;

		[KSPField(isPersistant = false)]
		public string AnimatedNodes;

		[KSPField(isPersistant = false)]
		public bool PackedByDefault = true;

		[KSPField(isPersistant = false)]
		public float InflatableVolume;

		[KSPField(isPersistant = true)]
		public float CompressedGas = -1f;

		[KSPField (guiName = "Compressed Gas", guiActive=true)] public string CompressedGasDisplay;

		readonly List<IControllableModule> controlled_modules = new List<IControllableModule>();
		readonly List<AnimatedNode> animated_nodes = new List<AnimatedNode>();

		public ConfigNode CompressorConfig = null;
		GasCompressor compressor = null;
		bool has_compressed_gas { get { return CompressedGas >= InflatableVolume; } }

		Part   prefab = null;
		Metric prefab_metric = null;
		Metric part_metric = null;
		protected float volume_scale = 1;

		const int skip_fixed_frames = 5;
		ModuleGUIState gui_state;
		bool just_loaded = false;


		#region Info
		public override string GetInfo()
		{ 
			if(compressor == null) return "";
			string info = "Compressor:\n";
			info += string.Format("Rate: {0}/el.u.\n", Utils.formatVolume(compressor.ConversionRate));
			info += string.Format("Power Consumption: {0} el.u./sec\n", compressor.ConsumptionRate);
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
		}
		void OnDestroy() { GameEvents.onEditorShipModified.Remove(UpdateGUI); }

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			if(state == StartState.None) return;
			//get controlled modules
			foreach(string module_name in ControlledModules.Split(' '))
			{
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
				compressor = new GasCompressor(part);
				compressor.Load(CompressorConfig);
			}
			//forbid surface attachment for the inflatable
			part.attachRules.allowSrfAttach = false;
			UpdatePart();
			//update GUI and set the flag
			ToggleEvents();
			StartCoroutine(UpdateStatus());
			gui_state = this.DeactivateGUI();
			just_loaded = true;
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			if(node.HasNode("COMPRESSOR")) 
			{
				CompressorConfig = node.GetNode("COMPRESSOR");
				//for part info only
				compressor = new GasCompressor(part);
				compressor.Load(CompressorConfig);
			}
			if(!node.HasValue("SavedState"))
				State = PackedByDefault? AnimatorState.Closed : AnimatorState.Opened;
		}
		#endregion

		#region Updates
		public void FixedUpdate()
		{
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
			if(compressor != null && !has_compressed_gas)
			{
				CompressedGas += compressor.CompressGas();
				if(has_compressed_gas) ToggleEvents();
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
			while(State != target_state) 
				yield return new WaitForFixedUpdate();
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