//   LimitedHangarStorage.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using AT_Utils;

namespace AtHangar
{
    public class SimpleHangarStorage : HangarStorage
    {
        public override string GetInfo()
        {
            var info = base.GetInfo();
            info += "Can store only 1 vessel\n";
            return info;
        }

        public override bool TryStoreVessel(PackedVessel vsl,
                                            bool in_optimal_orientation,
                                            bool update_vessel_orientation)
        {
            if(VesselsCount > 0)
            {
                Utils.Message("The storage is already occupied");
                return false;
            }
            return base.TryStoreVessel(vsl, in_optimal_orientation, update_vessel_orientation);
        }
    }

    public class SingleUseHangarStorage : SimpleHangarStorage
    {
        public override string GetInfo()
        {
            var info = base.GetInfo();
            info += "Can store only in editor\n";
            return info;
        }

        public override bool TryStoreVessel(PackedVessel vsl,
                                            bool in_optimal_orientation,
                                            bool update_vessel_orientation)
        {
            if(!(vsl is PackedConstruct))
            {
                Utils.Message("A vessel can be fixed inside this storage only during construction.");
                return false;
            }
            return base.TryStoreVessel(vsl, in_optimal_orientation, update_vessel_orientation);
        }
    }
}