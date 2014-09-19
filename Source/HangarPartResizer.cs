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
		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Aspect", guiFormat="S4")]
		[UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.0f, incrementSmall=0.1f, incrementSlide=0.001f)]
		public float aspect = 1.0f;

		[KSPField(isPersistant=false, guiActiveEditor=true, guiName="Mass")] 
		public string massDisplay;

		//module config
		[KSPField] public float minSize = -1;
		[KSPField] public float maxSize = -1;

		[KSPField] public float minAspect = -1;
		[KSPField] public float maxAspect = -1;

		[KSPField] public float sizeStepLarge = 1.0f;
		[KSPField] public float sizeStepSmall = 0.1f;

		[KSPField] public float aspectStepLarge = 0.5f;
		[KSPField] public float aspectStepSmall = 0.1f;

		protected Transform model;
		protected float old_aspect  = -1;
		public    float dry_cost    = 0f; 
		public    float delta_cost  = 0f;
		protected bool  just_loaded = true;

		//methods
		public void UpdateGUI(ShipConstruct ship)
		{ massDisplay = Utils.formatMass(part.TotalMass()); }

		public override void OnAwake()
		{
			base.OnAwake();
			GameEvents.onEditorShipModified.Add(UpdateGUI);
		}
		void OnDestroy() { GameEvents.onEditorShipModified.Remove(UpdateGUI); }

		protected override void SaveDefaults()
		{
			old_aspect = aspect;
			dry_cost   = base_part.DryCost();
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
				//calculate min and max sizes from tech tree
				float min_Size   = Utils.getTechMinValue(Utils.minSizeName, 0.5f);
				float max_Size   = Utils.getTechMaxValue(Utils.maxSizeName, 10);
				float min_Aspect = Utils.getTechMinValue(Utils.minAspectName, 0.5f);
				float max_Aspect = Utils.getTechMaxValue(Utils.maxAspectName, 10);
				//and truncate min-max values at common limits; use common limits by default
				if(minSize < 0   || minSize < min_Size) minSize = min_Size;
				if(maxSize < 0   || maxSize > max_Size) maxSize = max_Size;
				if(minAspect < 0 || minAspect < min_Aspect) minAspect = min_Aspect;
				if(maxAspect < 0 || maxAspect > max_Aspect) maxAspect = max_Aspect;
			}
			just_loaded = true;
		}

//		public override void OnInitialize()
//		{
//			base.OnInitialize();
//			Utils.Log("HangarPartResizerBase.OnInitialize");
//			Init();
//			SaveDefaults();
//		}

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
		float orig_size = -1;
		float old_size  = -1;
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
			HangarPartResizer resizer = base_part.GetModule<HangarPartResizer>();
			if(resizer != null) orig_size  = resizer.size;
			old_size = size;
			create_updaters();
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			if(HighLogic.LoadedSceneIsEditor) 
			{
				//setup sliders
				if(sizeOnly && aspectOnly) aspectOnly = false;
				if(aspectOnly) Fields["size"].guiActiveEditor=false;
				else
				{
					Utils.setFieldRange(Fields["size"], minSize, maxSize);
					((UI_FloatEdit)Fields["size"].uiControlEditor).incrementLarge = sizeStepLarge;
					((UI_FloatEdit)Fields["size"].uiControlEditor).incrementSmall = sizeStepSmall;
				}
				if(sizeOnly) Fields["aspect"].guiActiveEditor=false;
				else
				{
					Utils.setFieldRange(Fields["aspect"], minAspect, maxAspect);
					((UI_FloatEdit)Fields["aspect"].uiControlEditor).incrementLarge = aspectStepLarge;
					((UI_FloatEdit)Fields["aspect"].uiControlEditor).incrementSmall = aspectStepSmall;
				}
			}
		}

//		public override void OnInitialize()
//		{
//			base.OnInitialize();
//			Rescale();
//		}

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
			delta_cost = ((specificCost.x * _scale + specificCost.y) * _scale + specificCost.z) * _scale * _scale.aspect + specificCost.w - dry_cost;
			//save size and aspect
			old_size   = size;
			old_aspect = aspect;
			Utils.UpdateEditorGUI();
		}
	}
}