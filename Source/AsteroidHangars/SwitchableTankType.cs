using System;
using System.Collections.Generic;

namespace AtHangar
{
	/// <summary>
	/// Type of a switchable tank. 
	/// Defines resources that a tank of this type can hold, 
	/// how much units of each resource a liter of volume contains,
	/// and portion of part's volume that can be used by the tank.
	/// </summary>
	public class SwitchableTankType : ConfigNodeObject
	{
		/// <summary>
		/// The library of preconfigured tank types 
		/// that is loaded on game start by HangarConfigLoader.
		/// </summary>
		public static Dictionary<string, SwitchableTankType> TankTypes 
		{ 
			get
			{
				if(_tank_types == null)
				{
					_tank_types = new Dictionary<string, SwitchableTankType>();
					foreach(ConfigNode n in GameDatabase.Instance.GetConfigNodes(SwitchableTankType.NODE_NAME))
					{
						var tank_type = new SwitchableTankType();
						tank_type.Load(n);
						if(!tank_type.Valid)
						{
							ScreenMessager.showMessage(6, "Hangar: configuration of \"{0}\" tank type is INVALID.", tank_type.Name);
							continue;
						}
						try { _tank_types.Add(tank_type.Name, tank_type); }
						catch { Utils.Log("SwitchableTankType: ignoring duplicate configuration of {0} tank type", tank_type.Name); }
					}
				}
				return _tank_types;
			}
		}
		static Dictionary<string, SwitchableTankType> _tank_types;

		new public const string NODE_NAME = "TANKTYPE";
		/// <summary>
		/// The name of the tank type.
		/// </summary>
		[Persistent] public string Name;
		/// <summary>
		/// The string list of resources a tank of this type can hold. Format:
		/// ResourceName1 units_per_liter; ResourceName2 units_per_liter2; ...
		/// </summary>
		[Persistent] public string PossibleResources;
		/// <summary>
		/// The portion of a part's volume the tank can use.
		/// </summary>
		[Persistent] public float  UsefulVolumeRatio = 0.8f;

		public Dictionary<string,TankResource> Resources { get; private set; }
		public bool Valid { get { return Resources != null && Resources.Count > 0; } }
		public List<string> SortedNames { get; private set; }
		public TankResource DefaultResource { get; private set; }

		public TankResource this[string name]
		{
			get
			{
				TankResource res = default(TankResource);
				if(Valid) Resources.TryGetValue(name, out res);
				return res;
			}
		}

		public override void Load(ConfigNode node)
		{
			base.Load(node);
			Resources = TankResource.ParseResourceList(PossibleResources);
			if(Valid) 
			{
				var names = new List<string>(Resources.Keys); names.Sort();
				SortedNames = names;
				DefaultResource = Resources[SortedNames[0]];
			}
		}

		public string Info
		{ 
			get 
			{ 
				var info = "";
				if(!Valid) return info;
				info += "Tank can hold:\n";
				foreach(var r in SortedNames)
					info += string.Format("- {0}: {1}u/L\n", Resources[r].Name, Resources[r].UnitsPerLiter);
				return info;
			} 
		}
	}


	/// <summary>
	/// A Part Resource Definition complemented with Units Per Liter ratio.
	/// </summary>
	public struct TankResource
	{
		public PartResourceDefinition Resource { get; private set; }
		public float UnitsPerLiter { get; private set; }
		public string Name { get { return Resource.name; } }
		public bool Valid { get { return Resource != null; } }

		public TankResource(PartResourceDefinition resource, float units_per_liter = 1f) : this()
		{
			Resource = resource;
			UnitsPerLiter = units_per_liter;
		}

		public TankResource(string resource_definition) : this()
		{
			var name_and_value = resource_definition.Split(new []{' '}, 
				StringSplitOptions.RemoveEmptyEntries);
			if(name_and_value.Length != 2) 
			{
				Utils.Log("TankResource: Invalid format of tank resource definition. " +
					"Should be 'ResourceName units_per_liter', got {0}", resource_definition);
				return;
			}
			float val;
			if(!float.TryParse(name_and_value[1], out val) || val <= 0)
			{
				Utils.Log("TankResource: Invalid format of units_per_liter. " +
					"Should be positive float value, got: {0}", name_and_value[1]);
				return;
			}
			var res_def = PartResourceLibrary.Instance.GetDefinition(name_and_value[0]);
			if(res_def == null) 
			{
				Utils.Log("TankResource: Resource does not exist: {0}", name_and_value[0]);
				return;
			}
			Resource = res_def;
			UnitsPerLiter = val;
		}

		public static Dictionary<string, TankResource> ParseResourceList(string resources)
		{
			var res_dict = new Dictionary<string, TankResource>();
			foreach(var _res_str in resources.Split(';'))
			{
				var res_str = _res_str.Trim();
				if(res_str == string.Empty) continue;
				var res = new TankResource(res_str);
				if(!res.Valid) return null;
				try { res_dict.Add(res.Name, res); }
				catch(ArgumentException) {}
			}
			return res_dict;
		}
	}
}

