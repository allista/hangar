namespace AtHangar
{
	public class HangarStorageDynamic : HangarStorage
	{
		[KSPField(isPersistant = true)] public float CurrentVolume;
		[KSPField] public float UpdateVolumeThreshold = 0.1f; //m^3

		public override string GetInfo() { return ""; }

		protected override void early_setup(StartState state)
		{
			base.early_setup(state);
			Fields["hangar_v"].guiActive = true;
			Fields["hangar_d"].guiActive = true;
		}

		protected override void update_metrics()
		{
			PartMetric = new Metric(part);
			HangarMetric = new Metric(CurrentVolume);
		}

		public void UpdateMetric()
		{
			HangarMetric = new Metric(CurrentVolume);
			hangar_v = Utils.formatVolume(HangarMetric.volume);
			hangar_d = Utils.formatDimensions(HangarMetric.size);
			_used_volume = Utils.formatPercent(UsedVolumeFrac);
		}

		public void AddVolume(float dV) 
		{
			if(dV < 0) return;
			CurrentVolume += dV;
			if(CurrentVolume - HangarMetric.volume > UpdateVolumeThreshold)
				UpdateMetric();
		}
	}
}

