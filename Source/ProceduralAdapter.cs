using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSPAPIExtensions;

namespace AtHangar
{
	public class HangarProceduralAdapter : HangarResizableBase
	{
		//GUI
		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Top Size", guiFormat="S4")]
		[UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.0f, incrementSmall=0.1f, incrementSlide=0.001f)]
		public float top_size = 1.0f;

		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Bottom Size", guiFormat="S4")]
		[UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.0f, incrementSmall=0.1f, incrementSlide=0.001f)]
		public float bottom_size = 1.0f;

		//module config
		[KSPField] float UnitDiameter = 1.25f;
		[KSPField] float Length = 1f;

		//state
		Vector2 size { get { return new Vector2(top_size, bottom_size); } }
		Vector2 orig_size = new Vector2(-1, -1);
		Vector2 old_size  = new Vector2(-1, -1);

		//methods
		protected override void SaveDefaults()
		{
			base.SaveDefaults();
			HangarProceduralAdapter adapter = base_part.GetModule<HangarProceduralAdapter>();
			if(adapter != null) orig_size  = adapter.size;
			old_size = size;
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			if(HighLogic.LoadedSceneIsEditor) 
			{
				//setup sliders
				Utils.setFieldRange(Fields["top_size"], minSize, maxSize);
				((UI_FloatEdit)Fields["top_size"].uiControlEditor).incrementLarge = sizeStepLarge;
				((UI_FloatEdit)Fields["top_size"].uiControlEditor).incrementSmall = sizeStepSmall;
				Utils.setFieldRange(Fields["bottom_size"], minSize, maxSize);
				((UI_FloatEdit)Fields["bottom_size"].uiControlEditor).incrementLarge = sizeStepLarge;
				((UI_FloatEdit)Fields["bottom_size"].uiControlEditor).incrementSmall = sizeStepSmall;
				Utils.setFieldRange (Fields ["aspect"], minAspect, maxAspect);
				((UI_FloatEdit)Fields ["aspect"].uiControlEditor).incrementLarge = aspectStepLarge;
				((UI_FloatEdit)Fields ["aspect"].uiControlEditor).incrementSmall = aspectStepSmall;
			}
			just_loaded = true;
		}


		public void FixedUpdate() 
		{ 
			if(size != old_size || aspect != old_aspect || just_loaded) 
			{ UpdateMesh(); just_loaded = false; } 
		}

		public void UpdateMesh()
		{

		}



		float mesh_area { get { return 0f; } } //TODO
	}
}

