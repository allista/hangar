//   HangarStorage.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
    public class HangarStorage : HangarPassage, IPartCostModifier, IPartMassModifier
    {
        #region callbacks
        public delegate void PackedVesselHandler(PackedVessel pv);
        public delegate bool PackedVesselConstraint(PackedVessel pv);

        public PackedVesselConstraint FitConstraint = delegate { return true; };

        public PackedVesselHandler OnVesselStored = delegate { };
        public PackedVesselHandler OnVesselRemoved = delegate { };
        public PackedVesselHandler OnVesselUnfittedAdded = delegate { };
        public PackedVesselHandler OnVesselUnfittedRemoved = delegate { };
        public Action OnStorageEmpty = delegate { };
        #endregion

        #region Internals
        //hangar space
        [KSPField] public string SpawnSpace = string.Empty;
        [KSPField] public string SpawnTransform = string.Empty;
        [KSPField] public bool AutoPositionVessel;
        [KSPField] public bool SpawnSpaceSensor = true;
        [KSPField] public Vector3 SpawnOffset = Vector3.zero;
        public SpawnSpaceManager SpawnManager { get; protected set; }
        public bool HasSpaceMesh => SpawnManager.Space != null;
        public Metric PartMetric { get; protected set; }

        //vessels storage
        readonly static string SCIENCE_DATA = typeof(ScienceData).Name;
        readonly List<ConfigNode> stored_vessels_science = new List<ConfigNode>();
        readonly protected VesselsPack<PackedVessel> stored_vessels = new VesselsPack<PackedVessel>(Globals.Instance.EnableVesselPacking);
        readonly protected List<PackedConstruct> unfit_constructs = new List<PackedConstruct>();
        public Vector3 Size => SpawnManager.SpaceMetric.size;
        public float Volume => SpawnManager.SpaceMetric.volume;
        public int VesselsCount => stored_vessels.Count;
        public float VesselsMass => stored_vessels.VesselsMass;
        public float VesselsCost => stored_vessels.VesselsCost;
        public float UsedVolume => stored_vessels.UsedVolume;
        public float FreeVolume => Volume - UsedVolume;
        public float UsedVolumeFrac => UsedVolume / Volume;
        //coordination
        readonly List<HangarStorage> storage_checklist = new List<HangarStorage>();
        #endregion

        #region GUI
        [KSPField(guiName = "Volume", guiActiveEditor = true)] public string hangar_v;
        [KSPField(guiName = "Size", guiActiveEditor = true)] public string hangar_d;
        //GUI Active
        [KSPField(guiName = "Vessels", guiActive = true, guiActiveEditor = true)] public string _stored_vessels;
        [KSPField(guiName = "Stored Mass", guiActive = true, guiActiveEditor = true)] public string _stored_mass;
        [KSPField(guiName = "Stored Cost", guiActive = true, guiActiveEditor = true)] public string _stored_cost;
        [KSPField(guiName = "Used Volume", guiActive = true, guiActiveEditor = true)] public string _used_volume;
        #endregion

        #region Setup
        public override string GetInfo()
        {
            SpawnManager = new SpawnSpaceManager();
            SpawnManager.Load(ModuleConfig);
            SpawnManager.Init(part);
            update_metrics();
            var info = base.GetInfo();
            if(SpawnManager.SpaceMetric.volume > 0)
            {
                info += string.Format("Available Volume: {0}\n", Utils.formatVolume(SpawnManager.SpaceMetric.volume));
                info += string.Format("Dimensions: {0}\n", Utils.formatDimensions(SpawnManager.SpaceMetric.size));
            }
            return info;
        }

        void build_storage_checklist()
        {
            if(!HighLogic.LoadedSceneIsFlight || vessel == null) return;
            storage_checklist.Clear();
            foreach(Part p in vessel.parts)
            {
                if(p == part)
                {
                    foreach(var s in p.Modules.OfType<HangarStorage>())
                    {
                        if(s == this) return;
                        storage_checklist.Add(s);
                    }
                    return;
                }
                storage_checklist.AddRange(p.Modules.OfType<HangarStorage>());
            }
        }

        bool other_storages_ready => storage_checklist.All(s => s.Ready);

        protected override void early_setup(StartState state)
        {
            base.early_setup(state);
            SpawnManager = new SpawnSpaceManager();
            SpawnManager.Load(ModuleConfig);
            SpawnManager.Init(part);
            if(SpawnSpaceSensor)
                SpawnManager.SetupSensor();
            build_storage_checklist();
        }


        bool try_pack_construct(PackedVessel pv) =>
        VesselFits(pv, AutoPositionVessel) && stored_vessels.TryAdd(pv);

        void try_repack_construct(PackedVessel pv)
        {
            if(!try_pack_construct(pv))
            {
                OnVesselRemoved(pv);
                if(pv is PackedConstruct pc)
                    AddUnfit(pc);
            }

        }

        void try_pack_unfit_construct(PackedConstruct pc)
        {
            if(try_pack_construct(pc))
            {
                RemoveUnfit(pc);
                OnVesselStored(pc);
            }
        }

        protected virtual void update_metrics()
        {
            PartMetric = new Metric(part);
            SpawnManager.UpdateMetric();
        }

        public override void Setup(bool reset = false)
        {
            base.Setup(reset);
            //initialize part and hangar metric
            update_metrics();
            //setup vessels packs
            stored_vessels.space = SpawnManager.SpaceMetric;
            //display recalculated values
            hangar_v = Utils.formatVolume(SpawnManager.SpaceMetric.volume);
            hangar_d = Utils.formatDimensions(SpawnManager.SpaceMetric.size);
            if(reset)
            {   //if resetting, try to repack vessels
                var stored = stored_vessels.Values;
                stored_vessels.Clear();
                stored.ForEach(try_repack_construct);
                if(stored.Count > stored_vessels.Count)
                {
                    var dN = stored.Count - stored_vessels.Count;
                    Utils.Message("The storage became too small. {0} vessel(s) {1} removed",
                        dN, dN > 1 ? "were" : "was");
                }
                else if(unfit_constructs.Count > 0)
                {
                    unfit_constructs.ToList().ForEach(try_pack_unfit_construct);
                }
            }
            //then set other part parameters
            set_part_params(reset);
        }

        virtual protected void on_set_part_params()
        {
            var el = EditorLogic.fetch;
            if(el != null) GameEvents.onEditorShipModified.Fire(el.ship);
            //            else if(part.vessel != null) GameEvents.onVesselWasModified.Fire(part.vessel);
        }

        virtual protected void set_part_params(bool reset = false)
        {
            _stored_vessels = VesselsCount.ToString();
            _stored_mass = Utils.formatMass(VesselsMass);
            _stored_cost = VesselsCost.ToString();
            _used_volume = Volume > 0 ? UsedVolumeFrac.ToString("P1") : "N/A";
            on_set_part_params();
            part.UpdatePartMenu();
        }

        public override void OnAwake()
        {
            GameEvents.OnVesselRecoveryRequested.Add(onVesselRecoveryRequested);
        }

        public void OnDestroy()
        {
            GameEvents.OnVesselRecoveryRequested.Remove(onVesselRecoveryRequested);
        }
        #endregion

        #region Content Management
        public List<PackedVessel> GetVessels() => stored_vessels.Values;

        public int UnfitCount => unfit_constructs.Count;
        public List<PackedConstruct> UnfitConstucts => unfit_constructs.ToList();

        public void AddUnfit(PackedConstruct pc)
        {
            unfit_constructs.Add(pc);
            OnVesselUnfittedAdded(pc);
        }

        public bool RemoveUnfit(PackedConstruct pc)
        {
            if(unfit_constructs.Remove(pc))
            {
                OnVesselUnfittedRemoved(pc);
                return true;
            }
            return false;
        }

        public void UpdateParams()
        {
            stored_vessels.UpdateParams();
            set_part_params();
        }

        public void ClearVessels()
        {
            unfit_constructs.Clear();
            stored_vessels.Clear();
            OnStorageEmpty();
            set_part_params();
        }
        #endregion

        #region Logistics
        public override bool CanHold(PackedVessel vsl) =>
        VesselFits(vsl, true) && stored_vessels.CanAdd(vsl);

        public bool TryTransferTo(PackedVessel vsl, HangarStorage other)
        {
            if(!CanTransferTo(vsl, other))
            {
                Utils.Message("Unable to move \"{0}\" from \"{1}\" to \"{2}\"",
                    vsl.name, this.Title(), other.Title());
                return false;
            }
            if(!RemoveVessel(vsl))
            {
                this.Log("TryTransferTo: trying to remove a PackedVessel that is not present.");
                return false;
            }
            vsl.SpawnRotation = other.SpawnManager.GetOptimalRotation(vsl.size).eulerAngles;
            other.StoreVessel(vsl);
            return true;
        }
        #endregion

        #region Storage
        public bool VesselFits(PackedVessel pv, bool in_optimal_orientation)
        {
            if(pv != null)
            {
                Quaternion? rotation = in_optimal_orientation
                    ? SpawnManager.GetOptimalRotation(pv.size)
                    : pv.GetSpawnRotation();
                if(SpawnManager.MetricFits(pv.metric, rotation))
                {
                    var constraints = FitConstraint.GetInvocationList();
                    for(int i = 0, len = constraints.Length; i < len; i++)
                    {
                        if(constraints[i] is PackedVesselConstraint cons && !cons(pv))
                            return false;
                    }
                    return true;
                }
            }
            return false;
        }

        public void StoreVessel(PackedVessel vsl)
        {
            stored_vessels.ForceAdd(vsl);
            OnVesselStored(vsl);
            set_part_params();
        }

        public bool RemoveVessel(PackedVessel vsl)
        {
            if(stored_vessels.Remove(vsl))
            {
                OnVesselRemoved(vsl);
                if(stored_vessels.Count == 0)
                    OnStorageEmpty();
                set_part_params();
                return true;
            }
            return false;
        }

        public virtual bool TryStoreVessel(PackedVessel vsl,
                                           bool in_optimal_orientation,
                                           bool update_vessel_orientation)
        {
            bool stored = false;
            if(VesselFits(vsl, in_optimal_orientation))
            {
                if(stored_vessels.TryAdd(vsl))
                {
                    if(in_optimal_orientation && update_vessel_orientation)
                        vsl.SpawnRotation = SpawnManager.GetOptimalRotation(vsl.size).eulerAngles;
                    OnVesselStored(vsl);
                    set_part_params();
                    stored = true;
                }
                else
                    Utils.Message("There's no room for \"{0}\"", vsl.name);
            }
            else
                Utils.Message(5, "Insufficient vessel clearance for safe docking\n" +
                                 "\"{0}\" cannot be stored", vsl.name);
            if(!stored && HighLogic.LoadedSceneIsEditor && vsl is PackedConstruct pc)
            {
                AddUnfit(pc);
                return true;
            }
            return stored;
        }

        public bool TryStoreVesselInEditor(PackedVessel vsl) =>
        TryStoreVessel(vsl, AutoPositionVessel, AutoPositionVessel);

        public bool Contains(PackedVessel item) => stored_vessels.Contains(item);

        void onVesselRecoveryRequested(Vessel v)
        {
            if(v != vessel) return;
            stored_vessels_science.Clear();
            foreach(var vsl in stored_vessels.Values)
            {
                if(vsl is StoredVessel sv)
                {
                    foreach(var p in sv.proto_vessel.protoPartSnapshots)
                        foreach(var pm in p.modules)
                        {
                            var s = pm.moduleValues.GetNode(SCIENCE_DATA);
                            if(s != null) stored_vessels_science.Add(s);
                        }
                }
            }
        }
        #endregion

        #region Save-Load
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            //save stored vessels
            if(stored_vessels.Count > 0)
            {
                var sv_node = node.AddNode("STORED_VESSELS");
                var pc_node = node.AddNode("PACKED_CONSTRUCTS");
                foreach(var vsl in stored_vessels)
                {
                    if(vsl is StoredVessel sv)
                        sv.Save(sv_node.AddNode("STORED_VESSEL"));
                    else if(vsl is PackedConstruct pc)
                        pc.Save(pc_node.AddNode("STORED_VESSEL"));
                }
            }
            //save science data of stored ships
            if(stored_vessels_science.Count > 0)
                stored_vessels_science.ForEach(n => node.AddNode(n));
        }

        void load_vessels<T>(ConfigNode node) where T : PackedVessel, new()
        {
            if(node == null) return;
            foreach(ConfigNode vsl_node in node.nodes)
            {
                var vsl = new T();
                vsl.Load(vsl_node);
                stored_vessels.ForceAdd(vsl);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            //restore stored vessels
            load_vessels<StoredVessel>(node.GetNode("STORED_VESSELS"));
            load_vessels<PackedConstruct>(node.GetNode("PACKED_CONSTRUCTS"));
            //restore science data
            stored_vessels_science.Clear();
            foreach(var n in node.GetNodes(SCIENCE_DATA))
                stored_vessels_science.Add(n);

        }
        #endregion

        #region ControllableModule
        public override bool CanDisable()
        {
            if(stored_vessels.Count > 0)
            {
                Utils.Message("Cannot disable storage: there are vessels inside");
                return false;
            }
            return true;
        }

        public override void Enable(bool enable)
        {
            if(enable) Setup();
            base.Enable(enable);
        }
        #endregion

        #region IPart*Modifiers
        public virtual float GetModuleCost(float defaultCost, ModifierStagingSituation situation) { return VesselsCost; }
        public virtual ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        public virtual float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return VesselsMass; }
        public virtual ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        #endregion
    }
}

