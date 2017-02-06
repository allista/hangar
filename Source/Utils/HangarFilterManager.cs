//   Addons.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Linq;
using UnityEngine;
using KSP.UI;
using KSP.UI.Screens;

namespace AtHangar
{
	/// <summary>
	/// Addon that assembles a custom part filter by function in the part library.
	/// The code is adapted from the RealChute mod by Christophe Savard (stupid_chris):
	/// https://github.com/StupidChris/RealChute/blob/master/RealChute/RCFilterManager.cs
	/// Many thanks to Chris for figuring this out so fast!
	/// </summary>
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class HangarFilterManager : MonoBehaviour
	{
		const string CATEGORY = "Hangars";
		const string ICON = "HangarCategory";

		void Awake()
		{ GameEvents.onGUIEditorToolbarReady.Add(add_filter); }

		static bool is_hangar(string module_name)
		{
			module_name = string.Join("", module_name.Split());
			if(module_name == typeof(Hangar).Name) return true;
			if(module_name == typeof(HangarEntrance).Name) return true;
			if(module_name == typeof(HangarGateway).Name) return true;
			if(module_name == typeof(HangarStorage).Name) return true;
			if(module_name == typeof(SimpleHangarStorage).Name) return true;
			if(module_name == typeof(SingleUseHangarStorage).Name) return true;
			return false;
		}

		void add_filter()
		{
			//load the icon
			if(!PartCategorizer.Instance.iconLoader.iconDictionary.ContainsKey(ICON))
			{
				var _icon   = GameDatabase.Instance.GetTexture("Hangar/Icons/"+ICON, false);
				var _icon_s = GameDatabase.Instance.GetTexture("Hangar/Icons/"+ICON+"_selected", false);
				var pc_icon = new RUI.Icons.Selectable.Icon(ICON, _icon, _icon_s, true);
				PartCategorizer.Instance.iconLoader.icons.Add(pc_icon);
				PartCategorizer.Instance.iconLoader.iconDictionary.Add(ICON, pc_icon);
			}
			var icon = PartCategorizer.Instance.iconLoader.GetIcon(ICON);
			//add custom function filter
			var filter = PartCategorizer.Instance.filters
				.Find(f => f.button.categoryName == "Filter by Function");
			PartCategorizer.AddCustomSubcategoryFilter(filter, CATEGORY, icon, 
				p => p.moduleInfos.Any(m => is_hangar(m.moduleName)));
			//set icon(s) for all the modules
			PartCategorizer.Instance.filters
				.Find(f => f.button.categoryName == "Filter by Module")
				.subcategories.FindAll(s => is_hangar(s.button.categoryName))
				.ForEach(c => c.button.SetIcon(icon));
			//Apparently needed to make sure the icon actually shows at first
			var button = filter.button.activeButton;
			button.Value = false;
			button.Value = true;
		}
	}
}

