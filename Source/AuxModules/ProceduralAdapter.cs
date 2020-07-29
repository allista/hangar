//   ProceduralAdapter.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using UnityEngine;
using AT_Utils;
using JetBrains.Annotations;

namespace AtHangar
{
    public class HangarProceduralAdapter : AnisotropicResizableBase
    {
        //GUI
        [KSPField(isPersistant=true, guiActiveEditor=true, guiName="Top Size", guiFormat="S4")]
        [UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.0f, incrementSmall=0.1f, incrementSlide=0.001f, sigFigs = 4)]
        public float topSize = 1.0f;

        [UsedImplicitly] private FloatFieldWatcher topSizeWatcher;

        [KSPField(isPersistant=true, guiActiveEditor=true, guiName="Bottom Size", guiFormat="S4")]
        [UI_FloatEdit(scene=UI_Scene.Editor, minValue=0.5f, maxValue=10, incrementLarge=1.0f, incrementSmall=0.1f, incrementSlide=0.001f, sigFigs = 4)]
        public float bottomSize = 1.0f;

        [UsedImplicitly] private FloatFieldWatcher bottomSizeWatcher;

        void update_and_break_struts()
        {
            UpdateMesh(); 
            part.BreakConnectedCompoundParts();
        }

        protected override void on_aspect_changed() => update_and_break_struts();

        //module config
        [KSPField] public float AreaCost     = 9f;
        [KSPField] public float AreaDensity  = 2.7f*6e-3f; // 2.7t/m^3 * 1m^2 * 6mm: aluminium sheet 6mm thick
        [KSPField] public float UnitDiameter = 1.25f; // m
        [KSPField] public float Length       = 1f;    // m
        [KSPField] public float UsableVolumeRatio = 1f;

        [KSPField] public string BodyName       = "adapter";
        [KSPField] public string ColliderName   = "collider";
        [KSPField] public string TopNodeName    = "top";
        [KSPField] public string BottomNodeName = "bottom";

        //state
        public  State<TruncatedCone> body;
        Vector2 size { get { return new Vector2(topSize, bottomSize); } }
        Vector2 old_size   = new Vector2(-1, -1);
        Vector2 orig_size  = new Vector2(-1, -1);
        readonly AttachNode[] orig_nodes = new AttachNode[2];
        public float SurfaceArea 
        { get { return TruncatedCone.SurfaceArea(bottomSize*UnitDiameter/2, topSize*UnitDiameter/2, Length*aspect); } }

        //part components
        Mesh body_mesh;
        MeshCollider body_collider;
        HangarPassage passage;

        //methods
        public override string GetInfo() 
        {
            prepare_model();
            part.mass = mass;
            return base.GetInfo();
        }

        protected override void prepare_model()
        {
            orig_nodes[0] = base_part.FindAttachNode(TopNodeName);
            orig_nodes[1] = base_part.FindAttachNode(BottomNodeName);
            var adapter = base_part.Modules.GetModule<HangarProceduralAdapter>();
            if(adapter != null)
                orig_size = adapter.size;
            else
                this.Log("Can't find base ProceduralAdapter module");
            get_part_components();
            update_body();
            update_attach_nodes();
        }

        private TruncatedCone new_cone(float top, float bottom, float asp)
        {
            var uR = UnitDiameter / 2;
            return new TruncatedCone(bottom * uR, top * uR, Length * asp);
        }

        protected override void update_orig_mass_and_cost()
        {
            var cone = new_cone(orig_size.x, orig_size.y, orig_aspect);
            orig_mass = cone.Area * AreaDensity;
            orig_cost = cone.Area * AreaCost;
        }

        public override void SaveDefaults()
        {
            old_size = size;
            base.SaveDefaults();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if(HighLogic.LoadedSceneIsEditor) 
            {
                //init global limits
                if(minSize < 0) minSize = ResizerGlobals.Instance.AbsMinSize;
                if(maxSize < 0) maxSize = ResizerGlobals.Instance.AbsMaxSize;
                //get TechTree limits
                var limits = ResizerConfig.GetLimits(TechGroupID);
                if(limits != null)
                {
                    init_limit(limits.minSize, ref minSize, Mathf.Min(topSize, bottomSize));
                    init_limit(limits.maxSize, ref maxSize, Mathf.Max(topSize, bottomSize));
                }
                //setup sliders
                setup_field(Fields["topSize"], minSize, maxSize, sizeStepLarge, sizeStepSmall);
                setup_field(Fields["bottomSize"], minSize, maxSize, sizeStepLarge, sizeStepSmall);
                setup_field(Fields["aspect"], minAspect, maxAspect, aspectStepLarge, aspectStepSmall);
                topSizeWatcher = new FloatFieldWatcher(Fields[nameof(topSize)])
                {
                    epsilon = 1e-4f, onValueChanged = update_and_break_struts
                };
                bottomSizeWatcher = new FloatFieldWatcher(Fields[nameof(bottomSize)])
                {
                    epsilon = 1e-4f, onValueChanged = update_and_break_struts
                };
            }
            StartCoroutine(CallbackUtil.WaitUntil(() => passage == null || passage.Ready, UpdateMesh));
        }

