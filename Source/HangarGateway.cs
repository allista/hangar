//   HangarGateway.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
    public abstract class ExternalHangar : HangarMachinery
    {
        [KSPField] public string  SpawnSpace = string.Empty;
        [KSPField] public string  SpawnTransform = string.Empty;
        [KSPField] public bool    AutoPositionVessel;
        [KSPField] public Vector3 SpawnOffset = Vector3.up;
        public SpawnSpaceManager SpawnManager { get; protected set; }

        public override string GetInfo()
        {
            var info = base.GetInfo();
            info += AutoPositionVessel?
                "Free launch positioning\n" :
                "Strict launch positioning\n";
            return info;
        }

        protected override void early_setup(StartState state)
        {
            base.early_setup(state);
            SpawnManager = new SpawnSpaceManager();
            SpawnManager.Load(ModuleConfig);
            SpawnManager.Init(part);
            SpawnManager.SetupSensor();
            if(Storage != null) 
                Storage.FitConstraint = pv => SpawnManager.MetricFits(pv.metric);
        }

        protected override bool hangar_is_occupied() =>
        base.hangar_is_occupied() || !SpawnManager.SpawnSpaceEmpty;

        protected override Vector3 get_spawn_offset(PackedVessel pv)
        { return SpawnManager.GetSpawnOffset(pv.metric); }

        protected override Transform get_spawn_transform(PackedVessel pv)
        { return SpawnManager.GetSpawnTransform(pv.metric); }

        public override Transform GetSpawnTransform()
        { return SpawnManager.AutoPositionVessel? null : SpawnManager.GetSpawnTransform(); }

        protected override bool can_restore(PackedVessel v)
        { 
            if(v == null || !base.can_restore(v)) return false;
            if(!SpawnManager.MetricFits(v.metric))
            {
                Utils.Message(6, "Vessel clearance is insufficient for safe launch.\n\n" +
                              "\"{0}\" cannot be launched", v.name);
                return false;
            }
            return true;
        }
    }


    public class HangarEntrance : ExternalHangar
    {
        protected override List<HangarPassage> get_connected_passages()
        { return Storage == null ? null : Storage.ConnectedPassages(); }

        protected override void early_setup(StartState state)
        {
            base.early_setup(state);
            Storage = part.Modules.GetModule<HangarStorage>();
            if(Storage == null) 
            {
                this.ConfigurationInvalid("\"{0}\" part has no HangarStorage module", part.Title());
                return;
            }
        }
    }


    public class HangarGateway : ExternalHangar
    {
        HangarPassage entrance;

        protected override List<HangarPassage> get_connected_passages()
        { return entrance == null ? null : entrance.ConnectedPassages(); }

        protected override void update_connected_storage()
        {
            base.update_connected_storage();
            if(ConnectedStorage.Count == 0) Storage = null;
            else if(Storage == null || !ConnectedStorage.Contains(Storage))
            { Storage = ConnectedStorage[0]; Setup(); }
            this.EnableModule(Storage != null);
        }

        protected override void update_connected_storage(Vessel vsl)
        {
            if(vsl != part.vessel || !all_passages_ready) return;
            update_connected_storage();
            if(!enabled && hangar_gates != null) Close();
        }

        protected override void early_setup(StartState state)
        {
            base.early_setup(state);
            entrance = part.GetPassage();
        }

        protected override bool try_store_vessel(PackedVessel v)
        {
            if(!entrance.CanTransferTo(v, Storage))
            {
                Utils.Message(8, "There's no room in the hangar for this vessel,\n" +
                              "OR vessel clearance is insufficient for safe docking.\n\n" +
                              "\"{0}\" cannot be stored", v.name);
                return false;
            }
            Storage.StoreVessel(v);
            return true;
        }
    }
}

