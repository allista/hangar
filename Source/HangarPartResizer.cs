// This code is based on Procedural Fairings plug-in by Alexey Volynskov, KzPartResizer class
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using KSPAPIExtensions;

namespace AtHangar
{
	public class Scale
	{
		public float size { get { return size; } set { size = value; scale = size/orig_size; } }
		public float orig_size { get; private set; }
		public float scale { get; private set; }
		public float aspect { get; private set; }
		
		public Scale(float size, float orig_size, float aspect)
		{ this.orig_size = orig_size; this.aspect = aspect; this.size = size; }
		
		public static implicit operator float(Scale s) { return s.scale; }
	}
	
	public abstract class PartUpdater
	{
		public uint priority = 0; //highest
		protected Part part;
		public abstract void SaveDefaults();
		public abstract void OnRescale(Scale scale);
	}
	
	public class NodesUpdater : PartUpdater
	{
		private Dictionary<string,int> orig_sizes = new Dictionary<string, int>();
		public NodesUpdater(Part part) { this.part = part; }
		public override void SaveDefaults()
		{ foreach(AttachNode node in part.attachNodes) orig_sizes[node.id] = node.size;	}
		public override void OnRescale(Scale scale)
		{
			foreach(AttachNode node in part.attachNodes)
			{
				//update node position
				node.position = Utils.ScaleVector(node.originalPosition, scale, scale.aspect);
				Utils.updateAttachedPartPos(node, part);
				//update node size
				int new_size = orig_sizes[node.id] + Mathf.RoundToInt(scale.size-scale.orig_size);
				if(new_size < 0) new_size = 0;
				node.size = new_size;
			}
			Debug.Log("[Hangar] Nodes updated");
		}
	}
	
	public class ModuleUpdater<T> : PartUpdater where T : PartModule
	{
		protected T module;
		
		public ModuleUpdater(Part part) 
		{
			this.priority = 100; 
			this.part = part;
			this.module = part.Modules.OfType<T>().SingleOrDefault();
			if(this.module == null) 
				throw new MissingComponentException(string.Format("[Hangar] ModuleUpdater: part {0} does not have {1} module", part.name, module));
			SaveDefaults();
		}
		public override void SaveDefaults()	{}
		public override void OnRescale(Scale scale) {}
	}
	
	public class RCS_Updater : ModuleUpdater<ModuleRCS>
	{
		private float thrust = -1;
		public RCS_Updater(Part part) : base(part) {}
		public override void SaveDefaults()	{ thrust = module.thrusterPower; }
		public override void OnRescale(Scale scale) { module.thrusterPower = thrust*scale; 
			Debug.Log("[Hangar] RCS updated"); }
	}
	
	public class DockingNodeUpdater : ModuleUpdater<ModuleDockingNode>
	{
		public DockingNodeUpdater(Part part) : base(part) {}
		public override void SaveDefaults() {}
		public override void OnRescale(Scale scale)
		{
			AttachNode node = part.findAttachNode(module.referenceAttachNode);
			if(node == null) return;
			module.nodeType = string.Format("size{0}", node.size);
			Debug.Log("[Hangar] Dock updated");
		}
	}
	
	public class HangarUpdater : ModuleUpdater<Hangar>
	{
		public HangarUpdater(Part part) : base(part) {}
		public override void SaveDefaults()	{}
		public override void OnRescale(Scale scale) { module.Setup(); 
			Debug.Log("[Hangar] Hangar updated"); }
	}
	
	
	public class HangarPartResizer : PartModule
	{
		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Size", guiFormat="S4")]
		[UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.0f, incrementSmall=0.1f, incrementSlide=0.001f)]
		public float size = 1.0f;
		
		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Aspect", guiFormat="S4")]
		[UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.0f, incrementSmall=0.1f, incrementSlide=0.001f)]
		public float aspect = 1.0f;
		
		[KSPField(isPersistant=true)] private float orig_size = -1;
		
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
		
		[KSPField] public Vector4 specificMass = new Vector4(0.005f, 0.011f, 0.009f, 0f);
		[KSPField] public float specificBreakingForce  = 1536;
		[KSPField] public float specificBreakingTorque = 1536;
		
		[KSPField] public string minSizeName   = "HANGAR_MINSCALE";
		[KSPField] public string maxSizeName   = "HANGAR_MAXSCALE";
		[KSPField] public string minAspectName = "HANGAR_MINASPECT";
		[KSPField] public string maxAspectName = "HANGAR_MAXASPECT";
		
		[KSPField(isPersistant=false, guiActiveEditor=true, guiName="Mass")]
		public string massDisplay;
		
		private float old_size   = -1000;
		private float old_aspect = -1000;
		
		#region ModuleUpdaters
		private static Dictionary<string, Func<Part, PartUpdater>> updater_types = new Dictionary<string, Func<Part, PartUpdater>>();
		
		public static void RegisterUpdater<UpdaterType>(Func<Part, PartUpdater> creator) 
			where UpdaterType : PartUpdater
		{ 
			string updater_name = typeof(UpdaterType).FullName;
			if(updater_types.ContainsKey(updater_name)) return;
			updater_types[updater_name] = creator;
		}
		
		private List<PartUpdater> updaters = new List<PartUpdater>();
		
		private void create_updaters()
		{
			foreach(var updater_type in updater_types.Values) 
			{
				PartUpdater updater;
				try { updater = updater_type(part); }
				catch { continue; }
				updaters.Add(updater);
			}
			updaters.Sort((a, b) => a.priority.CompareTo(b.priority));
		}
		#endregion
		
		//methods
		public override void OnAwake()
		{
			base.OnAwake();
			RegisterUpdater<NodesUpdater>((Part p) => new NodesUpdater(p));
			RegisterUpdater<RCS_Updater>((Part p) => new RCS_Updater(p));
			RegisterUpdater<DockingNodeUpdater>((Part p) => new DockingNodeUpdater(p));
			RegisterUpdater<HangarUpdater>((Part p) => new HangarUpdater(p));
		}
		
		public override void OnStart(StartState state)
		{
			base.OnStart (state);
			if (HighLogic.LoadedSceneIsEditor) 
			{
				//remember initial values
				if(orig_size < 0) { orig_size = size; aspect = 1; }
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
			//create module updaters
			create_updaters();
		}
		
		public void FixedUpdate()
		{ if(size != old_size || aspect != old_aspect) resizePart(); }
		
		public void resizePart()
		{
			//calculate scale
			Scale scale = new Scale(size, orig_size, aspect);
			//change scale
			Transform model = part.FindModelTransform ("model");
			if(model != null) model.localScale = Utils.ScaleVector(Vector3.one, scale, aspect);
			else Debug.LogError ("[HangarPartResizer] No 'model' transform in the part", this);
			//recalculate mass
			part.mass   = ((specificMass.x * scale + specificMass.y) * scale + specificMass.z) * scale * aspect + specificMass.w;
			massDisplay = Utils.formatMass (part.mass);
			//change breaking forces
			part.breakingForce  = specificBreakingForce  * Mathf.Pow(scale, 2);
			part.breakingTorque = specificBreakingTorque * Mathf.Pow(scale, 2);
			//update nodes and modules
			foreach(PartUpdater updater in updaters) updater.OnRescale(scale);
			//save size and aspect
			old_size   = size;
			old_aspect = aspect;
		}
	}
}