        void get_part_components()
        {
            passage = part.Modules.GetModule<HangarPassage>();
            try
            {
                //get transforms and meshes
                Transform bodyT = part.FindModelTransform(BodyName);
                if(bodyT == null)
                    this.Log("'{}' transform does not exists in the {1}", 
                        BodyName, part.name);
                Transform colliderT = part.FindModelTransform(ColliderName);
                if(colliderT == null)
                    this.Log("'{}' transform does not exists in the {1}", 
                        ColliderName, part.name);
                //The mesh method unshares any shared meshes
                MeshFilter body_mesh_filter = bodyT.GetComponent<MeshFilter>();
                if(body_mesh_filter == null)
                    this.Log("'{}' does not have MeshFilter component", BodyName);
                body_mesh = body_mesh_filter.mesh;
                body_collider = colliderT.GetComponent<MeshCollider>();
                if(body_collider == null)
                    this.Log("'{}' does not have MeshCollider component", ColliderName);
            }
            catch(Exception ex)
            {
                Debug.LogException(ex);
                isEnabled = enabled = false;
            }
        }

        void update_body()
        {
            //recalculate the cone
            var cone = new_cone(topSize, bottomSize, aspect);
            if(body == null)
                body = new State<TruncatedCone>(cone);
            else
                body.current = cone;
            //calculate number of sides and dimensions
            int sides = Mathf.RoundToInt(24+6*(Mathf.Max(topSize, bottomSize)-1));
            sides += sides%2; // make sides even
            //update meshes
            var collider_mesh = new Mesh();
            body_collider.enabled = false;
            body.current.WriteTo(sides, body_mesh);
            body.current.WriteTo(sides/2, collider_mesh, for_collider: true);
            Destroy(body_collider.sharedMesh);
            body_collider.sharedMesh = collider_mesh;
            body_collider.enabled = true;
            part.ResetModelSkinnedMeshRenderersCache();
            part.ResetModelMeshRenderersCache();
            part.ResetModelRenderersCache();
            //calculate mass and cost changes
            mass = body.current.Area*AreaDensity;
            cost = body.current.Area*AreaCost;
        }

        void update_passage()
        {
            //update passage nodes
            if(passage != null)
            {
                passage.Nodes[TopNodeName].Size = new Vector3(size.x, size.x, 0) * UnitDiameter * 0.9f;
                passage.Nodes[BottomNodeName].Size = new Vector3(size.y, size.y, 0) * UnitDiameter * 0.9f;
            }
        }

        void update_attach_nodes()
        {
            //update stack nodes
            AttachNode top_node = part.FindAttachNode(TopNodeName);
            if(top_node != null && orig_nodes[0] != null)
            {
                top_node.position = new Vector3(0, body.current.H/2, 0);
                int new_size = orig_nodes[0].size + Mathf.RoundToInt(topSize - orig_size.x);
                if(new_size < 0) new_size = 0;
                top_node.size = new_size;
                //update node breaking forces
                var s = topSize/orig_size.x;
                top_node.breakingForce  = orig_nodes[0].breakingForce  * s;
                top_node.breakingTorque = orig_nodes[0].breakingTorque * s;
                //move the part
                if(!just_loaded)
                    part.UpdateAttachedPartPos(top_node);
            }
            AttachNode bottom_node = part.FindAttachNode(BottomNodeName);
            if(bottom_node != null && orig_nodes[1] != null)
            {
                bottom_node.position = new Vector3(0, -body.current.H/2, 0);
                int new_size = orig_nodes[1].size + Mathf.RoundToInt(bottomSize - orig_size.y);
                if(new_size < 0) new_size = 0;
                bottom_node.size = new_size;
                //update node breaking forces
                var s = bottomSize/orig_size.y;
                bottom_node.breakingForce  = orig_nodes[1].breakingForce  * s;
                bottom_node.breakingTorque = orig_nodes[1].breakingTorque * s;
                //move the part
                if(!just_loaded)
                    part.UpdateAttachedPartPos(bottom_node);
            }
            //no need to update surface attached parts for the first time
            if(just_loaded) return;
            //update surface attached parts
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
        }

        public void UpdateMesh()
        {
            if(body_mesh == null || body_collider == null) return;
            update_body();
            update_attach_nodes();
            update_passage();
            var data = new BaseEventDetails(BaseEventDetails.Sender.AUTO);
            data.Set<string>("volName", "Tankage");
            data.Set<double>("newTotalVolume", body.current.V*UsableVolumeRatio);
            part.SendEvent("OnPartVolumeChanged", data, 0);
            old_size = size;
            old_aspect = aspect;
            StartCoroutine(CallbackUtil.DelayedCallback(1, UpdateDragCube));
            part.UpdatePartMenu(true);
            just_loaded = false;
        }
    }
}

