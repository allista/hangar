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
	public class HangarResourceConverter : AnimatedConverterBase
	{
		[KSPField] public string WasteResource = "Waste";
		[KSPField] public string InputResources;
		[KSPField] public string OutputResources;

		List<ResourceLine> input;
		List<ResourceLine> output;
		ResourceLine waste;

		public override string GetInfo()
		{
			setup_resources();
			var info = base.GetInfo();
			if(input != null && input.Count > 0)
			{
				info += "Inputs:\n";
				info += input.Aggregate("", (s, r) => s+"- "+r.Info+'\n');
			}
			if(output != null && output.Count > 0)
			{
				info += "Outputs:\n";
				info += output.Aggregate("", (s, r) => s+"- "+r.Info+'\n');
			}
			if(waste != null && waste.Valid)
				info += string.Format("- {0}", waste.Info);
			return info;
		}

		void configuration_is_invalid(string msg)
		{
			ScreenMessager.showMessage(6, "WARNING: {0}.\n" +
				"Configuration of \"{1}\" is INVALID.", msg, this.Title());
			enabled = isEnabled = false;
			return;
		}

		static float mass_flow(ICollection<ResourceLine> resources)
		{
			if(resources == null || resources.Count == 0) return 0;
			return resources.Aggregate(0f, (f, r) => f+r.Rate);
		}

		void setup_resources()
		{
			//check energy consumption
			if(EnergyConsumption <= 0) EnergyConsumption = 0.01f;
			//parse input/output resources
			input  = ResourceLine.ParseResourcesToList(InputResources);
			if(input == null) 
			{ 
				configuration_is_invalid("unable to initialize INPUT resources"); 
				return; 
			}
			input.ForEach(r => r.InitializePump(part, RatesMultiplier));
			output = ResourceLine.ParseResourcesToList(OutputResources);
			if(output == null)
			{ 
				configuration_is_invalid("unable to initialize OUTPUT resources"); 
				return; 
			}
			output.ForEach(r => r.InitializePump(part, -RatesMultiplier));
			//mass flow conservation
			var net_mf = mass_flow(input) + mass_flow(output);
			if(net_mf < 0)
			{
				configuration_is_invalid("the mass flow of input resources is less then that of output resources");
				return;
			}
			if(net_mf > 0) //initialize waste resource
			{
				var waste_res = this.GetResourceDef(WasteResource);
				if(waste_res == null) return;
				if(waste_res.density == 0)
				{
					configuration_is_invalid("WasteResource should have non-zero density");
					return;
				}
				waste = new ResourceLine(part, waste_res, -net_mf);
			}
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

		static float transferred_mass(List<ResourceLine> resources)
		{ return resources.Aggregate(0f, (m, r) => m+r.Pump.Result*r.Resource.density); }

		static string try_transfer(List<ResourceLine> resources, bool skip_failed = false)
		{
			string failed = "";
			foreach(var r in resources)
			{
				if(r.TransferResource() && r.PartialTransfer) 
				{ 
					if(skip_failed) 
						failed += (failed == string.Empty? " " : "") 
							+ r.Resource.name;
					else return r.Resource.name; 
				}
			}
			return failed;
		}

		protected override bool can_convert(bool report = false)
		{ return true; } //do we need to check something here?

		protected override bool convert()
		{
			//consume energy
			socket.RequestTransfer(EnergyConsumption*TimeWarp.fixedDeltaTime);
			if(!socket.TransferResource()) return true;
			if(socket.PartialTransfer) 
			{
				ScreenMessager.showMessage("Not enough energy");
				socket.Clear(); 
				return false; 
			}
			//consume input resources
			var failed = try_transfer(input);
			//if not all inputs are present, dump consumed into waste and shut off
			if(failed != string.Empty)
			{
				if(waste.Valid)
				{
					waste.Pump.RequestTransfer(-transferred_mass(input)/waste.Density);
					if(waste.Pump.TransferResource() && waste.PartialTransfer)
						ScreenMessager.showMessage("No space left for {0}", waste.Resource.name);
				}
				input.ForEach(r => r.Pump.Clear());
				ScreenMessager.showMessage("{0} has been depleted", failed);
				return false;
			}
			//produce waste
			if(waste.Valid && waste.TransferResource() && waste.PartialTransfer)
			{
				ScreenMessager.showMessage("No space left for {0}", waste.Resource.name);
				return false;
			}
			failed = try_transfer(output, true);
			//if not all outputs are present, shut off
			if(failed != string.Empty)
			{
				output.ForEach(r => r.Pump.Clear());
				ScreenMessager.showMessage("No space self for {0}", failed);
				return false;
			}
			return true;
		}

		protected override void on_start_conversion() {}
		protected override void on_stop_conversion() {}
	}

	public class ResourceLine : ResourceWrapper<ResourceLine>
	{
		/// <summary>
		/// Gets the density in tons/unit.
		/// </summary>
		public float Density { get { return Resource.density; } }
		/// <summary>
		/// Gets conversion rate in tons/sec.
		/// </summary>
		public float Rate    { get; private set; }
		/// <summary>
		/// Gets conversion rate in units/sec.
		/// </summary>
		public float URate   { get; private set; } //u/sec

		public ResourcePump Pump { get; private set; }

		public ResourceLine() {}
		public ResourceLine(Part part, PartResourceDefinition res_def, float rate)
		{ 
			Resource = res_def; 
			Rate = rate;
			if(res_def != null) 
			{
				Pump = new ResourcePump(part, res_def.id);
				URate = rate/res_def.density;
			}
		}

		public void InitializePump(Part part, float rate_multiplier)
		{ 
			Pump   = new ResourcePump(part, Resource.id);
			Rate  *= rate_multiplier;
			URate *= rate_multiplier;
		}

		public override void LoadDefinition(string resource_definition)
		{
			var rate = load_definition(resource_definition);
			if(!Valid) return;
			Rate  = rate;
			URate = rate/Resource.density;
		}

		public bool TransferResource(float rate = 1f)
		{
			Pump.RequestTransfer(rate*URate*TimeWarp.fixedDeltaTime);
			return Pump.TransferResource();
		}

		public bool PartialTransfer { get { return Pump.PartialTransfer; } }

		public string Info
		{ get { return string.Format("{0}: {1}/sec", Resource.name, Utils.formatUnits(URate)); } }
	}
}
