// This code is based on Procedural Fairings plug-in by Alexey Volynskov, KzPartResizer class
// And on ideas drawn from the TweakScale plugin
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

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
				register = register.MakeGenericMethod(new Type[] { updater });
				register.Invoke(null, null);
			}
		}

		static bool IsPartUpdater(Type t)
		{ return !t.IsGenericType && typeof(PartUpdater).IsAssignableFrom(t); }
	}
	#endregion


	public class PartUpdater : PartModule
	{
		public uint priority = 0; // 0 is highest
		protected Part base_part;

		public static Vector3 ScaleVector(Vector3 v, float s, float l)
		{ return Vector3.Scale(v, new Vector3(s, s*l, s)); }

		public virtual void Init() 
		{ base_part = PartLoader.getPartInfoByName(part.partInfo.name).partPrefab; }

		protected virtual void SaveDefaults() {}
		public virtual void OnRescale(Scale scale) {}

		#region ModuleUpdaters
		protected readonly static Dictionary<string, Func<Part, PartUpdater>> updater_types = new Dictionary<string, Func<Part, PartUpdater>>();

		static Func<Part, PartUpdater> updaterConstructor<UpdaterType>() where UpdaterType : PartUpdater
		{ 
			return part => 
				part.Modules.Contains(typeof(UpdaterType).Name) ? 
				part.Modules.OfType<UpdaterType>().FirstOrDefault() : 
				(UpdaterType)part.AddModule(typeof(UpdaterType).Name); 
		}

		public static void RegisterUpdater<UpdaterType>() 
			where UpdaterType : PartUpdater
		{ 
			string updater_name = typeof(UpdaterType).FullName;
			if(updater_types.ContainsKey(updater_name)) return;
			Utils.Log("PartUpdater: registering {0}", updater_name);
			updater_types[updater_name] = updaterConstructor<UpdaterType>();
		}
		#endregion
	}

	public class NodesUpdater : PartUpdater
	{
		readonly Dictionary<string,int> orig_sizes = new Dictionary<string, int>();

		public override void Init() { base.Init(); SaveDefaults(); }
		protected override void SaveDefaults()
		{ foreach(AttachNode node in base_part.attachNodes) orig_sizes[node.id] = node.size; }

		void updateAttachedPartPos(AttachNode node)
		{
			if(node == null) return;
			var ap = node.attachedPart; 
			if(!ap) return;
			var an = ap.findAttachNodeByPart(part);	
			if(an == null) return;
			var dp =
				part.transform.TransformPoint(node.position) -
				ap.transform.TransformPoint(an.position);
			if(ap == part.parent) 
			{
				while (ap.parent) ap = ap.parent;
				ap.transform.position += dp;
				part.transform.position -= dp;
			} 
			else ap.transform.position += dp;
		}

		public override void OnRescale(Scale scale)
		{
			//update attach nodes and their parts
			foreach(AttachNode node in part.attachNodes)
			{
				//update node position
				node.position = ScaleVector(node.originalPosition, scale, scale.aspect);
				updateAttachedPartPos(node);
				//update node size
				int new_size = orig_sizes[node.id] + Mathf.RoundToInt(scale.size-scale.orig_size);
				if(new_size < 0) new_size = 0;
				node.size = new_size;
			}
			//update this surface attach node
			if(part.srfAttachNode != null) 
				part.srfAttachNode.position = ScaleVector(part.srfAttachNode.originalPosition, scale, scale.aspect);
			//update parts that are surface attached to this
			foreach(Part child in part.children)
			{
				if (child.srfAttachNode != null && child.srfAttachNode.attachedPart == part) // part is attached to us, but not on a node
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
			part.buoyancy = base_part.buoyancy * scale.absolute.cube;
			part.explosionPotential = base_part.explosionPotential * scale.absolute.cube;
		}
	}

	public class ResourcesUpdater : PartUpdater
	{
		public override void OnRescale(Scale scale)
		{
			foreach(PartResource r in part.Resources)
			{
				r.amount *= scale.relative.cube * scale.relative.aspect;
				r.maxAmount *= scale.relative.cube * scale.relative.aspect;
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
			module = part.Modules.OfType<T>().SingleOrDefault();
			base_module = base_part.Modules.OfType<T>().SingleOrDefault();
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
			module.nodeType = string.Format("size{0}", node.size);
		}
	}

	public class HangarUpdater : ModuleUpdater<Hangar>
	{ public override void OnRescale(Scale scale) { module.Setup(true); } }

	public class ReactionWheelUpdater : ModuleUpdater<ModuleReactionWheel>
	{
		public override void OnRescale(Scale scale)
		{
			module.PitchTorque = base_module.PitchTorque * scale.absolute.cube * scale.absolute.aspect;
			module.YawTorque   = base_module.YawTorque   * scale.absolute.cube * scale.absolute.aspect;
			module.RollTorque  = base_module.RollTorque  * scale.absolute.cube * scale.absolute.aspect;
			foreach(ModuleResource r in	module.inputResources)
				r.rate *= scale.relative.cube;
		}
	}
}

