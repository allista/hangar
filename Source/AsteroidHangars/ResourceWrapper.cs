//   ResourceWrapper.cs
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
using System.Collections.Generic;

namespace AtHangar
{
	public abstract class ResourceWrapper<Res> where Res : ResourceWrapper<Res>, new()
	{
		public PartResourceDefinition Resource { get; protected set; }
		public string Name { get { return Resource.name; } }
		public virtual bool Valid { get { return Resource != null; } }

		public abstract void LoadDefinition(string resource_definition);

		protected float load_definition(string resource_definition)
		{
			var name_and_value = resource_definition.Split(new []{' '}, 
				StringSplitOptions.RemoveEmptyEntries);
			var my_name = GetType().Name;
			if(name_and_value.Length != 2) 
			{
				Utils.Log("{0}: Invalid format of tank resource definition. " +
					"Should be 'ResourceName value', got {1}", my_name, resource_definition);
				return -1;
			}
			float val;
			if(!float.TryParse(name_and_value[1], out val) || val <= 0)
			{
				Utils.Log("{0}: Invalid format of value. " +
					"Should be positive float value, got: {1}", my_name, name_and_value[1]);
				return -1;
			}
			var res_def = PartResourceLibrary.Instance.GetDefinition(name_and_value[0]);
			if(res_def == null) 
			{
				Utils.Log("{0}: Resource does not exist: {1}", my_name, name_and_value[0]);
				return -1;
			}
			Resource = res_def;
			return val;
		}

		static Col parse_resources<Col>(string resources, Action<Col, Res> add_to_collection) 
			where Col : class, new()
		{
			var res_col = new Col();
			if(string.IsNullOrEmpty(resources)) return res_col;
			//remove comments
			var comment = resources.IndexOf("//");
			if(comment >= 0) resources = resources.Remove(comment);
			if(resources == string.Empty) return res_col;
			//parse resource definitions
			foreach(var res_str in resources.Split(new []{';'}, 
					StringSplitOptions.RemoveEmptyEntries))
			{
				var res = new Res();
				res.LoadDefinition(res_str.Trim());
				if(!res.Valid) continue;
				add_to_collection(res_col, res);
			}
			return res_col;
		}

		static public List<Res> ParseResourcesToList(string resources)
		{ return parse_resources<List<Res>>(resources, (c, r) => c.Add(r)); }

		static public SortedList<string, Res> ParseResourcesToSortedList(string resources)
		{ return parse_resources<SortedList<string, Res>>(resources, (c, r) => c.Add(r.Name, r)); }
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
		float base_rate;

		/// <summary>
		/// Gets conversion rate in units/sec.
		/// </summary>
		public float URate   { get; private set; } //u/sec

		public ResourcePump Pump { get; private set; }

		public ResourceLine() {}
		public ResourceLine(Part part, PartResourceDefinition res_def, float rate)
		{ 
			Resource = res_def; 
			Rate = base_rate = rate;
			if(res_def != null) 
			{
				Pump = new ResourcePump(part, res_def.id);
				URate = rate/res_def.density;
			}
		}

		public void InitializePump(Part part, float rate_multiplier)
		{ 
			Pump  = new ResourcePump(part, Resource.id);
			Rate  = base_rate * rate_multiplier;
			URate = Rate/Resource.density;
		}

		public override void LoadDefinition(string resource_definition)
		{
			var rate = load_definition(resource_definition);
			if(!Valid) return;
			Rate  = base_rate = rate;
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

