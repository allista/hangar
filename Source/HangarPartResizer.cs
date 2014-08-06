// This code is based on Procedural Fairings plug-in by Alexey Volynskov, KzPartResizer class
// And on ideas drawn from the TweakScale plugin
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
	

	public class HangarPartResizer : PartUpdater, IPartCostModifier
	{
		public static readonly string minSizeName   = "HANGAR_MINSCALE";
		public static readonly string maxSizeName   = "HANGAR_MAXSCALE";
		public static readonly string minAspectName = "HANGAR_MINASPECT";
		public static readonly string maxAspectName = "HANGAR_MAXASPECT";

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
		float orig_size   = -1;
		float old_size    = -1;
		float old_aspect  = -1;
		public float dry_cost   = 0f; 
		public float delta_cost = 0f;
		bool  just_loaded = true;
		Scale scale { get { return new Scale(size, old_size, orig_size, aspect, old_aspect); } }
		
		#region PartUpdaters
		List<PartUpdater> updaters = new List<PartUpdater>();
		
		void create_updaters()
		{
			foreach(var updater_type in updater_types.Values) 
			{
				PartUpdater updater = updater_type(part);
				if(updater == null || updater == this) continue;
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
			GameEvents.onEditorShipModified.Add(UpdateGUI);
		}
		void OnDestroy() { GameEvents.onEditorShipModified.Remove(UpdateGUI); }

		protected override void SaveDefaults()
		{
			HangarPartResizer resizer = base_part.Modules.OfType<HangarPartResizer>().SingleOrDefault();
			if(resizer != null) orig_size  = resizer.size;
			old_size   = size;
			old_aspect = aspect;
			dry_cost   = base_part.DryCost();
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

		public void UpdateGUI(ShipConstruct ship)
		{ massDisplay = Utils.formatMass(part.TotalMass()); }

		public void UpdateGUI()
		{ if(EditorLogic.fetch != null)	GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship); }

		public override void OnRescale(Scale scale)
		{
			//change model scale
			Transform model = part.FindModelTransform("model");
			if(model != null)
				model.localScale = ScaleVector(Vector3.one, scale, aspect);
			else
			{
				Utils.Log("HangarPartResizer: no 'model' transform in the part", this);
				return;
			}
			//recalculate mass
			part.mass  = ((specificMass.x * scale + specificMass.y) * scale + specificMass.z) * scale * aspect + specificMass.w;
			//update nodes and modules
			foreach(PartUpdater updater in updaters) updater.OnRescale(scale);
			//recalculate cost after all updaters
			delta_cost = ((specificCost.x * scale + specificCost.y) * scale + specificCost.z) * scale * aspect + specificCost.w - dry_cost;
			//save size and aspect
			old_size   = size;
			old_aspect = aspect;
			UpdateGUI();
		}
		public void Rescale() { OnRescale(scale); }

		public float GetModuleCost() { return delta_cost; }
	}
}