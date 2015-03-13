using System;
using System.Linq;
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
		#region Tank Type Library
		/// <summary>
		/// The library of preconfigured tank types 
		/// that is loaded on game start by HangarConfigLoader.
		/// </summary>
		public static SortedList<string, SwitchableTankType> TankTypes 
		{ 
			get
			{
				if(_tank_types == null)
				{
					var nodes = GameDatabase.Instance.GetConfigNodes(NODE_NAME);
					_tank_types = new SortedList<string, SwitchableTankType>(nodes.Length);
					foreach(ConfigNode n in nodes)
					{
						var tank_type = new SwitchableTankType();
						#if DEBUG
						Utils.Log("\n{0}", n.ToString());
						#endif
						tank_type.Load(n);
						if(!tank_type.Valid)
						{
							ScreenMessager.showMessage(6, "Hangar: configuration of \"{0}\" tank type is INVALID.", tank_type.name);
							continue;
						}
						try { _tank_types.Add(tank_type.name, tank_type); }
						catch { Utils.Log("SwitchableTankType: ignoring duplicate configuration of {0} tank type", tank_type.name); }
					}
				}
				return _tank_types;
			}
		}
		static SortedList<string, SwitchableTankType> _tank_types;

		/// <summary>
		/// Sorted list of tank type names.
		/// </summary>
		public static IList<string> TankTypeNames { get { return TankTypes.Keys; } }

		/// <summary>
		/// Returns info string describing available tank types
		/// </summary>
		public static string TypesInfo
		{
			get
			{
				var info = "Available Tank Types:\n";
				info += TankTypeNames.Aggregate("", (i, t) => i+"- "+t+"\n");
				return info;
			}
		}
		#endregion

		new public const string NODE_NAME = "TANKTYPE";
		/// <summary>
		/// The name of the tank type. 
		/// It is possible to edit these nodes with MM using NODE[name] syntax.
		/// </summary>
		[Persistent] public string name;
		/// <summary>
		/// The string list of resources a tank of this type can hold. Format:
		/// ResourceName1 units_per_liter; ResourceName2 units_per_liter2; ...
		/// </summary>
		[Persistent] public string PossibleResources;
		/// <summary>
		/// The portion of a part's volume the tank can use.
		/// </summary>
		[Persistent] public float  UsefulVolumeRatio = 1f;
		/// <summary>
		/// The cost of a tank of this type per tank volume.
		/// </summary>
		[Persistent] public float  TankCostPerVolume = 10f;

		public SortedList<string, TankResource> Resources { get; private set; }
		public bool Valid { get { return Resources != null && Resources.Count > 0; } }
		public IList<string> ResourceNames { get { return Resources.Keys; } }
		public TankResource DefaultResource { get { return Resources.Values[0]; } }

		public TankResource this[string name]
		{
			get
			{
				try { return Resources[name]; }
				catch { return null; }
			}
		}

		public override void Load(ConfigNode node)
		{
			base.Load(node);
			Resources = TankResource.ParseResourcesToSortedList(PossibleResources);
		}

		public string Info
		{ 
			get 
			{ 
				var info = "";
				if(!Valid) return info;
				info += "Tank can hold:\n";
				foreach(var r in ResourceNames)
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

