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
		[KSPField] public string OuptuResources;

		List<ResourceLine> input;
		List<ResourceLine> output;
		ResourceLine waste;

		public override string GetInfo()
		{
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
			if(waste.Valid)
				info += string.Format("Byproduct: {0}", waste.Info);
			return info;
		}

		string configuration_is_invalid 
		{ get { return string.Format("Configuration of \"{0}\" is INVALID.", this.Title()); } }

		ResourceLine parse_resource(string res, bool _in = true)
		{
			var name_and_rate = res.Split(new []{' '}, 
				StringSplitOptions.RemoveEmptyEntries);
			if(name_and_rate.Length != 2) 
			{
				this.Log("Invalid format of resource usage definition. " +
						 "Should be 'ResourceName conversion_rate', got {0}", res);
				return default(ResourceLine);
			}
			float rate;
			if(!float.TryParse(name_and_rate[1], out rate) || rate <= 0)
			{
				this.Log("Invalid format of resource usage rate. " +
					"Should be positive float value, got: {0}", name_and_rate[1]);
				return default(ResourceLine);
			}
			rate *= RatesMultiplier;
			var res_def = this.GetResourceDef(name_and_rate[0]);
			if(res_def == null) 
			{
				this.Log("Resource does not exist: {0}", name_and_rate[0]);
				return default(ResourceLine);
			}
			return new ResourceLine(part, res_def, _in? rate : -rate);
		}

		List<ResourceLine> parse_resources(string resources, bool _in = true)
		{
			var res_list = new List<ResourceLine>();
			foreach(var _res_str in resources.Split(';'))
			{
				var res_str = _res_str.Trim();
				if(res_str == string.Empty) continue;
				var res = parse_resource(res_str, _in);
				if(!res.Valid) return null;
				res_list.Add(res);
			}
			return res_list;
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
			input  = parse_resources(InputResources);
			if(input == null) return;
			output = parse_resources(OuptuResources, false);
			if(output == null) return;
			//mass flow conservation
			var in_mf  = mass_flow(input);
			var out_mf = mass_flow(output);
			if(in_mf < out_mf)
			{
				ScreenMessager.showMessage(6, "WARNING: the mass flow of input resources is less then that of output resources.\n" +
					configuration_is_invalid);
				enabled = isEnabled = false;
				return;
			}
			if(in_mf > out_mf) //initialize waste resource
			{
				var waste_res = this.GetResourceDef(WasteResource);
				if(waste_res == null) return;
				if(waste_res.density == 0)
				{
					ScreenMessager.showMessage(6, "WARNING: WasteResource should have non-zero density.\n" +
						configuration_is_invalid);
					enabled = isEnabled = false;
					return;
				}
				var waste_rate = (out_mf-in_mf)/waste_res.density;
				waste = new ResourceLine(part, waste_res, waste_rate);
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			setup_resources();
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
					waste.Pump.RequestTransfer(transferred_mass(input)/waste.Density);
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


	public struct ResourceLine
	{
		public PartResourceDefinition Resource { get; private set; }
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
		public bool Valid { get { return Resource != null; } }

		public ResourceLine(Part part, PartResourceDefinition res_def, float rate) : this()
		{ 
			Resource = res_def; 
			Rate = rate;
			if(res_def != null) 
			{
				Pump = new ResourcePump(part, res_def.id);
				URate = rate*res_def.density;
			}
		}

		public bool TransferResource(float rate = 1f)
		{
			Pump.RequestTransfer(rate*URate*TimeWarp.fixedDeltaTime);
			return Pump.TransferResource();
		}

		public bool PartialTransfer { get { return Pump.PartialTransfer; } }

		public string Info
		{ get { return string.Format("{0}: {1}/sec", Resource.name, URate); } }
	}
}

