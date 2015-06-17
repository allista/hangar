using System.Linq;
using UnityEngine;

namespace AtHangar
{
	/// <summary>
	/// Screen messager is an addon that displays on-screen 
	/// messages in the top-center of the screen.
	/// It is a part of the Hangar module.
	/// </summary>
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class ScreenMessager : MonoBehaviour
	{
		static float  osdMessageTime = 0;
		static string osdMessageText = null;

		public static void showMessage(string msg, params object[] args)
		{ showMessage(3, msg, args);}

		public static void showMessage(float delay, string msg, params object[] args)
		{
			#if DEBUG
			if(osdMessageText != string.Format(msg, args))
				Utils.Log(msg, args);
			#endif
			osdMessageText = string.Format(msg, args);
			osdMessageTime = Time.time + delay;
		}

		public void OnGUI ()
		{
			if (Time.time < osdMessageTime) 
			{
				GUI.skin = HighLogic.Skin;
				GUIStyle style = new GUIStyle("Label");
				style.alignment = TextAnchor.MiddleCenter;
				style.fontSize = 20;
				style.normal.textColor = Color.black;
				GUI.Label (new Rect (2, 2 + (Screen.height / 9), Screen.width, 50), osdMessageText, style);
				style.normal.textColor = Color.yellow;
				GUI.Label (new Rect (0, Screen.height / 9, Screen.width, 50), osdMessageText, style);
			}
		}
	}

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
			button.SetFalse(button, RUIToggleButtonTyped.ClickType.FORCED);
			button.SetTrue(button, RUIToggleButtonTyped.ClickType.FORCED);
		}
	}
}

