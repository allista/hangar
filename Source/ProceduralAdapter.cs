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
		[KSPField] public float AreaDensity  = 2.7f*6e-3f; // 2.7t/m^3 * 1m^2 * 6mm: aluminium sheet 6mm thick
		[KSPField] public float UnitDiameter = 1.25f; // m
		[KSPField] public float Length = 1f; // m

		[KSPField] public string BodyName       = "adapter";
		[KSPField] public string ColliderName   = "collider";
		[KSPField] public string TopNodeName    = "top";
		[KSPField] public string BottomNodeName = "bottom";

		//state
		public  State<TruncatedCone> body;
		Vector2 size { get { return new Vector2(topSize, bottomSize); } }
		Vector2 orig_size  = new Vector2(-1, -1);
		Vector2 old_size   = new Vector2(-1, -1);
		Vector2 nodes_size = new Vector2(-1, -1);
		float   orig_area;
		public float SurfaceArea 
		{ get { return TruncatedCone.SurfaceArea(bottomSize*UnitDiameter/2, topSize*UnitDiameter/2, Length*aspect); } }

		Mesh body_mesh;
		MeshCollider body_collider;
		readonly Mesh collider_mesh = new Mesh();

		//methods
		public override string GetInfo() 
		{
			// OnStart(StartState.Editor); //does not work as SaveDefaults needs partInfo which is not there yet
			// Need to rescale everything to make it look good in the icon, but reenable otherwise OnStart won't get called again.
			// isEnabled = enabled = true;
			part.mass = SurfaceArea*AreaDensity;
			return base.GetInfo();
		}

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
			AttachNode top_node = base_part.findAttachNode(TopNodeName);
			if(top_node != null) nodes_size.x = top_node.size;
			AttachNode bottom_node = base_part.findAttachNode(BottomNodeName);
			if(bottom_node != null) nodes_size.y = bottom_node.size;
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
			update_body();
			get_part_components();
			just_loaded = true;
		}

		public void FixedUpdate() 
		{ 
			if(size != old_size || aspect != old_aspect || just_loaded) 
			{ UpdateMesh(); just_loaded = false; } 
		}

		void update_body()
		{
			float H  = Length*aspect;
			float Rb = bottomSize*UnitDiameter/2;
			float Rt = topSize*UnitDiameter/2;
			if(body == null) body = new State<TruncatedCone>(new TruncatedCone(Rb, Rt, H));
			else body.current = new TruncatedCone(Rb, Rt, H);
		}

		void update_nodes()
		{
			//update stack nodes
			AttachNode top_node = part.findAttachNode(TopNodeName);
			if(top_node != null)
			{
				top_node.position = new Vector3(0, body.current.H/2, 0);
				part.UpdateAttachedPartPos(top_node);
				int new_size = (int)nodes_size.x + Mathf.RoundToInt(topSize - orig_size.x);
				if(new_size < 0) new_size = 0;
				top_node.size = new_size;
			}
			AttachNode bottom_node = part.findAttachNode(BottomNodeName);
			if(bottom_node != null)
			{
				bottom_node.position = new Vector3(0, -body.current.H/2, 0);
				part.UpdateAttachedPartPos(bottom_node);
				int new_size = (int)nodes_size.y + Mathf.RoundToInt(bottomSize - orig_size.y);
				if(new_size < 0) new_size = 0;
				bottom_node.size = new_size;
			}
			//update attach nodes
			foreach(Part child in part.children)
			{
				if (child.srfAttachNode != null && child.srfAttachNode.attachedPart == part) // part is attached to us, but not on a node
				{
					//update child position
					Vector3 attachedPosition = child.transform.localPosition + child.transform.localRotation * child.srfAttachNode.position;
					attachedPosition.y *= aspect/old_aspect;
					Vector3 targetPosition = body.current.NewSurfacePosition(attachedPosition);
					child.transform.Translate(targetPosition - attachedPosition, part.transform);
					//update child orientation
					Basis old_basis = body.old.GetTangentalBasis(attachedPosition);
					Basis new_basis = body.current.GetTangentalBasis(attachedPosition);
					float fi  = Mathf.Acos(Vector3.Dot(old_basis.y, new_basis.y));
					float dir = Mathf.Sign(Vector3.Dot(Vector3.Cross(old_basis.y, new_basis.y), new_basis.x));
					Quaternion drot = Quaternion.AngleAxis(Mathf.Rad2Deg*fi*dir, new_basis.x);
					child.transform.localRotation = drot * child.transform.localRotation;
				}
			}
			part.BreakConnectedStruts();
		}

		public void UpdateMesh()
		{
			if(body_mesh == null || body_collider == null) return;
			update_body();
			//calculate number of sides and dimensions
			int sides = Mathf.RoundToInt(24+6*(Mathf.Max(topSize, bottomSize)-1));
			sides += sides%2; // make sides even
			//calculate surface area, mass and cost changes
			part.mass  = body.current.Area*AreaDensity;
			delta_cost = dry_cost*(body.current.Area/orig_area-1);
			//update meshes
			body.current.WriteTo(sides, body_mesh);
			body.current.WriteTo(sides/2, collider_mesh);
			body_collider.sharedMesh = collider_mesh;
			body_collider.enabled = false;
			body_collider.enabled = true;
			//update attach nodes
			update_nodes();
			//save new values
			old_size   = size;
			old_aspect = aspect;
			Utils.UpdateEditorGUI();
		}
	}
}

