// This code is based on Procedural Fairings plug-in by Alexey Volynskov, KzPartResizer class
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using KSPAPIExtensions;

namespace AtHangar
{
	public class HangarPartResizer : PartModule
	{
		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Scale", guiFormat="S4")]
		[UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.1f, maxValue=10, incrementLarge=1.25f, incrementSmall=0.125f, incrementSlide=0.001f)]
		public float size=1.0f;
		
		[KSPField] public float sizeStepLarge = 1f;
		[KSPField] public float sizeStepSmall = 0.1f;
		
		[KSPField] public Vector4 specificMass = new Vector4(0.005f, 0.011f, 0.009f, 0f);
		[KSPField] public float specificBreakingForce  = 1536;
		[KSPField] public float specificBreakingTorque = 1536;
		
		[KSPField] public string minSizeName = "HANGAR_MINSCALE";
		[KSPField] public string maxSizeName = "HANGAR_MAXSCALE";
		
		[KSPField(isPersistant=false, guiActive=false, guiActiveEditor=true, guiName="Mass")]
		public string massDisplay;
		
		protected float old_size   = -1000;
		protected bool just_loaded = false;
		
		
		public override void OnStart (StartState state)
		{
			base.OnStart (state);
			
			if (HighLogic.LoadedSceneIsEditor) {
				float minSize = Utils.getTechMinValue (minSizeName, 0.1f);
				float maxSize = Utils.getTechMaxValue (maxSizeName, 10);
			
				Utils.setFieldRange (Fields ["size"], minSize, maxSize);
			
				((UI_FloatEdit)Fields ["size"].uiControlEditor).incrementLarge = sizeStepLarge;
				((UI_FloatEdit)Fields ["size"].uiControlEditor).incrementSmall = sizeStepSmall;
			}
			
			updateNodeSize (size);
		}
		
		public override void OnLoad (ConfigNode cfg)
		{
			base.OnLoad (cfg);
			just_loaded = true;
			if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
				updateNodeSize (size);
		}
		
		public virtual void FixedUpdate ()
		{
			if (size != old_size) resizePart (size);
			just_loaded = false;
		}
		
		public void scaleNode (AttachNode node, float scale, bool setSize)
		{
			if (node == null)
				return;
			node.position = node.originalPosition * scale;
			if (!just_loaded)
				Utils.updateAttachedPartPos (node, part);
			if (setSize)
				node.size = Mathf.RoundToInt (scale / sizeStepLarge);
		}
		
		public void setNodeSize (AttachNode node, float scale)
		{
			if (node == null)
				return;
			node.size = Mathf.RoundToInt (scale / sizeStepLarge);
		}
		
		public virtual void updateNodeSize (float scale)
		{
			setNodeSize (part.findAttachNode ("top"), scale);
			setNodeSize (part.findAttachNode ("bottom"), scale);
		}
		
		public virtual void resizePart (float scale)
		{
			old_size = size;
		
			//change mass and forces
			part.mass = ((specificMass.x * scale + specificMass.y) * scale + specificMass.z) * scale + specificMass.w;
			massDisplay = Utils.formatMass (part.mass);
			part.breakingForce = specificBreakingForce * Mathf.Pow (scale, 2);
			part.breakingTorque = specificBreakingTorque * Mathf.Pow (scale, 2);
			
			//change scale
			var model = part.FindModelTransform ("model");
			if (model != null)
				model.localScale = Vector3.one * scale;
			else
				Debug.LogError ("[AtPartResizer] No 'model' transform in the part", this);
			
			//change volume if the part is a hangar
			Hangar hangar = part.Modules.OfType<Hangar>().SingleOrDefault();
			if(hangar != null) hangar.Setup();
		
			scaleNode (part.findAttachNode ("top"), scale, true);
			scaleNode (part.findAttachNode ("bottom"), scale, true);
		}
	}
}

