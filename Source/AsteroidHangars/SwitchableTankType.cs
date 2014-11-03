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

		/// <summary>
		/// Gets the sorted list of tank type names.
		/// </summary>
		public static List<string> TankTypeNames
		{
			get
			{
				var types = TankTypes;
				var names = new List<string>(types.Keys);
				names.Sort();
				return names;
			}
		}

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
				TankResource res = null;
				if(Valid) Resources.TryGetValue(name, out res);
				return res;
			}
		}

		public override void Load(ConfigNode node)
		{
			base.Load(node);
			Resources = TankResource.ParseResourcesToDict(PossibleResources);
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
					info += string.Format("- {0}: {1}/L\n", 
						Resources[r].Name, Utils.formatUnits(Resources[r].UnitsPerLiter));
				return info;
			} 
		}
	}


	/// <summary>
	/// A Part Resource Definition complemented with Units Per Liter ratio.
	/// </summary>
	public class TankResource : ResourceWrapper<TankResource>
	{
		public float UnitsPerLiter { get; private set; }

		public override void LoadDefinition(string resource_definition)
		{
			var upl = load_definition(resource_definition);
			if(Valid) UnitsPerLiter = upl;
		}	
	}
}

