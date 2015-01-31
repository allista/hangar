//   HangarResourceUser.cs
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
using System.Linq;
using System.Collections.Generic;

namespace AtHangar
{
	public abstract class HangarResourceConsumer : AnimatedConverterBase
	{
		[KSPField] public string InputResources = string.Empty;
		protected List<ResourceLine> input;

		public override string GetInfo()
		{
			setup_resources();
			var info = base.GetInfo();
			if(input != null && input.Count > 0)
			{
				info += "Inputs:\n";
				info += input.Aggregate("", (s, r) => s+"- "+r.Info+'\n');
			}
			return info;
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			//check energy consumption; even generators consume energy
			if(EnergyConsumption <= 0) 
				EnergyConsumption = 0.01f;
		}

		protected virtual void setup_resources()
		{
			//parse input/output resources
			input  = ResourceLine.ParseResourcesToList(InputResources);
			if(input == null) 
			{ 
				this.ConfigurationInvalid("unable to initialize INPUT resources"); 
				return; 
			}
			input.ForEach(r => r.InitializePump(part, RatesMultiplier));
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			setup_resources();
		}

		public override void SetRatesMultiplier(float mult)
		{
			base.SetRatesMultiplier(mult);
			setup_resources();
		}

		protected static string try_transfer(List<ResourceLine> resources, float rate = 1f, bool skip_failed = false)
		{
			string failed = "";
			foreach(var r in resources)
			{
				if(r.TransferResource(rate) && r.PartialTransfer) 
				{ 
					if(skip_failed) 
						failed += (failed == ""? "" : ", ") + HangarGUI.ParseCamelCase(r.Name);
					else return HangarGUI.ParseCamelCase(r.Name); 
				}
			}
			return failed;
		}
	}
}

