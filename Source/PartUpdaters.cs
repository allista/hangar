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
			var all_updaters = get_all_types()
				.Where(IsPartUpdater)
				.ToArray();
			foreach (var updater in all_updaters)
			{
				MethodInfo register = typeof(PartUpdater).GetMethod("RegisterUpdater");
				register = register.MakeGenericMethod(new [] { updater });
				register.Invoke(null, null);
			}
		}

		static bool IsPartUpdater(Type t)
		{ return !t.IsGenericType && typeof(PartUpdater).IsAssignableFrom(t); }
	}
	#endregion

	public class PartUpdaterBase : PartModule
	{
		protected Part base_part;

		public static Vector3 ScaleVector(Vector3 v, float s, float l)
		{ return Vector3.Scale(v, new Vector3(s, s*l, s)); }

		public virtual void Init() 
		{ base_part = PartLoader.getPartInfoByName(part.partInfo.name).partPrefab; }

		protected virtual void SaveDefaults() {}
	}

	public class PartUpdater : PartUpdaterBase
	{
		public uint priority = 0; // 0 is highest

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
		{ foreach(AttachNode node in base_part.attachNodes) orig_nodes[node.id] = node; }

		public override void OnRescale(Scale scale)
		{
			//update attach nodes and their parts
			foreach(AttachNode node in part.attachNodes)
			{
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
				if(!scale.FirstTime)
				{
					Vector3 d_pos = part.transform.TransformDirection(part.srfAttachNode.position - old_position);
					part.transform.position -= d_pos;
				}
			}
			//no need to update surface attached parts for the first time
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
			if(base_part.breakingForce == 22f) part.breakingForce = 32.0f * scale.absolute.quad; //taken from TweakScale
			else part.breakingForce = base_part.breakingForce * scale.absolute.quad;
			if (part.breakingForce < 22f) part.breakingForce = 22f;
			if(base_part.breakingTorque == 22f) part.breakingTorque = 32.0f * scale.absolute.quad;
			else part.breakingTorque = base_part.breakingTorque * scale.absolute.quad;
			if(part.breakingTorque < 22f) part.breakingTorque = 22f;
			//change other properties
			part.buoyancy = base_part.buoyancy * scale.absolute.cube * scale.absolute.aspect;
			part.explosionPotential = base_part.explosionPotential * scale.absolute.cube * scale.absolute.aspect;
		}
	}

	public class ResourcesUpdater : PartUpdater
	{
		public override void OnRescale(Scale scale)
		{
			foreach(PartResource r in part.Resources)
			{
				var s = r.resourceName == "AblativeShielding"? 
					scale.relative.quad : scale.relative.cube * scale.relative.aspect;
				r.amount *= s; r.maxAmount *= s;
			}
		}
	}

	public class ModuleUpdater<T> : PartUpdater where T : PartModule
	{
		protected T module;
		protected T base_module;

		public override void Init() 
		{
			base.Init();
			priority = 100; 
			module = part.GetModule<T>();
			base_module = base_part.GetModule<T>();
			if(module == null) 
				throw new MissingComponentException(string.Format("[Hangar] ModuleUpdater: part {0} does not have {1} module", part.name, module));
			SaveDefaults();
		}

		protected override void SaveDefaults() {}
		public override void OnRescale(Scale scale) {}
	}

	public class RCS_Updater : ModuleUpdater<ModuleRCS>
	{
		[KSPField(isPersistant=false, guiActiveEditor=true, guiActive=true, guiName="Thrust")]
		public string thrustDisplay;
		float thrust;
		protected override void SaveDefaults()	{ thrust = base_module.thrusterPower; thrustDisplay = thrust.ToString(); }
		public override void OnRescale(Scale scale) { module.thrusterPower = thrust*scale.absolute.quad; thrustDisplay =  module.thrusterPower.ToString(); }
	}

	public class DockingNodeUpdater : ModuleUpdater<ModuleDockingNode>
	{
		public override void OnRescale(Scale scale)
		{
			AttachNode node = part.findAttachNode(module.referenceAttachNode);
			if(node == null) return;
			if(module.nodeType.StartsWith("size"))
				module.nodeType = string.Format("size{0}", node.size);
		}
	}

	public class PassageUpdater : ModuleUpdater<HangarPassage>
	{ 
		public override void OnRescale(Scale scale) 
		{ 
			module.Setup(true);
			foreach(var key in new List<string>(module.Nodes.Keys))
				module.Nodes[key].Size = Vector3.Scale(base_module.Nodes[key].Size, 
													   new Vector3(scale, scale, 1));
		}
	}

	public class HangarUpdater : ModuleUpdater<Hangar>
	{ 
		public override void OnRescale(Scale scale) 
		{ 
			module.Setup(true);
			module.EnergyConsumption = base_module.EnergyConsumption * scale.absolute.quad * scale.absolute.aspect; 
		}
	}

	public class AnimatorUpdater : ModuleUpdater<HangarAnimator>
	{ 
		public override void OnRescale(Scale scale) 
		{ module.EnergyConsumption = base_module.EnergyConsumption * scale.absolute.quad * scale.absolute.aspect; }
	}

	public class ReactionWheelUpdater : ModuleUpdater<ModuleReactionWheel>
	{
		readonly Dictionary<string,ModuleResource> input_resources = new Dictionary<string, ModuleResource>();
		protected override void SaveDefaults()
		{ base_module.inputResources.ForEach(r => input_resources.Add(r.name, r)); }

		public override void OnRescale(Scale scale)
		{
			module.PitchTorque = base_module.PitchTorque * scale.absolute.cube * scale.absolute.aspect;
			module.YawTorque   = base_module.YawTorque   * scale.absolute.cube * scale.absolute.aspect;
			module.RollTorque  = base_module.RollTorque  * scale.absolute.cube * scale.absolute.aspect;
			module.inputResources.ForEach(r => r.rate = input_resources[r.name].rate * scale.absolute.cube * scale.absolute.aspect);
		}
	}

	public class GenericInflatableUpdater : ModuleUpdater<HangarGenericInflatable>
	{
		public override void OnRescale(Scale scale)
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
		readonly Dictionary<string, ModuleGenerator.GeneratorResource> input_resources  = new Dictionary<string, ModuleGenerator.GeneratorResource>();
		readonly Dictionary<string, ModuleGenerator.GeneratorResource> output_resources = new Dictionary<string, ModuleGenerator.GeneratorResource>();
		protected override void SaveDefaults()
		{ 
			base_module.inputList.ForEach(r => input_resources.Add(r.name, r)); 
			base_module.outputList.ForEach(r => output_resources.Add(r.name, r)); 
		}

		public override void OnRescale(Scale scale)
		{
			module.inputList.ForEach(r =>  r.rate = input_resources[r.name].rate  * scale.absolute.cube * scale.absolute.aspect);
			module.outputList.ForEach(r => r.rate = output_resources[r.name].rate * scale.absolute.cube * scale.absolute.aspect);
		}
	}

	public class SolarPanelUpdater : ModuleUpdater<ModuleDeployableSolarPanel>
	{
		public override void OnRescale(Scale scale)
		{ 
			module.chargeRate = base_module.chargeRate * scale.absolute.quad * scale.absolute.aspect; 
			module.flowRate   = base_module.flowRate   * scale.absolute.quad * scale.absolute.aspect; 
		}
	}

	public class DecoupleUpdater : ModuleUpdater<ModuleDecouple>
	{
		public override void OnRescale(Scale scale)
		{ module.ejectionForce = base_module.ejectionForce * scale.absolute; }
	}
}

