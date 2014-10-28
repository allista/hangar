using System;

namespace AtHangar
{
	public class HangarLight : HangarAnimator
	{
		[KSPField(isPersistant = false)] public float ShutdownRateThreshold = 0.1f;
		[KSPField(isPersistant = false)] public float DownclockRateThreshold = 0.99f;

		public override void OnStart (StartState state)
		{
			base.OnStart (state);
			DragMultiplier = 1f;
		}

		public override void FixedUpdate()
		{
			if(HighLogic.LoadedSceneIsFlight)
			{
				if(EnergyConsumption > 0 && 
					State != AnimatorState.Closing && 
					State != AnimatorState.Closed)
				{
					float request  = progress*EnergyConsumption*TimeWarp.fixedDeltaTime;
					float consumed = part.RequestResource(Utils.ElectricChargeID, request);
					var rate = consumed/request;
					if(rate < DownclockRateThreshold)
					{
						if(rate < ShutdownRateThreshold) 
						{ Close(); update_events(); }
						else seek(progress*rate);
					}
				}
			}
		}
	}
}

