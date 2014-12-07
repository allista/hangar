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
		public const string MIN_SIZE   = "MINSCALE";
		public const string MAX_SIZE   = "MAXSCALE";
		public const string MIN_ASPECT = "MINASPECT";
		public const string MAX_ASPECT = "MAXASPECT";

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

		protected Transform model { get { return part.transform.GetChild(0); } }
		public    float delta_cost;
		protected bool  just_loaded = true;

		#region TechTree
		static bool have_tech(string name)
		{
			if(HighLogic.CurrentGame.Mode != Game.Modes.CAREER) return name == "sandbox";
			return ResearchAndDevelopment.GetTechnologyState(name) == RDTech.State.Available;
		}

		static float get_tech_value(string name, float orig, Func<float, float, bool> compare)
		{
			float val = orig;
			foreach(var tech in HangarConfig.GetNodes(name))
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

		protected static void setup_field(BaseField field, float minval, float maxval, float l_increment, float s_increment)
		{
			var fe = field.uiControlEditor as UI_FloatEdit;
			if(fe != null) 
			{ 
				fe.minValue = minval;
				fe.maxValue = maxval;
				fe.incrementLarge = l_increment;
				fe.incrementSmall = s_increment;
			}
		}
		#endregion

		protected const float eps = 1e-5f;
		protected static bool unequal(float f1, float f2)
		{ return Mathf.Abs(f1-f2) > eps; }

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
			if(orig_aspect < 0 || HighLogic.LoadedSceneIsEditor)
			{
				var resizer = base_part.GetModule<HangarResizableBase>();
				orig_aspect = resizer != null ? resizer.aspect : aspect;
			}
			old_aspect = aspect;

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
		Vector3 old_local_scale;
		[KSPField(isPersistant=true)] public float orig_size = -1;
		Scale scale { get { return new Scale(size, old_size, orig_size, aspect, old_aspect, just_loaded); } }
		float orig_cost;
		
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
			old_size  = size;
			orig_cost = specificCost.x+specificCost.y+specificCost.z; //specificCost.w is eliminated anyway
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
				else setup_field(Fields["size"], minSize, maxSize, sizeStepLarge, sizeStepSmall);
				if(sizeOnly || minAspect == maxAspect) Fields["aspect"].guiActiveEditor=false;
				else setup_field(Fields["aspect"], minAspect, maxAspect, aspectStepLarge, aspectStepSmall);
			}
			Rescale();
		}

		public void Update()
		{
			if(!HighLogic.LoadedSceneIsEditor) return;
			if(old_local_scale != model.localScale) Rescale();
			else if(unequal(old_size, size) || unequal(old_aspect, aspect))
			{ Rescale(); part.BreakConnectedStruts(); }
		}

		public void Rescale()
		{
			if(model == null) return;
			Scale _scale = scale;
			//change model scale
			model.localScale = ScaleVector(Vector3.one, _scale, _scale.aspect);
			model.hasChanged = true;
			part.transform.hasChanged = true;
			//recalculate mass and cost
			part.mass  = ((specificMass.x*_scale + specificMass.y)*_scale + specificMass.z)*_scale * _scale.aspect + specificMass.w;
			delta_cost = ((specificCost.x*_scale + specificCost.y)*_scale + specificCost.z)*_scale * _scale.aspect - orig_cost; //specificCost.w is eliminated anyway
			//update nodes and modules
			updaters.ForEach(u => u.OnRescale(_scale));
			//save size and aspect
			old_size   = size;
			old_aspect = aspect;
			old_local_scale = model.localScale;
			Utils.UpdateEditorGUI();
			just_loaded = false;
		}
	}
}