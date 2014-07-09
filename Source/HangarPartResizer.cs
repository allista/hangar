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
		private bool just_loaded = false;
		
		private Dictionary<string,int> orig_sizes = new Dictionary<string, int>();
		
		
		private Vector3 scale_vector(Vector3 v, float s, float l)
		{ return Vector3.Scale(v, new Vector3(s, s*l, s)); }
		
		public override void OnStart (StartState state)
		{
			base.OnStart (state);
			//check if the part has the hangar module
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
			//save original sizes of nodes
			foreach(AttachNode node in part.attachNodes)
				orig_sizes[node.id] = node.size;
			updateNodeSizes();
		}
		
		public override void OnLoad (ConfigNode cfg)
		{
			base.OnLoad(cfg);
			just_loaded = true;
		}
		
		public void FixedUpdate ()
		{
			if (size != old_size || aspect != old_aspect) resizePart();
			just_loaded = false;
		}
		
		private void updateDockingNode()
		{
			ModuleDockingNode dock = part.Modules.OfType<ModuleDockingNode>().SingleOrDefault();
			if(dock == null) return;
			AttachNode node = part.findAttachNode(dock.referenceAttachNode);
			if(node == null) return;
			dock.nodeType = string.Format("size{0}", node.size);
		}
		
		private void updateNodeSizes()
		{
			foreach(AttachNode node in part.attachNodes)
			{
				int new_size = orig_sizes[node.id] + Mathf.RoundToInt(size/orig_size/sizeStepLarge) - 1;
				if(new_size < 0) new_size = 0;
				node.size = new_size;
			}
			updateDockingNode();
		}
		
		private void scaleNodes(float scale, float len)
		{
			foreach(AttachNode node in part.attachNodes)
			{
				node.position = scale_vector(node.originalPosition, scale, len);
				if (!just_loaded)
					Utils.updateAttachedPartPos(node, part);
			}
		}

		
		public void resizePart()
		{
			//calculate scale
			float scale = size/orig_size; 
			//change scale
			Transform model = part.FindModelTransform ("model");
			if(model != null) model.localScale = scale_vector(Vector3.one, scale, aspect);
			else Debug.LogError ("[HangarPartResizer] No 'model' transform in the part", this);
			//recalculate mass
			part.mass   = ((specificMass.x * scale + specificMass.y) * scale + specificMass.z) * scale * aspect + specificMass.w;
			massDisplay = Utils.formatMass (part.mass);
			//change breaking forces
			part.breakingForce  = specificBreakingForce  * Mathf.Pow(scale, 2);
			part.breakingTorque = specificBreakingTorque * Mathf.Pow(scale, 2);
			//change volume if the part is a hangar, and let the hangar module handle mass calculations
			Hangar hangar = part.Modules.OfType<Hangar>().SingleOrDefault();
			if(hangar != null) hangar.Setup();
			//update nodes
			scaleNodes(scale, aspect);
			updateNodeSizes();
			//save size and aspect
			old_size   = size;
			old_aspect = aspect;
		}
	}
}