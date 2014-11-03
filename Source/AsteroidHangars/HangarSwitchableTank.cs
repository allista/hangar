using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using KSPAPIExtensions;
using UnityEngine;

namespace AtHangar
{
	/// <summary>
	/// This is a different approach than in ModularFuelTanks, more suitable for "cargo" resources than fuels:
	/// Such tank may contain only one type of resources, but this type may be switched in-flight, 
	/// if the part has zero amount of the current resource.
	/// </summary>
	public class HangarSwitchableTank : PartModule, IPartCostModifier
	{
		/// <summary>
		/// The camel case components matching regexp.
		/// From: http://stackoverflow.com/questions/155303/net-how-can-you-split-a-caps-delimited-string-into-an-array
		/// </summary>
		const string CamelCaseRegexp = "([a-z](?=[A-Z])|[A-Z](?=[A-Z][a-z]))";

		/// <summary>
		/// If a tank type can be selected in editor.
		/// </summary>
		[KSPField] public bool ChooseTankType;
		/// <summary>
		/// The type of the tank. Types are defined in separate config nodes. Cannot be changed in flight.
		/// </summary>
		[KSPField(guiActiveEditor = true, guiName = "Tank Type")]
		[UI_ChooseOption(scene = UI_Scene.Editor)]
		public string TankType;
		SwitchableTankType tank_type;

		/// <summary>
		/// The volume of a tank in liters. It is defined in a config or calculated from the part volume in editor.
		/// Cannot be changed in flight.
		/// </summary>
		[KSPField(isPersistant = true)] public float  Volume = -1f;

		/// <summary>
		/// The name of a currently selected resource. Can be changed in flight if resource amount is zero.
		/// </summary>
		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Resource")]
		[UI_ChooseOption]
		public string CurrentResource = string.Empty;
		PartResource current_resource;

		public ConfigNode ModuleConfig;

		public override string GetInfo()
		{
			var info = "";
			if(ChooseTankType)
			{
				info += "Available Tank Types:\n";
				info += SwitchableTankType.TankTypeNames.Aggregate("", (i, t) => i+"- "+t+"\n");
			}
			if(!init_tank_type()) return info;
			info += tank_type.Info;
			init_tank_volume();
			info += "Tank Volume: " + Utils.formatVolume(Volume/1000f);
			return info;
		}

		public float GetModuleCost() { return part.MaxResourcesCost(); }

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			init_tank_volume();
			//initialize tank type chooser
			HangarGUI.EnableField(Fields["TankType"], false);
			if( ChooseTankType &&
				state == StartState.Editor &&
				SwitchableTankType.TankTypes.Count > 1)
			{
				var names = SwitchableTankType.TankTypeNames;
				var tank_types = names.ToArray();
				var tank_names = names.ConvertAll(s => Regex.Replace(s, CamelCaseRegexp, "$1 ")).ToArray();
				HangarGUI.SetupChooser(tank_names, tank_types, Fields["TankType"]);
				HangarGUI.EnableField(Fields["TankType"]);
			}
			init_tank_type();
			switch_resource();
			StartCoroutine(slow_update());
		}

		void init_tank_volume()
		{
			if(Volume > 0) return;
			Volume = Metric.Volume(part)*tank_type.UsefulVolumeRatio*1000f;
		}

		bool init_tank_type()
		{
			//check if the tank is in use
			if( tank_type != null && 
				current_resource != null &&
				current_resource.amount > 0)
				{ 
					ScreenMessager.showMessage("Cannot change tank type while tank is in use");
					TankType = tank_type.Name;
					return false;
				}
			//setup new tank type
			tank_type = null;
			//if tank type is not provided, use the first one from the library
			if(string.IsNullOrEmpty(TankType))
			{ TankType = SwitchableTankType.TankTypeNames[0]; }
			//if just loaded and there is a saved config of a tank type, use it
			if( ModuleConfig != null &&
				ModuleConfig.HasNode(SwitchableTankType.NODE_NAME))
			{
				tank_type = new SwitchableTankType();
				tank_type.Load(ModuleConfig.GetNode(SwitchableTankType.NODE_NAME));
				if(!tank_type.Valid)
				{
					ScreenMessager.showMessage(6, "Hangar: Configuration of \"{0}\" Tank Type in \"{1}\" is INVALID.\n", 
						tank_type.Name, this.Title());
					tank_type = null;
				}
				else TankType = tank_type.Name;
				ModuleConfig = null;
			} 
			//otherwise, select tank type from the library
			else if(!SwitchableTankType.TankTypes.TryGetValue(TankType, out tank_type))
				ScreenMessager.showMessage(6, "Hangar: No \"{0}\" tank type in the library. Configuration of \"{1}\" is INVALID.", 
					TankType, this.Title());
			//switch off the UI
			HangarGUI.EnableField(Fields["CurrentResource"], false);
			if(tank_type == null) return false;
			//initialize new tank UI if needed
			if(tank_type.Resources.Count > 1)
			{
				var res_values = tank_type.SortedNames.ToArray();
				var res_names  = tank_type.SortedNames.ConvertAll(s => Regex.Replace(s, CamelCaseRegexp, "$1 ")).ToArray();
				HangarGUI.SetupChooser(res_names, res_values, Fields["CurrentResource"]);
				HangarGUI.EnableField(Fields["CurrentResource"]);
			}
			//initialize current resource
			if(CurrentResource == string.Empty || 
				!tank_type.Resources.ContainsKey(CurrentResource)) 
				CurrentResource = tank_type.DefaultResource.Name;
			return true;
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			ModuleConfig = node;
		}

		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
			if(tank_type != null && tank_type.Valid)
			{
				ConfigNode tank_node = node.AddNode(SwitchableTankType.NODE_NAME);
				tank_type.Save(tank_node);
			}
		}

		bool switch_resource()
		{
			if(tank_type == null) return false;
			var res = tank_type[CurrentResource];
			if(res == null)
			{
				ScreenMessager.showMessage("The tank cannot hold {0}", CurrentResource);
				CurrentResource = current_resource.resourceName;
				return false;
			}
			//if some resource is currently managed, check it's amount 
			//and if it's zero, remove the resource
			if(current_resource != null)
			{
				if(current_resource.amount > 0) 
				{ 
					ScreenMessager.showMessage("Cannot change resource type while tank is in use");
					CurrentResource = current_resource.resourceName;
					return false;
				}
				part.Resources.list.Remove(current_resource); 
				Destroy(current_resource);
				current_resource = null;
			}
			//calculate maxAmount
			var maxAmount = Volume*res.UnitsPerLiter;
			//if there is such resource already, just plug it in
			var part_res = part.Resources[res.Name];
			if(part_res != null)
			{
				current_resource = part_res;
				current_resource.maxAmount = maxAmount;
				if(current_resource.amount > current_resource.maxAmount)
					current_resource.amount = current_resource.maxAmount;
				return true;
			}
			//otherwise, create the new resource
			var node = new ConfigNode("RESOURCE");
			node.AddValue("name", res.Name);
			node.AddValue("amount", 0);
			node.AddValue("maxAmount", Volume*res.UnitsPerLiter);
			part.Resources.Add(node);
			current_resource = part.Resources[res.Name];
			return true;
		}

		IEnumerator<YieldInstruction> slow_update()
		{
			while(true)
			{
				if(HighLogic.LoadedSceneIsEditor &&
				   (tank_type == null || 
					tank_type.Name != TankType))
					init_tank_type();
				if(	current_resource != null && 
					current_resource.resourceName != CurrentResource)
					switch_resource();
				yield return new WaitForSeconds(0.1f);
			}
		}
	}
}

