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
        [KSPField] public string SpawnSpace = string.Empty;
        [KSPField] public string SpawnTransform = string.Empty;
        [KSPField] public bool AutoPositionVessel;
        [KSPField] public Vector3 SpawnOffset = Vector3.up;
        public SpawnSpaceManager SpawnManager { get; protected set; }
        protected override SpawnSpaceManager spawn_space_manager => SpawnManager;

        protected override void early_setup(StartState state)
        {
            base.early_setup(state);
            SpawnManager = new SpawnSpaceManager();
            SpawnManager.Load(ModuleConfig);
            SpawnManager.Init(part);
            SpawnManager.SetupSensor();
        }

        protected override bool can_store_packed_vessel(PackedVessel vsl, bool in_flight)
        {
            if(!base.can_store_packed_vessel(vsl, in_flight))
                return false;
            Quaternion? rotation = AutoPositionVessel && !in_flight
                ? SpawnManager.GetOptimalRotation(vsl.size)
                : vsl.GetSpawnRotation();
            if(!SpawnManager.MetricFits(vsl.metric, rotation))
            {
                Utils.Message(5, "Insufficient vessel clearance in hangar entrance\n" +
                                 "\"{0}\" cannot be stored", vsl.name);
                return false;
            }
            return true;
        }

        protected override bool can_restore(PackedVessel v)
        {
            if(v == null || !base.can_restore(v)) return false;
            if(!SpawnManager.MetricFits(v.metric, v.GetSpawnRotation()))
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
        protected override List<HangarPassage> get_connected_passages() =>
        Storage?.ConnectedPassages();

        protected override void early_setup(StartState state)
        {
            base.early_setup(state);
            Storage = part.Modules.GetModule<HangarStorage>();
            if(Storage == null)
                this.ConfigurationInvalid("\"{0}\" part has no HangarStorage module", part.Title());
        }
    }


    public class HangarGateway : ExternalHangar
    {
        HangarPassage entrance;

        protected override List<HangarPassage> get_connected_passages() =>
        entrance?.ConnectedPassages();

        protected override void update_connected_storage()
        {
            base.update_connected_storage();
            if(ConnectedStorage.Count == 0)
                Storage = null;
            else if(Storage == null || !ConnectedStorage.Contains(Storage))
            {
                Storage = ConnectedStorage[0];
                Setup();
            }
            this.EnableModule(Storage != null);
        }

        protected override void update_connected_storage(Vessel vsl)
        {
            if(vsl == part.vessel && all_passages_ready)
            {
                update_connected_storage();
                if(!enabled && hangar_gates != null)
                    Close();
            }
        }

        protected override void early_setup(StartState state)
        {
            base.early_setup(state);
            entrance = part.GetPassage();
        }

        protected override bool try_store_packed_vessel(PackedVessel vsl, bool in_flight)
        {
            if(!entrance.CanTransferTo(vsl, Storage))
            {
                Utils.Message(8, "There's no room in the hangar for this vessel,\n" +
                              "OR vessel clearance is insufficient for safe docking.\n\n" +
                              "\"{0}\" cannot be stored", vsl.name);
                return false;
            }
            Storage.StoreVessel(vsl);
            return true;
        }
    }
}

