// This code is based on Procedural Fairings plug-in by Alexey Volynskov, KzPartResizer class
// And on ideas drawn from the TweakScale plugin
using System;
using System.Collections.Generic;
using UnityEngine;
using KSPAPIExtensions;

namespace AtHangar
{
	public class Scale
	{
		public class SimpleScale
		{
			public float scale  { get; private set; }
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

		readonly public SimpleScale absolute;
		readonly public SimpleScale relative;
		readonly public bool FirstTime;

		public float size { get; private set; }
		public float orig_size { get; private set; }
		public float aspect { get { return absolute.aspect; } }
		
		public Scale(float size, float old_size, float orig_size, float aspect, float old_aspect, bool first_time)
		{ 
			this.size      = size; 
			this.orig_size = orig_size; 
			absolute	   = new SimpleScale(size/orig_size, aspect);
			relative	   = new SimpleScale(size/old_size, aspect/old_aspect);
			FirstTime      = first_time;
		}
		
		public static implicit operator float(Scale s) { return s.absolute; }
	}


	public class HangarResizableBase : PartUpdaterBase, IPartCostModifier
	{
		public const string MIN_SIZE   = "HANGAR_MIN_SCALE";
		public const string MAX_SIZE   = "HANGAR_MAX_SCALE";
		public const string MIN_ASPECT = "HANGAR_MIN_ASPECT";
		public const string MAX_ASPECT = "HANGAR_MAX_ASPECT";

		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Aspect", guiFormat="S4")]
		[UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.0f, incrementSmall=0.1f, incrementSlide=0.001f)]
		public float aspect = 1.0f;

		[KSPField(isPersistant=false, guiActiveEditor=true, guiName="Mass")] 
		public string MassDisplay;

		//module config
		[KSPField] public float minSize = -1;
		[KSPField] public float maxSize = -1;
		[KSPField] public float sizeStepLarge = 1.0f;
		[KSPField] public float sizeStepSmall = 0.1f;

		[KSPField] public float minAspect = -1;
		[KSPField] public float maxAspect = -1;
		[KSPField] public float aspectStepLarge = 0.5f;
		[KSPField] public float aspectStepSmall = 0.1f;

		protected float old_aspect  = -1;
		[KSPField(isPersistant=true)] public float orig_aspect = -1;

		protected Transform model;
		public    float delta_cost  = 0f;
		protected bool  just_loaded = true;

		#region TechTree
		static bool have_tech(string name)
		{
			if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER) return name == "sandbox";
			return ResearchAndDevelopment.GetTechnologyState(name) == RDTech.State.Available;
		}

		static float get_tech_value(string name, float orig, Func<float, float, bool> compare)
		{
			float val = orig;
			foreach(var tech in GameDatabase.Instance.GetConfigNodes(name))
				foreach(ConfigNode.Value value in tech.values) 
				{
					if(!have_tech(value.name)) continue;
					float v = float.Parse(value.value);
					if(compare(v, val)) val = v;
				}
			return val;
		}

		protected static void init_limit(string name, ref float val, float orig, Func<float, float, bool> compare)
		{
			float _val = get_tech_value(name, orig, compare);
			if(val < 0 || compare(val, _val)) val = _val;
		}
		#endregion

		public void UpdateGUI(ShipConstruct ship)
		{ MassDisplay = Utils.formatMass(part.TotalMass()); }

		public override void OnAwake()
		{
			base.OnAwake();
			GameEvents.onEditorShipModified.Add(UpdateGUI);
		}
		void OnDestroy() { GameEvents.onEditorShipModified.Remove(UpdateGUI); }

		protected override void SaveDefaults()
		{
			base.SaveDefaults();
			if(orig_aspect < 0 || HighLogic.LoadedSceneIsEditor)
			{
				var resizer = base_part.GetModule<HangarResizableBase>();
				orig_aspect = resizer != null ? resizer.aspect : aspect;
			}
			old_aspect = aspect;
			model = part.FindModelTransform("model");
			if(model == null)
				Utils.Log("HangarPartResizer: no 'model' transform in the part", this);
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			Init(); 
			SaveDefaults();
			if(HighLogic.LoadedSceneIsEditor) 
			{
				init_limit(MIN_ASPECT, ref minAspect, aspect, (a, b) => a < b);
				init_limit(MAX_ASPECT, ref maxAspect, aspect, (a, b) => a > b);
			}
			just_loaded = true;
		}

