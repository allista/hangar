using System;

namespace AtHangar
{
	public class HangarLight : HangarAnimator
	{
		public override string GetInfo()
		{
			var info = base.GetInfo();
			if(info != string.Empty) info += "\n";
			info += string.Format("Energy Consumption: {0}/sec", EnergyConsumption);
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
			if(OpenEventGUIName  == string.Empty) OpenEventGUIName  = "Lights On";
			if(CloseEventGUIName == string.Empty) CloseEventGUIName = "Lights Off";
			if(ActionGUIName     == string.Empty) ActionGUIName     = "Toggle Lights";
			Actions["ToggleAction"].actionGroup = KSPActionGroup.Light;
			base.OnStart (state);
		}

		protected override void consume_energy()
		{
			if(State != AnimatorState.Opened && State != AnimatorState.Opening) return;
			socket.RequestTransfer(EnergyConsumption*TimeWarp.fixedDeltaTime);
			if(!socket.TransferResource()) return;
			if(socket.PartialTransfer) { Close(); update_events(); socket.Clear(); }
		}

		public override void FixedUpdate()
		{ if(HighLogic.LoadedSceneIsFlight) consume_energy(); }
	}
}

