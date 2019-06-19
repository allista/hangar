//   Hangar.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri

using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
    public class Hangar : HangarMachinery
    {
        public override string GetInfo()
        {
            var info = base.GetInfo();
            var storage = part.Modules.GetModule<HangarStorage>();
            if(storage != null)
            {
                info += storage.AutoPositionVessel ?
                    "Free launch positioning\n" :
                    "Strict launch positioning\n";
            }
            return info;
        }

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

        protected override bool hangar_is_occupied() =>
        base.hangar_is_occupied() || !Storage.SpawnManager.SpawnSpaceEmpty;

        protected override Vector3 get_spawn_offset(PackedVessel pv)
        { return Storage.SpawnManager.GetSpawnOffset(pv.metric); }

        protected override Transform get_spawn_transform(PackedVessel pv)
        { return Storage.SpawnManager.GetSpawnTransform(pv.metric); }

        public override Transform GetSpawnTransform()
        { return Storage.SpawnManager.AutoPositionVessel ? null : Storage.SpawnManager.GetSpawnTransform(); }

#if DEBUG
        [KSPEvent(guiActive = true, guiName = "Check Airlock", active = true)]
        public void CheckAirlock()
        {
            if(part.airlock == null) return;
            RaycastHit raycastHit;
            if(Physics.Raycast(part.airlock.transform.position, (part.airlock.transform.position - part.transform.position).normalized, out raycastHit, 1, 32769))
            {
                this.Log("Airlock should be blocked:\n" +
                         "collider 'in front': {}\n" +
                         "distance to it: {}\n",
                         raycastHit.collider.name,
                         raycastHit.distance
                        );
            }
        }
#endif
    }
}

