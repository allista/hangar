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
		[UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.25f, incrementSmall=0.125f, incrementSlide=0.001f)]
		public float size = 1.0f;
		
		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Length", guiFormat="S4")]
		[UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.0f, incrementSmall=0.1f, incrementSlide=0.001f)]
		public float length = 1.0f;
		
		[KSPField] public bool sizeOnly = false;
		[KSPField] public bool lengthOnly = false;
		[KSPField] public float offset = 1;
		
		[KSPField] public float minSize = 0.5f;
		[KSPField] public float maxSize = 10f;
		
		[KSPField] public float sizeStepLarge = 1.0f;
		[KSPField] public float sizeStepSmall = 0.1f;
		
		[KSPField] public float lengthStepLarge = 1.0f;
		[KSPField] public float lengthStepSmall = 0.1f;
		
		[KSPField] public Vector4 specificMass = new Vector4(0.005f, 0.011f, 0.009f, 0f);
		[KSPField] public float specificBreakingForce  = 1536;
		[KSPField] public float specificBreakingTorque = 1536;
		
		[KSPField] public string minSizeName = "HANGAR_MINSCALE";
		[KSPField] public string maxSizeName = "HANGAR_MAXSCALE";
		
		[KSPField(isPersistant=false, guiActive=false, guiActiveEditor=true, guiName="Mass")]
		public string massDisplay;
		
		private float old_size   = -1000;
		private float old_length = -1000;
		private bool just_loaded = false;
		
		private Dictionary<string,int> orig_sizes = new Dictionary<string, int>();
		
		
		private Vector3 scale_vector(Vector3 v, float s, float l)
		{ return Vector3.Scale(v, new Vector3(s, s*l, s)); }
		
		public override void OnStart (StartState state)
		{
			base.OnStart (state);
			if (HighLogic.LoadedSceneIsEditor) 
			{
				//calculate min and max sizes from tech tree and module fields
				float min_Size = Utils.getTechMinValue (minSizeName, 0.5f)/offset;
				float max_Size = Utils.getTechMaxValue (maxSizeName, 10)/offset;
				if(max_Size < 1) max_Size = 1;
				//truncate min-max values at hard limits
				if(minSize < min_Size) minSize = min_Size;
				if(maxSize > max_Size) maxSize = max_Size;
				//setup sliders
				if(sizeOnly && lengthOnly) lengthOnly = false;
				if(!lengthOnly)
				{
					Utils.setFieldRange (Fields ["size"], minSize, maxSize);
					((UI_FloatEdit)Fields ["size"].uiControlEditor).incrementLarge = sizeStepLarge;
					((UI_FloatEdit)Fields ["size"].uiControlEditor).incrementSmall = sizeStepSmall;
				}
				else Fields["size"].guiActiveEditor=false;
				if(!sizeOnly)
				{
					Utils.setFieldRange (Fields ["length"], minSize, maxSize);
					((UI_FloatEdit)Fields ["length"].uiControlEditor).incrementLarge = lengthStepLarge;
					((UI_FloatEdit)Fields ["length"].uiControlEditor).incrementSmall = lengthStepSmall;
				}
				else Fields["length"].guiActiveEditor=false;
			}
			updateNodeSizes(size);
		}
		
		public override void OnLoad (ConfigNode cfg)
		{
			base.OnLoad(cfg);
			just_loaded = true;
			foreach(AttachNode node in part.attachNodes)
			{
				orig_sizes[node.id] = node.size;
				Debug.Log(string.Format("1 node id: {0}", node.id));
			}
			if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
				updateNodeSizes(size);
		}
		
		public virtual void FixedUpdate ()
		{
			if (size != old_size || length != old_length) 
				resizePart(size, length);
			just_loaded = false;
		}
		
		public virtual void updateDockingNode()
		{
			ModuleDockingNode dock = part.Modules.OfType<ModuleDockingNode>().SingleOrDefault();
			if(dock == null) return;
			AttachNode node = part.findAttachNode(dock.referenceAttachNode);
			if(node == null) return;
			dock.nodeType = string.Format("size{0}", node.size);
		}
		
		public virtual void updateNodeSizes(float scale)
		{
			foreach(AttachNode node in part.attachNodes)
			{
				Debug.Log(string.Format("2 node id: {0}", node.id));
				int new_size = orig_sizes[node.id] + Mathf.RoundToInt(scale/sizeStepLarge) - 1;
				if(new_size < 0) new_size = 0;
				node.size = new_size;
			}
			updateDockingNode();
		}
		
		public void scaleNodes(float scale, float len)
		{
			foreach(AttachNode node in part.attachNodes)
			{
				node.position = scale_vector(node.originalPosition, scale, len);
				if (!just_loaded)
					Utils.updateAttachedPartPos(node, part);
			}
		}

		
		public virtual void resizePart (float scale, float len)
		{
			old_size   = size;
			old_length = length;
		
			//change mass and forces
			part.mass  = ((specificMass.x * scale + specificMass.y) * scale + specificMass.z * len) * scale + specificMass.w;
			massDisplay = Utils.formatMass (part.mass);
			part.breakingForce = specificBreakingForce * Mathf.Pow (scale, 2);
			part.breakingTorque = specificBreakingTorque * Mathf.Pow (scale, 2);
			
			//change scale
			Transform model = part.FindModelTransform ("model");
			if(model != null)
				model.localScale = scale_vector(Vector3.one, scale, len);
			else
				Debug.LogError ("[HangarPartResizer] No 'model' transform in the part", this);
			
			//change volume if the part is a hangar
			Hangar hangar = part.Modules.OfType<Hangar>().SingleOrDefault();
			if(hangar != null) hangar.Setup();
		
			scaleNodes(scale, len);
			updateNodeSizes(scale);
		}
	}
}

