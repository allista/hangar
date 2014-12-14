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
			if(VesselsDocked > 0)
			{
				ScreenMessager.showMessage("The storage is already occupied");
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
				ScreenMessager.showMessage("A vessel can be fixed inside this storage only during construction.");
				return false;
			}
			return base.TryStoreVessel(v);
		}
	}
}