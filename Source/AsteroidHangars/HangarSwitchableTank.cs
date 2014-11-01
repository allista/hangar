using System;
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
	public class HangarSwitchableTank : PartModule
	{
		/// <summary>
		/// The camel case components matching regexp.
		/// From: http://stackoverflow.com/questions/155303/net-how-can-you-split-a-caps-delimited-string-into-an-array
		/// </summary>
		const string CamelCaseRegexp = "([a-z](?=[A-Z])|[A-Z](?=[A-Z][a-z]))";
		/// <summary>
		/// The type of the tank. Types are defined in separate config nodes.
		/// </summary>
		[KSPField] public string TankType;
		/// <summary>
		/// The volume of a tank. Is defined in a config or calculated from the part volume in editor.
		/// Cannot be changed in flight.
		/// </summary>
		[KSPField(isPersistant = true)] public float  Volume = -1f;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Resource")]
		[UI_ChooseOption]
		public string CurrentResource = string.Empty;
		PartResource current_resource;

		public ConfigNode ModuleConfig;
		SwitchableTankType tank_type;


		public override string GetInfo()
		{
			var info = "";
			if(!init_tank_type()) return info;
			info += tank_type.Info;
			init_tank_volume();
			info += "Tank Volume: " + Utils.formatVolume(Volume/1000f);
			return info;
		}

		static void setup_res_chooser(string[] names, string[] values, UI_Control control)
		{
			var current_res_edit = control as UI_ChooseOption;
			if(current_res_edit == null) return;
			current_res_edit.display = names;
			current_res_edit.options = values;
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			if(!init_tank_type()) return;
			init_tank_volume();
			//initialize UI
			if(tank_type.Resources.Count > 1)
			{
				var res_values = tank_type.SortedNames.ToArray();
				var res_names  = tank_type.SortedNames.ConvertAll(s => Regex.Replace(s, CamelCaseRegexp, "$1 ")).ToArray();
				setup_res_chooser(res_names, res_values, Fields["CurrentResource"].uiControlEditor);
				setup_res_chooser(res_names, res_values, Fields["CurrentResource"].uiControlFlight);
			}
			else Fields["CurrentResource"].guiActive = Fields["CurrentResource"].guiActiveEditor = false;
			//initialize resource
			if(CurrentResource == string.Empty) CurrentResource = tank_type.DefaultResource.Name;
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
			tank_type = null;
			if(ModuleConfig.HasNode(SwitchableTankType.NODE_NAME))
			{
				tank_type = new SwitchableTankType();
				tank_type.Load(ModuleConfig.GetNode(SwitchableTankType.NODE_NAME));
				if(!tank_type.Valid)
				{
					ScreenMessager.showMessage(6, "Hangar: Configuration of \"{0}\" Tank Type in \"{1}\" is INVALID.\n", 
						tank_type.Name, this.Title());
					tank_type = null;
				}
			}
			else if(!SwitchableTankType.TankTypes.TryGetValue(TankType, out tank_type))
				ScreenMessager.showMessage(6, "Hangar: No \"{0}\" tank type in the library. Configuration of \"{1}\" is INVALID.", 
					TankType, this.Title());
			enabled = isEnabled = tank_type != null;
			return enabled;
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
			var res = tank_type[CurrentResource];
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
				if(current_resource != null && 
					current_resource.resourceName != CurrentResource)
					switch_resource();
				yield return new WaitForSeconds(0.1f);
			}
		}
	}
}

