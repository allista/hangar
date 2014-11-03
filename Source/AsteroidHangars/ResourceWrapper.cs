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
			var my_name = this.GetType().Name;
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
			foreach(var res_str in resources.Split(new []{';'}, 
					StringSplitOptions.RemoveEmptyEntries))
			{
				var res = new Res();
				res.LoadDefinition(res_str.Trim());
				if(!res.Valid) return null;
				add_to_collection(res_col, res);
			}
			return res_col;
		}

		static public List<Res> ParseResourcesToList(string resources)
		{ return parse_resources<List<Res>>(resources, (c, r) => c.Add(r)); }

		static public Dictionary<string, Res> ParseResourcesToDict(string resources)
		{ return parse_resources<Dictionary<string, Res>>(resources, (c, r) => c.Add(r.Name, r)); }
	}
}

