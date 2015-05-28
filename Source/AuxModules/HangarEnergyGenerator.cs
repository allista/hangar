//   HangarEnergyGenerator.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

using System;
using UnityEngine;

namespace AtHangar
{
	public class HangarEnergyGenerator : HangarResourceConsumer
	{
		[KSPField] public float EnergyProduction = 100f;

		[KSPField(guiActiveEditor = true, guiName = "Energy Production", guiUnits = "/s", guiFormat = "F2")] 
		public float CurrentEnergyProduction;

		public override string GetInfo()
		{
			setup_resources();
			var info = base.GetInfo();
			update_energy_production();
			info += string.Format("Energy Production: {0:F2}/sec\n", CurrentEnergyProduction);
			return info;
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			//check energy production
			if(EnergyProduction <= 0) 
				EnergyProduction = 0.01f;
		}

		public override void OnStart(StartState state)
		{
			Fields["EnergyProduction"].guiName = Title+" Produces";
			update_energy_production();
			base.OnStart(state);
		}

		public override void SetRatesMultiplier(float mult)
		{
			base.SetRatesMultiplier(mult);
			update_energy_production();
		}

		void update_energy_production()
		{ CurrentEnergyProduction = EnergyProduction*RatesMultiplier; }

		protected override bool can_convert(bool report = false)
		{ return true; } //do we need to check something here?

		protected override bool convert()
		{
			if(!consume_energy()) return true;
			//consume input resources
			var failed = try_transfer(input, Rate, true);
			if(failed != string.Empty)
			{
				input.ForEach(r => r.Pump.Clear());
				ScreenMessager.showMessage("Not enough {0}", failed);
				next_rate = 0;
			} else next_rate = 1;
			//produce energy only if current Rate is above threshold
			if(Rate >= MinimumRate)
			{
				socket.RequestTransfer(-Rate*CurrentEnergyProduction*TimeWarp.fixedDeltaTime);
				if(socket.TransferResource() && failed == string.Empty)
					next_rate = Mathf.Max(socket.Ratio, MinimumRate);
			}
//			this.Log("Rate {0}, Next rate {1}, socket.Request {2}, socket.Result {3}, socket.Ratio {4}",
//			         Rate, next_rate, socket.Requested, socket.Result, socket.Ratio);//debug
			//check rate threshold
			return above_threshold;
		}

		protected override void on_start_conversion() {}
		protected override void on_stop_conversion() {}
	}
}

