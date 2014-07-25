// This code is based on Procedural Fairings plug-in by Alexey Volynskov, KzPartResizer class
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSPAPIExtensions;

namespace AtHangar
{
	public class Scale
	{
		public class SimpleScale
		{
			public float scale { get; private set; }
			public float aspect { get; private set; }
			public float sqrt { get; private set; }
			public float quad { get; private set; }
			public float cube { get; private set; }

			public SimpleScale(float scale, float aspect)
			{ 
				this.scale = scale; 
				this.aspect = aspect;
				sqrt = (float)Math.Sqrt(scale);
				quad = scale*scale;
				cube = quad*scale;
			}

			public static implicit operator float(SimpleScale s) { return s.scale; }
		}

		public SimpleScale absolute;
		public SimpleScale relative;

		public float size { get; private set; }
		public float orig_size { get; private set; }
		public float aspect { get { return absolute.aspect; } }
		
		public Scale(float size, float old_size, float orig_size, float aspect, float old_aspect)
		{ 
			this.size      = size; 
			this.orig_size = orig_size; 
			absolute	   = new SimpleScale(size/orig_size, aspect);
			relative	   = new SimpleScale(size/old_size, aspect/old_aspect);
		}
		
		public static implicit operator float(Scale s) { return s.absolute; }
	}
	
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
	}
	
	public class NodesUpdater : PartUpdater
	{
		private Dictionary<string,int> orig_sizes = new Dictionary<string, int>();

		public override void Init() { base.Init(); SaveDefaults(); }
		protected override void SaveDefaults()
		{ foreach(AttachNode node in base_part.attachNodes) orig_sizes[node.id] = node.size; }

		private void updateAttachedPartPos(AttachNode node)
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
		private float thrust;
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
	

	public class HangarPartResizer : PartUpdater
	{
		public static string minSizeName   = "HANGAR_MINSCALE";
		public static string maxSizeName   = "HANGAR_MAXSCALE";
		public static string minAspectName = "HANGAR_MINASPECT";
		public static string maxAspectName = "HANGAR_MAXASPECT";

		//GUI
		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Size", guiFormat="S4")]
		[UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.0f, incrementSmall=0.1f, incrementSlide=0.001f)]
		public float size = 1.0f;
		
		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Aspect", guiFormat="S4")]
		[UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.0f, incrementSmall=0.1f, incrementSlide=0.001f)]
		public float aspect = 1.0f;

		[KSPField(isPersistant=false, guiActiveEditor=true, guiName="Mass")] 
		public string massDisplay;

		//module config
		[KSPField] public bool sizeOnly   = false;
		[KSPField] public bool aspectOnly = false;
		
		[KSPField] public float minSize = 0.5f;
		[KSPField] public float maxSize = 10f;
		
		[KSPField] public float minAspect = 0.5f;
		[KSPField] public float maxAspect = 10f;
		
		[KSPField] public float sizeStepLarge = 1.0f;
		[KSPField] public float sizeStepSmall = 0.1f;
		
		[KSPField] public float aspectStepLarge = 0.5f;
		[KSPField] public float aspectStepSmall = 0.1f;
		
		[KSPField] public Vector4 specificMass = new Vector4(1.0f, 1.0f, 1.0f, 0f);
		[KSPField] public Vector4 specificCost = new Vector4(1.0f, 1.0f, 1.0f, 0f);

		//state
		private float orig_size   = -1;
		private float old_size    = -1;
		private float old_aspect  = -1;
		private bool  just_loaded = true;
		private Scale scale { get { return new Scale(size, old_size, orig_size, aspect, old_aspect); } }
		
		#region ModuleUpdaters
		private static Dictionary<string, Func<Part, PartUpdater>> updater_types = new Dictionary<string, Func<Part, PartUpdater>>();
		
		private static Func<Part, PartUpdater> updaterConstructor<UpdaterType>() where UpdaterType : PartUpdater
		{ 
			return (Part part) => part.Modules.Contains(typeof(UpdaterType).Name) ? 
				part.Modules.OfType<UpdaterType>().FirstOrDefault() : 
				(UpdaterType)part.AddModule(typeof(UpdaterType).Name); 
		}

		public static void RegisterUpdater<UpdaterType>() 
			where UpdaterType : PartUpdater
		{ 
			string updater_name = typeof(UpdaterType).FullName;
			if(updater_types.ContainsKey(updater_name)) return;
			Debug.Log(string.Format("[Hangar] HangarPartResizer: registering {0}", updater_name));
			updater_types[updater_name] = updaterConstructor<UpdaterType>();
		}
		
		private List<PartUpdater> updaters = new List<PartUpdater>();
		
		private void create_updaters()
		{
			foreach(var updater_type in updater_types.Values) 
			{
				PartUpdater updater = updater_type(part);
				if(updater == null) continue;
				try { updater.Init(); }
				catch 
				{ 
					part.RemoveModule(updater); 
					continue; 
				}
				updaters.Add(updater);
			}
			updaters.Sort((a, b) => a.priority.CompareTo(b.priority));
		}
		#endregion
		
		//methods
		public override void OnAwake()
		{
			base.OnAwake();
			RegisterUpdater<NodesUpdater>();
			RegisterUpdater<RCS_Updater>();
			RegisterUpdater<DockingNodeUpdater>();
			RegisterUpdater<HangarUpdater>();
		}

		protected override void SaveDefaults()
		{
			part.partInfo = part.partInfo.CloneIfDefault();
			HangarPartResizer resizer = base_part.Modules.OfType<HangarPartResizer>().SingleOrDefault();
			if(resizer != null) orig_size  = resizer.size;
			old_size   = size;
			old_aspect = aspect;
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			Init(); 
			SaveDefaults();
			create_updaters();
			if(HighLogic.LoadedSceneIsEditor) 
			{
				//calculate min and max sizes from tech tree
				float min_Size   = Utils.getTechMinValue(minSizeName, 0.5f);
				float max_Size   = Utils.getTechMaxValue(maxSizeName, 10);
				float min_Aspect = Utils.getTechMinValue(minAspectName, 0.5f);
				float max_Aspect = Utils.getTechMaxValue(maxAspectName, 10);
				//truncate min-max values at hard limits
				if(minSize < min_Size) minSize = min_Size;
				if(maxSize > max_Size) maxSize = max_Size;
				if(minAspect < min_Aspect) minAspect = min_Aspect;
				if(maxAspect > max_Aspect) maxAspect = max_Aspect;
				//setup sliders
				if(sizeOnly && aspectOnly) aspectOnly = false;
				if(aspectOnly) Fields["size"].guiActiveEditor=false;
				else
				{
					Utils.setFieldRange (Fields ["size"], minSize, maxSize);
					((UI_FloatEdit)Fields ["size"].uiControlEditor).incrementLarge = sizeStepLarge;
					((UI_FloatEdit)Fields ["size"].uiControlEditor).incrementSmall = sizeStepSmall;
				}
				if(sizeOnly) Fields["aspect"].guiActiveEditor=false;
				else
				{
					Utils.setFieldRange (Fields ["aspect"], minAspect, maxAspect);
					((UI_FloatEdit)Fields ["aspect"].uiControlEditor).incrementLarge = aspectStepLarge;
					((UI_FloatEdit)Fields ["aspect"].uiControlEditor).incrementSmall = aspectStepSmall;
				}
			}
			just_loaded = true;
		}

		public void FixedUpdate() 
		{ 
			if(size != old_size || aspect != old_aspect || just_loaded) 
				{ Rescale(); just_loaded = false; } 
		}

		public void UpdateGUI()
		{ 
			massDisplay = Utils.formatMass(part.TotalMass());
			if(EditorLogic.fetch != null)
				GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		public override void OnRescale(Scale scale)
		{
			//change model scale
			Transform model = part.FindModelTransform("model");
			if(model != null) model.localScale = ScaleVector(Vector3.one, scale, aspect);
			else Debug.LogError ("[HangarPartResizer] No 'model' transform in the part", this);
			//recalculate mass
			part.mass = ((specificMass.x * scale + specificMass.y) * scale + specificMass.z) * scale * aspect + specificMass.w;
			//changing cost
			part.partInfo.cost = ((specificCost.x * scale + specificCost.y) * scale + specificCost.z) * scale * aspect + specificCost.w;
			//change breaking forces (if not defined in the config, set to a reasonable default)
			if (base_part.breakingForce == 22f) part.breakingForce = 32.0f * scale.absolute.quad; //taken from TweakScale
			else part.breakingForce = base_part.breakingForce * scale.absolute.quad;
			if (part.breakingForce < 22f) part.breakingForce = 22f;
			if (base_part.breakingTorque == 22f) part.breakingTorque = 32.0f * scale.absolute.quad;
			else part.breakingTorque = base_part.breakingTorque * scale.absolute.quad;
			if (part.breakingTorque < 22f) part.breakingTorque = 22f;
			//change other properties
			part.buoyancy = base_part.buoyancy * scale.absolute.cube;
			part.explosionPotential = base_part.explosionPotential * scale.absolute.cube;
			//update nodes and modules
			foreach(PartUpdater updater in updaters) updater.OnRescale(scale);
			//save size and aspect
			old_size   = size;
			old_aspect = aspect;
			UpdateGUI();
		}
		public void Rescale() { OnRescale(scale); }
	}
}