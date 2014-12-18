using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	/// <summary>
	/// Loads hangar configuration presets at game loading start
	/// </summary>
	[KSPAddon(KSPAddon.Startup.Instantly, true)]
	public class HangarConfig : MonoBehaviour
	{
		//root config node name and value
		public const string HANGAR_CONFIG  = "HANGARCONFIG";
		static ConfigNode root;

		#region Configuration
		public static List<string> MeshesToSkip { get; private set; }

		static void parse_values()
		{
			//init meshes names
			var meshes = GetValue(property_name(new {MeshesToSkip}));
			MeshesToSkip = new List<string>(meshes.Split(new []{' '}, StringSplitOptions.RemoveEmptyEntries));
		}
		#endregion

		#region Public Interface
		public static ConfigNode[] GetNodes(string node_name)
		{ return root.GetNodes(node_name); }

		public static string GetValue(string cfg_name, string separator = " ")
		{
			return root.GetValues(cfg_name).Aggregate("", (val, v) => 
				val + ((val != "")? separator+v : v));
		}
		#endregion

		#region Implementation
		//from http://stackoverflow.com/questions/716399/c-sharp-how-do-you-get-a-variables-name-as-it-was-physically-typed-in-its-dec
		//second answer
		static string property_name<T>(T obj) { return typeof(T).GetProperties()[0].Name; }

		public void Start()
		{
			//load_config//
			var roots = GameDatabase.Instance.GetConfigNodes(HANGAR_CONFIG);
			if(roots.Length == 0) return;
			if(roots.Length > 1)
				Utils.Log("HangarConfig: found {0} versions of {1} node. Using the first one.", 
					roots.Length, HANGAR_CONFIG);
			root = roots[0];
			//-----------//
			parse_values();
		}
		#endregion
	}

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

		static bool has_hangar(string module_name)
		{
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
			if(!PartCategorizer.Instance.iconDictionary.ContainsKey(ICON))
			{
				var _icon   = GameDatabase.Instance.GetTexture("Hangar/Icons/"+ICON, false);
				var _icon_s = GameDatabase.Instance.GetTexture("Hangar/Icons/"+ICON+"_selected", false);
				var pc_icon = new PartCategorizer.Icon(ICON, _icon, _icon_s, true);
				PartCategorizer.Instance.icons.Add(pc_icon);
				PartCategorizer.Instance.iconDictionary.Add(ICON, pc_icon);
			}
			var icon = PartCategorizer.Instance.GetIcon(ICON);
			//add custom function filter
			var filter = PartCategorizer.Instance.filters
				.Find(f => f.button.categoryName == "Filter by Function");
			PartCategorizer.AddCustomSubcategoryFilter(filter, CATEGORY, icon, 
				p => p.moduleInfos.Any(m => has_hangar(m.moduleName)));
			//set icon(s) for all the modules
			PartCategorizer.Instance.filters
				.Find(f => f.button.categoryName == "Filter by Module")
				.subcategories.FindAll(s => has_hangar(string.Join("", s.button.categoryName.Split())))
				.ForEach(c => c.button.SetIcon(icon));
			//Apparently needed to make sure the icon actually shows at first
			var button = filter.button.activeButton;
			button.SetFalse(button, RUIToggleButtonTyped.ClickType.FORCED);
			button.SetTrue(button, RUIToggleButtonTyped.ClickType.FORCED);
		}
	}
}

