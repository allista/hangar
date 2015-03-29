//   PartUpdaters.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

// This code is based on Procedural Fairings plug-in by Alexey Volynskov, KzPartResizer class
// And on ideas drawn from the TweakScale plugin

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using AtHangar;

namespace AtHangar
{
	#region From TweakScale (reworked)
	public abstract class UpdaterRegistrator : MonoBehaviour
	//Can't understand what it is needed for =^_^=
	//Probably a workaround of some sort.
	{
		static bool loadedInScene = false;

		public void Start()
		{
			if(loadedInScene)
			{
				Destroy(gameObject);
				return;
			}
			loadedInScene = true;
			OnStart();
		}
		public abstract void OnStart();

		public void Update()
		{
			loadedInScene = false;
			Destroy(gameObject);

		}
	}

	[KSPAddon(KSPAddon.Startup.Instantly, true)]
	class PartUpdatereRegister : UpdaterRegistrator
	{
		/// <summary>
		/// Gets all types defined in all loaded assemblies.
		/// </summary>
		static IEnumerable<Type> get_all_types()
		{
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type[] types;
				try	{ types = assembly.GetTypes(); }
				catch(Exception) { types = Type.EmptyTypes; }
				foreach(var type in types) yield return type;
			}
		}

		//Register all found PartUpdaters
		override public void OnStart()
		{
			var all_updaters = get_all_types().Where(IsPartUpdater).ToArray();
			foreach (var updater in all_updaters)
			{
				MethodInfo register = typeof(PartUpdater).GetMethod("RegisterUpdater");
				register = register.MakeGenericMethod(new [] { updater });
				register.Invoke(null, null);
			}
		}

