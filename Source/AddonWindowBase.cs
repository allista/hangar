using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using KSP.IO;

namespace AtHangar
{
	abstract public class AddonWindowBase<T> : MonoBehaviour where T : AddonWindowBase<T>
	{
		public class Styles 
		{
			public static GUIStyle normal;
			public static GUIStyle red;
			public static GUIStyle yellow;
			public static GUIStyle green;
			public static GUIStyle white;
			public static GUIStyle label;

			public static GUIStyle listItem;
			public static GUIStyle listBox;

			private static bool initialized;

			public static void Init ()
			{
				if (initialized) return;
				initialized = true;

				normal = new GUIStyle (GUI.skin.button);
				normal.normal.textColor = normal.focused.textColor = Color.white;
				normal.hover.textColor = normal.active.textColor = Color.yellow;
				normal.onNormal.textColor = normal.onFocused.textColor = normal.onHover.textColor = normal.onActive.textColor = Color.green;
				normal.padding = new RectOffset (8, 8, 8, 8);

				red = new GUIStyle (GUI.skin.box);
				red.padding = new RectOffset (8, 8, 8, 8);
				red.normal.textColor = red.focused.textColor = Color.red;

				yellow = new GUIStyle (GUI.skin.box);
				yellow.padding = new RectOffset (8, 8, 8, 8);
				yellow.normal.textColor = yellow.focused.textColor = Color.yellow;

				green = new GUIStyle (GUI.skin.box);
				green.padding = new RectOffset (8, 8, 8, 8);
				green.normal.textColor = green.focused.textColor = Color.green;

				white = new GUIStyle (GUI.skin.box);
				white.padding = new RectOffset (8, 8, 8, 8);
				white.normal.textColor = white.focused.textColor = Color.white;

				label = new GUIStyle (GUI.skin.label);
				label.normal.textColor = label.focused.textColor = Color.white;
				label.alignment = TextAnchor.MiddleCenter;

				listItem = new GUIStyle ();
				listItem.normal.textColor = Color.white;
				Texture2D texInit = new Texture2D(1, 1);
				texInit.SetPixel(0, 0, Color.white);
				texInit.Apply();
				listItem.hover.background = texInit;
				listItem.onHover.background = texInit;
				listItem.hover.textColor = Color.black;
				listItem.onHover.textColor = Color.black;
				listItem.padding = new RectOffset(4, 4, 4, 4);

				listBox = new GUIStyle(GUI.skin.box);
			}
		}
		
		protected static T instance;
		protected static KSP.IO.PluginConfiguration configfile = KSP.IO.PluginConfiguration.CreateForType<T>();
		protected static bool gui_enabled = true;
		protected static bool hide_ui = false;
		
		//update parameters
		float next_update = 0;
		static float update_interval = 0.2f;
		
		
		//GUI toggles
		public static void ToggleGUI()
		{
			gui_enabled = !gui_enabled;
			if(instance != null) {
				instance.UpdateGUIState();
			}
		}

		public static void HideGUI()
		{
			gui_enabled = false;
			if(instance != null) {
				instance.UpdateGUIState();
			}
		}

		public static void ShowGUI()
		{
			gui_enabled = true;
			if(instance != null) {
				instance.UpdateGUIState();
			}
		}
		
		public void onHideUI()
		{
			hide_ui = true;
			instance.UpdateGUIState();
		}

		public void onShowUI()
		{
			hide_ui = false;
			instance.UpdateGUIState();
		}
		
		virtual public void UpdateGUIState() { enabled = !hide_ui && gui_enabled; }
		
		//update-init-destroy
		abstract public void OnUpdate();
		
		void Update() 
		{ 
			if(Time.time > next_update)
			{
				OnUpdate();
				next_update += update_interval;
			}
		}
		
		protected void Awake() 
		{ 
			LoadSettings(); 
			instance = (T)this; 
			next_update = Time.time; 
			GameEvents.onHideUI.Add(onHideUI);
			GameEvents.onShowUI.Add(onShowUI);
		}
		
		protected void OnDestroy() 
		{ 
			SaveSettings(); 
			GameEvents.onHideUI.Remove(onHideUI);
			GameEvents.onShowUI.Remove(onShowUI);
			instance = null;  
		}
		
		//settings
		public static string mangleName(string name) { return typeof(T).Name+"-"+name; }
		
		virtual public void LoadSettings()
		{
			configfile.load();
			gui_enabled = configfile.GetValue<bool>(mangleName("gui_enabled"));
			UpdateGUIState();
			
		}

		virtual public void SaveSettings()
		{
			configfile.SetValue(mangleName("gui_enabled"), gui_enabled);
			configfile.save();
		}
		
		//GUI staff
		abstract public void OnGUI();
	}
}

