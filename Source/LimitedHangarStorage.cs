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

		public override bool TryStoreVessel(PackedVessel v)
		{
			if(TotalVesselsDocked > 0)
			{
				Utils.Message("The storage is already occupied");
				return false;
			}
			return base.TryStoreVessel(v);
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

		public override bool TryStoreVessel(PackedVessel v)
		{
			if(!(v is PackedConstruct))
			{
				Utils.Message("A vessel can be fixed inside this storage only during construction.");
				return false;
			}
			return base.TryStoreVessel(v);
		}
	}
}