		public float GetModuleCost() { return delta_cost; }
	}

	public class HangarPartResizer : HangarResizableBase
	{
		//GUI
		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Size", guiFormat="S4")]
		[UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.0f, incrementSmall=0.1f, incrementSlide=0.001f)]
		public float size = 1.0f;
		
		//module config
		[KSPField] public bool sizeOnly   = false;
		[KSPField] public bool aspectOnly = false;

		[KSPField] public Vector4 specificMass = new Vector4(1.0f, 1.0f, 1.0f, 0f);
		[KSPField] public Vector4 specificCost = new Vector4(1.0f, 1.0f, 1.0f, 0f);

		//state
		float old_size  = -1;
		[KSPField(isPersistant=true)] public float orig_size = -1;
		Scale scale { get { return new Scale(size, old_size, orig_size, aspect, old_aspect, just_loaded); } }
		
		#region PartUpdaters
		readonly List<PartUpdater> updaters = new List<PartUpdater>();
		
		void create_updaters()
		{
			foreach(var updater_type in PartUpdater.UpdatersTypes.Values) 
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
		protected override void SaveDefaults()
		{
			base.SaveDefaults();
			if(orig_size < 0 || HighLogic.LoadedSceneIsEditor)
			{
				var resizer = base_part.GetModule<HangarPartResizer>();
				orig_size = resizer != null ? resizer.size : size;
			}
			old_size = size;
			create_updaters();
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			if(HighLogic.LoadedSceneIsEditor) 
			{
				init_limit(MIN_SIZE, ref minSize, size, (a, b) => a < b);
				init_limit(MAX_SIZE, ref maxSize, size, (a, b) => a > b);
				//setup sliders
				if(sizeOnly && aspectOnly) aspectOnly = false;
				if(aspectOnly || minSize == maxSize) Fields["size"].guiActiveEditor=false;
				else
				{
					Utils.setFieldRange(Fields["size"], minSize, maxSize);
					((UI_FloatEdit)Fields["size"].uiControlEditor).incrementLarge = sizeStepLarge;
					((UI_FloatEdit)Fields["size"].uiControlEditor).incrementSmall = sizeStepSmall;
				}
				if(sizeOnly || minAspect == maxAspect) Fields["aspect"].guiActiveEditor=false;
				else
				{
					Utils.setFieldRange(Fields["aspect"], minAspect, maxAspect);
					((UI_FloatEdit)Fields["aspect"].uiControlEditor).incrementLarge = aspectStepLarge;
					((UI_FloatEdit)Fields["aspect"].uiControlEditor).incrementSmall = aspectStepSmall;
				}
			}
		}

		public void FixedUpdate() 
		{ 
			if(size != old_size || aspect != old_aspect) 
				{ Rescale(); part.BreakConnectedStruts(); }
			else if(just_loaded) 
				{ Rescale(); just_loaded = false; } 
		}

		public void Rescale()
		{
			if(model == null) return;
			Scale _scale = scale;
			//change model scale
			model.localScale = ScaleVector(Vector3.one, _scale, _scale.aspect);
			//recalculate mass
			part.mass  = ((specificMass.x * _scale + specificMass.y) * _scale + specificMass.z) * _scale * _scale.aspect + specificMass.w;
			//update nodes and modules
			foreach(PartUpdater updater in updaters) updater.OnRescale(_scale);
			//recalculate cost after all updaters
			delta_cost = ((specificCost.x * _scale + specificCost.y) * _scale + specificCost.z) * _scale * _scale.aspect + specificCost.w - part.DryCost();
			//save size and aspect
			old_size   = size;
			old_aspect = aspect;
			Utils.UpdateEditorGUI();
		}
	}
}