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
			if(!part.GetComponents<EffectBehaviour>()
				.Any(e => e is ModelMultiParticleFX || e is ModelParticleFX)) return;
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

			public ModulePair(M base_module, M module)
			{
				this.module = module;
				this.base_module = base_module;
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

		protected abstract void on_rescale(T module, T base_module, Scale scale);

		public override void OnRescale(Scale scale) 
		{ modules.ForEach(mp => on_rescale(mp.module, mp.base_module, scale)); }
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

		protected override void on_rescale(ModuleRCS module, ModuleRCS base_module, Scale scale)
		{ module.thrusterPower = base_module.thrusterPower*scale.absolute.quad; }
	}

	public class DockingNodeUpdater : ModuleUpdater<ModuleDockingNode>
	{
		protected override void on_rescale(ModuleDockingNode module, ModuleDockingNode base_module, Scale scale)
		{
			AttachNode node = part.findAttachNode(module.referenceAttachNode);
			if(node == null) return;
			if(module.nodeType.StartsWith("size"))
				module.nodeType = string.Format("size{0}", node.size);
		}
	}

	public class PassageUpdater : ModuleUpdater<HangarPassage>
	{ 
		protected override void on_rescale(HangarPassage module, HangarPassage base_module, Scale scale)
		{
			module.Setup(!scale.FirstTime);
			foreach(var key in new List<string>(module.Nodes.Keys))
				module.Nodes[key].Size = Vector3.Scale(base_module.Nodes[key].Size, 
					new Vector3(scale, scale, 1));
		}
	}

	public class HangarMachineryUpdater : ModuleUpdater<HangarMachinery>
	{ 
		protected override void on_rescale(HangarMachinery module, HangarMachinery base_module, Scale scale)
		{
			module.Setup(!scale.FirstTime);
			module.EnergyConsumption = base_module.EnergyConsumption * scale.absolute.quad * scale.absolute.aspect; 
		}
	}

	public class AnimatorUpdater : ModuleUpdater<HangarAnimator>
	{ 
		protected override void on_rescale(HangarAnimator module, HangarAnimator base_module, Scale scale)
		{ module.EnergyConsumption = base_module.EnergyConsumption * scale.absolute.quad * scale.absolute.aspect; }
	}

	public class ReactionWheelUpdater : ModuleUpdater<ModuleReactionWheel>
	{
		protected override void on_rescale(ModuleReactionWheel module, ModuleReactionWheel base_module, Scale scale)
		{
			module.PitchTorque  = base_module.PitchTorque * scale.absolute.cube * scale.absolute.aspect;
			module.YawTorque    = base_module.YawTorque   * scale.absolute.cube * scale.absolute.aspect;
			module.RollTorque   = base_module.RollTorque  * scale.absolute.cube * scale.absolute.aspect;
			var input_resources = base_module.inputResources.ToDictionary(r => r.name);
			module.inputResources.ForEach(r => r.rate = input_resources[r.name].rate * scale.absolute.cube * scale.absolute.aspect);
		}
	}

	public class GenericInflatableUpdater : ModuleUpdater<HangarGenericInflatable>
	{
		protected override void on_rescale(HangarGenericInflatable module, HangarGenericInflatable base_module, Scale scale)
		{
			module.InflatableVolume = base_module.InflatableVolume * scale.absolute.cube * scale.absolute.aspect;
			module.CompressedGas   *= scale.relative.cube * scale.relative.aspect;
			module.ForwardSpeed     = base_module.ForwardSpeed / (scale.absolute * scale.aspect);
			module.ReverseSpeed     = base_module.ReverseSpeed / (scale.absolute * scale.aspect);
			if(module.Compressor == null) return;
			module.Compressor.ConsumptionRate = base_module.Compressor.ConsumptionRate * scale.absolute.cube * scale.absolute.aspect;
		}
	}

	public class GeneratorUpdater : ModuleUpdater<ModuleGenerator>
	{
		protected override void on_rescale(ModuleGenerator module, ModuleGenerator base_module, Scale scale)
		{
			var input_resources  = base_module.inputList.ToDictionary(r => r.name);
			var output_resources = base_module.outputList.ToDictionary(r => r.name);
			module.inputList.ForEach(r =>  r.rate = input_resources[r.name].rate  * scale.absolute.cube * scale.absolute.aspect);
			module.outputList.ForEach(r => r.rate = output_resources[r.name].rate * scale.absolute.cube * scale.absolute.aspect);
		}
	}

	public class SolarPanelUpdater : ModuleUpdater<ModuleDeployableSolarPanel>
	{
		protected override void on_rescale(ModuleDeployableSolarPanel module, ModuleDeployableSolarPanel base_module, Scale scale)
		{
			module.chargeRate = base_module.chargeRate * scale.absolute.quad * scale.absolute.aspect; 
			module.flowRate   = base_module.flowRate   * scale.absolute.quad * scale.absolute.aspect; 
		}
	}

	public class DecoupleUpdater : ModuleUpdater<ModuleDecouple>
	{
		protected override void on_rescale(ModuleDecouple module, ModuleDecouple base_module, Scale scale)
		{ module.ejectionForce = base_module.ejectionForce * scale.absolute; }
	}

	public class SwitchableTankUpdater : ModuleUpdater<HangarSwitchableTank>
	{
		protected override void on_rescale(HangarSwitchableTank module, HangarSwitchableTank base_module, Scale scale)
		{ module.Volume *= scale.relative.cube * scale.relative.aspect;	}
	}

	public class ResourceConverterUpdater : ModuleUpdater<AnimatedConverterBase>
	{
		protected override void on_rescale(AnimatedConverterBase module, AnimatedConverterBase base_module, Scale scale)
		{
			module.EnergyConsumption = base_module.EnergyConsumption * scale.absolute.cube * scale.absolute.aspect;
			module.SetRatesMultiplier(base_module.RatesMultiplier * scale.absolute.cube * scale.absolute.aspect); 
		}
	}

	public class TankManagerUpdater : ModuleUpdater<HangarTankManager>
	{
		protected override void on_rescale(HangarTankManager module, HangarTankManager base_module, Scale scale)
		{ 
			module.RescaleTanks(scale.relative.cube * scale.relative.aspect); 
			module.Volume *= scale.relative.cube * scale.relative.aspect;
		}
	}

	public class HangarLightUpdater : ModuleUpdater<HangarLight>
	{
		protected override void on_rescale(HangarLight module, HangarLight base_module, Scale scale)
		{ 
			module.RangeMultiplier = base_module.RangeMultiplier * scale;
			module.UpdateLights(); 
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

		protected override void on_rescale(ModuleEngines module, ModuleEngines base_module, Scale scale)
		{
			module.minThrust = base_module.minThrust * scale.absolute.quad;
			module.maxThrust = base_module.maxThrust * scale.absolute.quad;
//			module.heatProduction = base_module.heatProduction * scale.absolute;
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

		protected override void on_rescale(ModuleEnginesFX module, ModuleEnginesFX base_module, Scale scale)
		{
			module.minThrust = base_module.minThrust * scale.absolute.quad;
			module.maxThrust = base_module.maxThrust * scale.absolute.quad;
//			module.heatProduction = base_module.heatProduction * scale.absolute;
		}
	}

	public class ResourceIntakeUpdater : ModuleUpdater<ModuleResourceIntake>
	{
		protected override void on_rescale(ModuleResourceIntake module, ModuleResourceIntake base_module, Scale scale)
		{ module.area = base_module.area * scale.absolute.quad; }
	}
}