		static bool IsPartUpdater(Type t)
		{ return !t.IsGenericType && t.IsSubclassOf(typeof(PartUpdater)); }
	}
	#endregion

	public abstract class PartUpdaterBase : PartModule
	{
		protected Part base_part;

		public static Vector3 ScaleVector(Vector3 v, float s, float l)
		{ return Vector3.Scale(v, new Vector3(s, s*l, s)); }

		public virtual void Init() 
		{ base_part = PartLoader.getPartInfoByName(part.partInfo.name).partPrefab; }

		protected abstract void SaveDefaults();
	}

	public abstract class PartUpdater : PartUpdaterBase
	{
		public uint priority = 0; // 0 is highest

		protected override void SaveDefaults() {}
		public virtual void OnRescale(Scale scale) {}

		#region ModuleUpdaters
		public readonly static Dictionary<string, Func<Part, PartUpdater>> UpdatersTypes = new Dictionary<string, Func<Part, PartUpdater>>();

		static Func<Part, PartUpdater> updaterConstructor<UpdaterType>() where UpdaterType : PartUpdater
		{ return part => part.GetModule<UpdaterType>() ?? part.AddModule(typeof(UpdaterType).Name) as UpdaterType; }

		public static void RegisterUpdater<UpdaterType>() 
			where UpdaterType : PartUpdater
		{ 
			string updater_name = typeof(UpdaterType).FullName;
			if(UpdatersTypes.ContainsKey(updater_name)) return;
			Utils.Log("PartUpdater: registering {0}", updater_name);
			UpdatersTypes[updater_name] = updaterConstructor<UpdaterType>();
		}
		#endregion
	}

	public class NodesUpdater : PartUpdater
	{
		readonly Dictionary<string, AttachNode> orig_nodes = new Dictionary<string, AttachNode>();

		public override void Init() { base.Init(); SaveDefaults(); }
		protected override void SaveDefaults()
		{ base_part.attachNodes.ForEach(n => orig_nodes[n.id] = n); }

		public override void OnRescale(Scale scale)
		{
			//update attach nodes and their parts
			foreach(AttachNode node in part.attachNodes)
			{
				#if DEBUG
				this.Log("OnRescale: node.id {0}, node.size {1}, node.bForce {2} node.bTorque {3}", 
					node.id, node.size, node.breakingForce, node.breakingTorque);
				#endif
				//ModuleGrappleNode adds new AttachNode on dock
				if(!orig_nodes.ContainsKey(node.id)) continue; 
				//update node position
				node.position = ScaleVector(node.originalPosition, scale, scale.aspect);
				part.UpdateAttachedPartPos(node);
				//update node size
				int new_size = orig_nodes[node.id].size + Mathf.RoundToInt(scale.size-scale.orig_size);
				if(new_size < 0) new_size = 0;
				node.size = new_size;
				//update node breaking forces
				node.breakingForce  = orig_nodes[node.id].breakingForce  * scale.absolute.quad;
				node.breakingTorque = orig_nodes[node.id].breakingTorque * scale.absolute.quad;
			}
			//update this surface attach node
			if(part.srfAttachNode != null)
			{
				Vector3 old_position = part.srfAttachNode.position;
				part.srfAttachNode.position = ScaleVector(part.srfAttachNode.originalPosition, scale, scale.aspect);
				//don't move the part at start, its position is persistant
				if(!scale.FirstTime)
				{
					Vector3 d_pos = part.transform.TransformDirection(part.srfAttachNode.position - old_position);
					part.transform.position -= d_pos;
				}
			}
			//no need to update surface attached parts on start
			//as their positions are persistant; less calculations
			if(scale.FirstTime) return;
			//update parts that are surface attached to this
			foreach(Part child in part.children)
			{
				if(child.srfAttachNode != null && child.srfAttachNode.attachedPart == part)
				{
					Vector3 attachedPosition = child.transform.localPosition + child.transform.localRotation * child.srfAttachNode.position;
					Vector3 targetPosition = ScaleVector(attachedPosition, scale.relative, scale.relative.aspect);
					child.transform.Translate(targetPosition - attachedPosition, part.transform);
				}
			}
		}
	}

	public class PropsUpdater : PartUpdater
	{
		public override void OnRescale(Scale scale)
		{
			//change breaking forces (if not defined in the config, set to a reasonable default)
			part.breakingForce  = Mathf.Max(22f, base_part.breakingForce * scale.absolute.quad);
			part.breakingTorque = Mathf.Max(22f, base_part.breakingTorque * scale.absolute.quad);
			//change other properties
			part.buoyancy = base_part.buoyancy * scale.absolute.cube * scale.absolute.aspect;
			part.explosionPotential = base_part.explosionPotential * scale.absolute.cube * scale.absolute.aspect;
		}
	}

	/// <summary>
	/// Emitter updater. Adapted from TweakScale.
	/// </summary>
	public class EmitterUpdater : PartUpdater
	{
		struct EmitterData
		{
			public readonly float MinSize, MaxSize, Shape1D;
			public readonly Vector2 Shape2D;
			public readonly Vector3 Shape3D, LocalVelocity, Force;
			public EmitterData(KSPParticleEmitter pe)
			{
				MinSize = pe.minSize;
				MaxSize = pe.maxSize;
				Shape1D = pe.shape1D;
				Shape2D = pe.shape2D;
				Shape3D = pe.shape3D;
				Force   = pe.force;
				LocalVelocity = pe.localVelocity;
			}
		}

		Scale scale;
		readonly Dictionary<KSPParticleEmitter, EmitterData> orig_scales = new Dictionary<KSPParticleEmitter, EmitterData>();

		void UpdateParticleEmitter(KSPParticleEmitter pe)
		{
			if(pe == null) return;
			if(!orig_scales.ContainsKey(pe))
				orig_scales[pe] = new EmitterData(pe);
			var ed = orig_scales[pe];
			pe.minSize = ed.MinSize * scale;
			pe.maxSize = ed.MaxSize * scale;
			pe.shape1D = ed.Shape1D * scale;
			pe.shape2D = ed.Shape2D * scale;
			pe.shape3D = ed.Shape3D * scale;
			pe.force   = ed.Force   * scale;
			pe.localVelocity = ed.LocalVelocity * scale;
		}

		public override void OnUpdate()
		{
			if(scale == null) return;
			var emitters = part.gameObject.GetComponentsInChildren<KSPParticleEmitter>();
			if(emitters == null) return;
			emitters.ForEach(UpdateParticleEmitter);
			scale = null;
		}

		public override void OnRescale(Scale scale)
		{
			if(part.FindModelComponent<KSPParticleEmitter>() != null ||
			   part.GetComponents<EffectBehaviour>()
			   .Any(e => e is ModelMultiParticleFX || e is ModelParticleFX))
				this.scale = scale;
		}
	}

	public class ResourcesUpdater : PartUpdater
	{
		public override void OnRescale(Scale scale)
		{
			//no need to update resources on start
			//as they are persistant; less calculations
			if(scale.FirstTime) return;
			foreach(PartResource r in part.Resources)
			{
				var s = r.resourceName == "AblativeShielding"? 
					scale.relative.quad : scale.relative.cube * scale.relative.aspect;
				r.amount *= s; r.maxAmount *= s;
			}
		}
	}

	public abstract class ModuleUpdater<T> : PartUpdater where T : PartModule
	{
		protected struct ModulePair<M>
		{
			public M base_module;
			public M module;
			public Dictionary<string, object> orig_data;

			public ModulePair(M base_module, M module)
			{
				this.module = module;
				this.base_module = base_module;
				orig_data = new Dictionary<string, object>();
			}
		}

		protected readonly List<ModulePair<T>> modules = new List<ModulePair<T>>();

		public override void Init() 
		{
			base.Init();
			priority = 100; 
			var m = part.Modules.GetEnumerator();
			var b = base_part.Modules.GetEnumerator();
			while(b.MoveNext() && m.MoveNext())
			{
				if(b.Current is T && m.Current is T)
					modules.Add(new ModulePair<T>(b.Current as T, m.Current as T));
			}
			if(modules.Count == 0) 
				throw new MissingComponentException(string.Format("[Hangar] ModuleUpdater: part {0} does not have {1} module", part.name, typeof(T).Name));
			SaveDefaults();
		}

		protected abstract void on_rescale(ModulePair<T> mp, Scale scale);

		public override void OnRescale(Scale scale) 
		{ modules.ForEach(mp => on_rescale(mp, scale)); }
	}

	public class RCS_Updater : ModuleUpdater<ModuleRCS>
	{
		[KSPField(isPersistant=false, guiActiveEditor=true, guiActive=true, guiName="Thrust")]
		public string thrustDisplay;

		string all_thrusts() 
		{ 
			return modules
				.Aggregate("", (s, mp) => s+mp.module.thrusterPower + ", ")
				.Trim(", ".ToCharArray()); 
		}

		public override void OnStart(StartState state) { base.OnStart(state); thrustDisplay = all_thrusts(); }
		public override void OnRescale(Scale scale)	{ base.OnRescale(scale); thrustDisplay = all_thrusts(); }

		protected override void on_rescale(ModulePair<ModuleRCS> mp, Scale scale)
		{ mp.module.thrusterPower = mp.base_module.thrusterPower*scale.absolute.quad; }
	}

	public class DockingNodeUpdater : ModuleUpdater<ModuleDockingNode>
	{
		protected override void on_rescale(ModulePair<ModuleDockingNode> mp, Scale scale)
		{
			AttachNode node = part.findAttachNode(mp.module.referenceAttachNode);
			if(node == null) return;
			if(mp.module.nodeType.StartsWith("size"))
				mp.module.nodeType = string.Format("size{0}", node.size);
		}
	}

	public class PassageUpdater : ModuleUpdater<HangarPassage>
	{ 
		protected override void on_rescale(ModulePair<HangarPassage> mp, Scale scale)
		{
			mp.module.Setup(!scale.FirstTime);
			foreach(var key in new List<string>(mp.module.Nodes.Keys))
				mp.module.Nodes[key].Size = Vector3.Scale(mp.base_module.Nodes[key].Size, 
					new Vector3(scale, scale, 1));
		}
	}

	public class HangarMachineryUpdater : ModuleUpdater<HangarMachinery>
	{ 
		protected override void on_rescale(ModulePair<HangarMachinery> mp, Scale scale)
		{
			mp.module.Setup(!scale.FirstTime);
			mp.module.EnergyConsumption = mp.base_module.EnergyConsumption * scale.absolute.quad * scale.absolute.aspect; 
		}
	}

	public class AnimatorUpdater : ModuleUpdater<HangarAnimator>
	{ 
		protected override void on_rescale(ModulePair<HangarAnimator> mp, Scale scale)
		{ mp.module.EnergyConsumption = mp.base_module.EnergyConsumption * scale.absolute.quad * scale.absolute.aspect; }
	}

	public class ReactionWheelUpdater : ModuleUpdater<ModuleReactionWheel>
	{
		protected override void on_rescale(ModulePair<ModuleReactionWheel> mp, Scale scale)
		{
			mp.module.PitchTorque  = mp.base_module.PitchTorque * scale.absolute.quad * scale.absolute.aspect;
			mp.module.YawTorque    = mp.base_module.YawTorque   * scale.absolute.quad * scale.absolute.aspect;
			mp.module.RollTorque   = mp.base_module.RollTorque  * scale.absolute.quad * scale.absolute.aspect;
			var input_resources = mp.base_module.inputResources.ToDictionary(r => r.name);
			mp.module.inputResources.ForEach(r => r.rate = input_resources[r.name].rate * scale.absolute.quad * scale.absolute.aspect);
		}
	}

	public class GenericInflatableUpdater : ModuleUpdater<HangarGenericInflatable>
	{
		protected override void on_rescale(ModulePair<HangarGenericInflatable> mp, Scale scale)
		{
			mp.module.InflatableVolume = mp.base_module.InflatableVolume * scale.absolute.cube * scale.absolute.aspect;
			mp.module.CompressedGas   *= scale.relative.cube * scale.relative.aspect;
			mp.module.ForwardSpeed     = mp.base_module.ForwardSpeed / (scale.absolute * scale.aspect);
			mp.module.ReverseSpeed     = mp.base_module.ReverseSpeed / (scale.absolute * scale.aspect);
			if(mp.module.Compressor == null) return;
			mp.module.Compressor.ConsumptionRate = mp.base_module.Compressor.ConsumptionRate * scale.absolute.cube * scale.absolute.aspect;
		}
	}

	public class GeneratorUpdater : ModuleUpdater<ModuleGenerator>
	{
		protected override void on_rescale(ModulePair<ModuleGenerator> mp, Scale scale)
		{
			var input_resources  = mp.base_module.inputList.ToDictionary(r => r.name);
			var output_resources = mp.base_module.outputList.ToDictionary(r => r.name);
			mp.module.inputList.ForEach(r =>  r.rate = input_resources[r.name].rate  * scale.absolute.cube * scale.absolute.aspect);
			mp.module.outputList.ForEach(r => r.rate = output_resources[r.name].rate * scale.absolute.cube * scale.absolute.aspect);
		}
	}

	public class SolarPanelUpdater : ModuleUpdater<ModuleDeployableSolarPanel>
	{
		protected override void on_rescale(ModulePair<ModuleDeployableSolarPanel> mp, Scale scale)
		{
			mp.module.chargeRate = mp.base_module.chargeRate * scale.absolute.quad * scale.absolute.aspect; 
			mp.module.flowRate   = mp.base_module.flowRate   * scale.absolute.quad * scale.absolute.aspect; 
		}
	}

	public class DecoupleUpdater : ModuleUpdater<ModuleDecouple>
	{
		protected override void on_rescale(ModulePair<ModuleDecouple> mp, Scale scale)
		{ mp.module.ejectionForce = mp.base_module.ejectionForce * scale.absolute.cube; }
	}

	public class SwitchableTankUpdater : ModuleUpdater<HangarSwitchableTank>
	{
		protected override void on_rescale(ModulePair<HangarSwitchableTank> mp, Scale scale)
		{ mp.module.Volume *= scale.relative.cube * scale.relative.aspect;	}
	}

	public class ResourceConverterUpdater : ModuleUpdater<AnimatedConverterBase>
	{
		protected override void on_rescale(ModulePair<AnimatedConverterBase> mp, Scale scale)
		{ mp.module.SetRatesMultiplier(mp.base_module.RatesMultiplier * scale.absolute.cube * scale.absolute.aspect); }
	}

	public class TankManagerUpdater : ModuleUpdater<HangarTankManager>
	{
		protected override void on_rescale(ModulePair<HangarTankManager> mp, Scale scale)
		{ 
			mp.module.RescaleTanks(scale.relative.cube * scale.relative.aspect); 
			mp.module.Volume = mp.base_module.Volume * scale.absolute.cube * scale.absolute.aspect;
		}
	}

	public class HangarLightUpdater : ModuleUpdater<HangarLight>
	{
		protected override void on_rescale(ModulePair<HangarLight> mp, Scale scale)
		{ 
			mp.module.RangeMultiplier = mp.base_module.RangeMultiplier * scale;
			mp.module.UpdateLights(); 
		}
	}

	public class EngineUpdater : ModuleUpdater<ModuleEngines>
	{
		[KSPField(isPersistant=false, guiActiveEditor=true, guiActive=false, guiName="Max. Thrust")]
		public string thrustDisplay;

		string all_thrusts() 
		{ 
			return modules
				.Aggregate("", (s, mp) => s+mp.module.maxThrust + ", ")
				.Trim(", ".ToCharArray()); 
		}

		public override void OnStart(StartState state) { base.OnStart(state); thrustDisplay = all_thrusts(); }
		public override void OnRescale(Scale scale)	{ base.OnRescale(scale); thrustDisplay = all_thrusts(); }

		protected override void on_rescale(ModulePair<ModuleEngines> mp, Scale scale)
		{
			mp.module.minThrust = mp.base_module.minThrust * scale.absolute.quad;
			mp.module.maxThrust = mp.base_module.maxThrust * scale.absolute.quad;
//			mp.module.heatProduction = mp.base_module.heatProduction * scale.absolute;
		}
	}

	public class EngineFXUpdater : ModuleUpdater<ModuleEnginesFX>
	{
		[KSPField(isPersistant=false, guiActiveEditor=true, guiActive=false, guiName="Max. Thrust")]
		public string thrustDisplay;

		string all_thrusts() 
		{ 
			return modules
				.Aggregate("", (s, mp) => s+mp.module.maxThrust + ", ")
				.Trim(", ".ToCharArray()); 
		}

		public override void OnStart(StartState state) { base.OnStart(state); thrustDisplay = all_thrusts(); }
		public override void OnRescale(Scale scale)	{ base.OnRescale(scale); thrustDisplay = all_thrusts(); }

		protected override void on_rescale(ModulePair<ModuleEnginesFX> mp, Scale scale)
		{
			mp.module.minThrust = mp.base_module.minThrust * scale.absolute.quad;
			mp.module.maxThrust = mp.base_module.maxThrust * scale.absolute.quad;
//			mp.module.heatProduction = mp.base_module.heatProduction * scale.absolute;
		}
	}

	public class ResourceIntakeUpdater : ModuleUpdater<ModuleResourceIntake>
	{
		protected override void on_rescale(ModulePair<ModuleResourceIntake> mp, Scale scale)
		{ mp.module.area = mp.base_module.area * scale.absolute.quad; }
	}

	public class HangarFairingsUpdater : ModuleUpdater<HangarFairings>
	{
		protected override void on_rescale(ModulePair<HangarFairings> mp, Scale scale)
		{ 
			mp.module.JettisonForce = mp.base_module.JettisonForce * scale.absolute.cube * scale.absolute.aspect;
			mp.module.FairingsCost = mp.base_module.FairingsCost * scale.absolute.cube * scale.absolute.aspect;
		}
	}

	public class JettisonUpdater : ModuleUpdater<ModuleJettison>
	{
		protected override void SaveDefaults()
		{
			base.SaveDefaults();
			foreach(var mp in modules)
				mp.orig_data["local_scale"] = mp.module.jettisonTransform.localScale;
		}

		protected override void on_rescale(ModulePair<ModuleJettison> mp, Scale scale)
		{
			mp.module.jettisonedObjectMass = mp.base_module.jettisonedObjectMass * scale.absolute.cube * scale.absolute.aspect;
			mp.module.jettisonForce = mp.base_module.jettisonForce * scale.absolute.cube * scale.absolute.aspect;
			if(mp.module.jettisonTransform != null)
			{
				var p = mp.module.jettisonTransform.parent.gameObject.GetComponent<Part>();
				if(p == null || p == mp.module.part) return;
				object orig_scale;
				if(!mp.orig_data.TryGetValue("local_scale", out orig_scale) ||
				   !(orig_scale is Vector3)) return;
				mp.module.jettisonTransform.localScale = ScaleVector((Vector3)orig_scale, scale, scale.aspect);
			}
		}
	}
}
