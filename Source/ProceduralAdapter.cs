using System;
using UnityEngine;
using KSPAPIExtensions;

namespace AtHangar
{
	public class HangarProceduralAdapter : HangarResizableBase
	{
		//GUI
		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Top Size", guiFormat="S4")]
		[UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.0f, incrementSmall=0.1f, incrementSlide=0.001f)]
		public float topSize = 1.0f;

		[KSPField(isPersistant=true, guiActiveEditor=true, guiName="Bottom Size", guiFormat="S4")]
		[UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.0f, incrementSmall=0.1f, incrementSlide=0.001f)]
		public float bottomSize = 1.0f;

		//module config
		[KSPField] float AreaDensity  = 2.8f*6e-3f; // 2.8t/m^3 * 1m^2 * 6mm: aluminium sheet 6mm thick
		[KSPField] float UnitDiameter = 1.25f; // m
		[KSPField] float Length = 1f; // m

		[KSPField] public string BodyName = "adapter";
		[KSPField] public string ColliderName = "collider";

		//state
		Vector2 size { get { return new Vector2(topSize, bottomSize); } }
		Vector2 orig_size  = new Vector2(-1, -1);
		Vector2 old_size   = new Vector2(-1, -1);
		Vector2 nodes_size = new Vector2(-1, -1);

		float H, Rb, Rt, area;
		float orig_area;

		Material body_material;
		Mesh body_mesh;
		MeshCollider body_collider;
		readonly Mesh collider_mesh = new Mesh();

		public float SurfaceArea
		{ get { return Mathf.PI*(Rb*Rb + Rt*Rt + (Rb+Rt)*Mathf.Sqrt(H*H + Mathf.Pow(Rb-Rt, 2))); } }

		//methods
		protected override void SaveDefaults()
		{
			base.SaveDefaults();
			HangarProceduralAdapter adapter = base_part.GetModule<HangarProceduralAdapter>();
			if(adapter != null) 
			{
				orig_size = adapter.size;
				orig_area = adapter.SurfaceArea;
			}
			else Utils.Log("Can't find base ProceduralAdapter module");
			old_size = size;
			AttachNode top_node = base_part.findAttachNode("top");
			if(top_node != null) nodes_size.x = top_node.size;
			AttachNode bottom_node = base_part.findAttachNode("bottom");
			if(bottom_node != null) nodes_size.x = bottom_node.size;
		}

		void get_part_components()
		{
			try
			{
				//get transforms and meshes
				Transform bodyT = part.FindModelTransform(BodyName);
				if(bodyT == null)
					Utils.Log("ProceduralAdapter: '{0}' transform does not exists in the {1}", 
						BodyName, part.name);
				Transform colliderT = part.FindModelTransform(ColliderName);
				if(colliderT == null)
					Utils.Log("ProceduralAdapter: '{0}' transform does not exists in the {1}", 
						ColliderName, part.name);
				//The mesh method unshares any shared meshes
				MeshFilter body_mesh_filter = bodyT.GetComponent<MeshFilter>();
				if(body_mesh_filter == null)
					Utils.Log("ProceduralAdapter: '{0}' does not have MeshFilter component", BodyName);
				body_mesh = body_mesh_filter.mesh;
				body_collider = colliderT.GetComponent<MeshCollider>();
				if(body_collider == null)
					Utils.Log("ProceduralAdapter: '{0}' does not have MeshCollider component", ColliderName);
				//get material
				if(bodyT.renderer == null) 
					Utils.Log("{0} transform does not contain a renderer", BodyName);
				body_material = bodyT.renderer.material;
			}
			catch(Exception ex)
			{
				Debug.LogException(ex);
				isEnabled = enabled = false;
			}
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			if(HighLogic.LoadedSceneIsEditor) 
			{
				//setup sliders
				Utils.setFieldRange(Fields["topSize"], minSize, maxSize);
				((UI_FloatEdit)Fields["topSize"].uiControlEditor).incrementLarge = sizeStepLarge;
				((UI_FloatEdit)Fields["topSize"].uiControlEditor).incrementSmall = sizeStepSmall;
				Utils.setFieldRange(Fields["bottomSize"], minSize, maxSize);
				((UI_FloatEdit)Fields["bottomSize"].uiControlEditor).incrementLarge = sizeStepLarge;
				((UI_FloatEdit)Fields["bottomSize"].uiControlEditor).incrementSmall = sizeStepSmall;
				Utils.setFieldRange (Fields ["aspect"], minAspect, maxAspect);
				((UI_FloatEdit)Fields ["aspect"].uiControlEditor).incrementLarge = aspectStepLarge;
				((UI_FloatEdit)Fields ["aspect"].uiControlEditor).incrementSmall = aspectStepSmall;
			}
			get_part_components();
			//forbid surface attachment for the inflatable
			part.attachRules.allowSrfAttach = false;
			just_loaded = true;
		}

		public void FixedUpdate() 
		{ 
			if(size != old_size || aspect != old_aspect || just_loaded) 
			{ UpdateMesh(); just_loaded = false; } 
		}

		void update_nodes()
		{
			//update stack nodes
			AttachNode top_node = part.findAttachNode("top");
			if(top_node != null)
			{
				if(aspect != old_aspect) 
				{
					top_node.position = new Vector3(0, H/2, 0);
					updateAttachedPartPos(top_node);
				}
				int new_size = (int)nodes_size.x + Mathf.RoundToInt(topSize - orig_size.x);
				if(new_size < 0) new_size = 0;
				top_node.size = new_size;
			}
			AttachNode bottom_node = base_part.findAttachNode("bottom");
			if(bottom_node != null)
			{
				if(aspect != old_aspect) 
				{
					bottom_node.position = new Vector3(0, -H/2, 0);
					updateAttachedPartPos(bottom_node);
				}
				int new_size = (int)nodes_size.y + Mathf.RoundToInt(bottomSize - orig_size.y);
				if(new_size < 0) new_size = 0;
				bottom_node.size = new_size;
			}
			//update attach nodes
			foreach(Part child in part.children)
			{
				if (child.srfAttachNode != null && child.srfAttachNode.attachedPart == part) // part is attached to us, but not on a node
				{
					Vector3 attachedPosition = child.transform.localPosition + child.transform.localRotation * child.srfAttachNode.position;
					float y  = attachedPosition.y * aspect/old_aspect;
					float R2 = Mathf.Pow((Rt-Rb)/H*(y-H/2) + Rb, 2);
					float r2 = attachedPosition.x*attachedPosition.x + attachedPosition.z*attachedPosition.z;
					float x  = R2/r2*attachedPosition.x*attachedPosition.x;
					float z  = R2/r2*attachedPosition.z*attachedPosition.z;
					Vector3 targetPosition = new Vector3(x, y, z);
					child.transform.Translate(targetPosition - attachedPosition, part.transform);
				}
			}
			part.BreakConnectedStruts();
		}

		public void UpdateMesh()
		{
			if(body_mesh == null || body_collider == null) return;
			//calculate number of sides and dimensions
			int sides = Mathf.RoundToInt(24+6*(Mathf.Max(topSize, bottomSize)-1));
			sides += sides%2; // make sides even
			H      = Length*aspect;
			Rb     = bottomSize*UnitDiameter;
			Rt     = topSize*UnitDiameter;
			//calculate surface area, mass and cost changes
			area   = SurfaceArea;
			part.mass  = area*AreaDensity;
			delta_cost = dry_cost*area/orig_area;
			//make body and collider cones
			var body_cone = new TruncatedCone(Rb, Rt, H, sides);
			var collider_cone = new TruncatedCone(Rb, Rt, H, sides/2);
			//update meshes
			body_cone.WriteTo(body_mesh);
			collider_cone.WriteTo(collider_mesh);
			body_collider.sharedMesh = collider_mesh;
			body_collider.enabled = false;
			body_collider.enabled = true;
			//update attach nodes
			update_nodes();
			//save new values
			old_size   = size;
			old_aspect = aspect;
		}
	}
}

