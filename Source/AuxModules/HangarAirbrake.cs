using System;

namespace AtHangar
{
	public class HangarAirbrake : HangarAnimator
	{
		public override string GetInfo()
		{
			var info = base.GetInfo();
			if(info != string.Empty) info += "\n";
			info += string.Format("Deployed Drag: {0}", DragMultiplier*part.maximum_drag);
			return info;
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			if(EnergyConsumption <= 0f) 
				EnergyConsumption = 0.01f;
		}

		public override void OnStart (StartState state)
		{
			//default labels
			if(OpenEventGUIName  == string.Empty) OpenEventGUIName  = "Open Brake";
			if(CloseEventGUIName == string.Empty) CloseEventGUIName = "Close Brake";
			if(ActionGUIName     == string.Empty) ActionGUIName     = "Toggle Brake";
			Actions["ToggleAction"].actionGroup = KSPActionGroup.Brakes;
			base.OnStart (state);
		}
	}
}