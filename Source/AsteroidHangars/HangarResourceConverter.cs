//   HangarResourceConverter.cs
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
	/// <summary>
	/// Mass-conserving resource converter.
	/// Always requires electricity.
	/// Conserves total mass by producing WasteResource if nesessary.
	/// </summary>
	public class HangarResourceConverter : HangarResourceConsumer
	{
		[KSPField] public string WasteResource = "Waste";
		[KSPField] public string OutputResources;

		List<ResourceLine> output;
		ResourceLine waste;

		public override string GetInfo()
		{
			setup_resources();
			var info = base.GetInfo();
			if(output != null && output.Count > 0)
			{
				info += "Outputs:\n";
				info += output.Aggregate("", (s, r) => s+"- "+r.Info+'\n');
			}
			if(waste != null && waste.Valid)
				info += string.Format("- {0}", waste.Info);
			return info;
		}

		static float mass_flow(ICollection<ResourceLine> resources)
		{
			if(resources == null || resources.Count == 0) return 0;
			return resources.Aggregate(0f, (f, r) => f+r.Rate);
		}

		protected override void setup_resources()
		{
			base.setup_resources();
			output = ResourceLine.ParseResourcesToList(OutputResources);
			if(output == null)
			{ 
				this.ConfigurationInvalid("unable to initialize OUTPUT resources"); 
				return; 
			}
			output.ForEach(r => r.InitializePump(part, -RatesMultiplier));
			//mass flow conservation
			var net_mf = mass_flow(input) + mass_flow(output);
			if(net_mf < 0)
			{
				this.ConfigurationInvalid("the mass flow of input resources is less then that of output resources");
				return;
			}
			if(net_mf > 0) //initialize waste resource
			{
				var waste_res = this.GetResourceDef(WasteResource);
				if(waste_res == null) return;
				if(waste_res.density == 0)
				{
					this.ConfigurationInvalid("WasteResource should have non-zero density");
					return;
				}
				waste = new ResourceLine(part, waste_res, -net_mf);
			}
		}

		static float transferred_mass(List<ResourceLine> resources)
		{ return resources.Aggregate(0f, (m, r) => m+r.Pump.Result*r.Density); }

		protected override bool can_convert(bool report = false)
		{ return true; } //do we need to check something here?

		protected override bool convert()
		{
			//consume energy, udpate conversion rate
			if(!consume_energy()) return true;
			if(ShuttingOff || Rate < MinimumRate) goto end;
			//consume input resources
			var failed = try_transfer(input, Rate);
			//if not all inputs are present, dump consumed into waste and shut off
			if(failed != string.Empty)
			{
				if(waste != null && waste.Valid)
				{
					waste.Pump.RequestTransfer(-transferred_mass(input)/waste.Density);
					if(waste.Pump.TransferResource() && waste.PartialTransfer)
						ScreenMessager.showMessage("No space left for {0}", waste.Name);
				}
				input.ForEach(r => r.Pump.Clear());
				ScreenMessager.showMessage("Not enough {0}", failed);
				StopConversion();
				goto end;
			}
			//produce waste
			if(waste != null && waste.Valid && waste.TransferResource(Rate) && waste.PartialTransfer)
			{
				ScreenMessager.showMessage("No space left for {0}", waste.Name);
				StopConversion();
				goto end;
			}
			failed = try_transfer(output, Rate, true);
			//if not all outputs are present, shut off
			if(failed != string.Empty)
			{
				output.ForEach(r => r.Pump.Clear());
				ScreenMessager.showMessage("No space self for {0}", failed);
				StopConversion();
			}
			end:
			return above_threshold;
		}

		protected override void on_start_conversion() {}
		protected override void on_stop_conversion() {}
	}
}
