using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using KSP.IO;

namespace AtHangar
{
	public static class Styles //This is the code from Extraplanetary Launchpad plugin.
	{
		public static GUISkin skin;
		
		public static GUIStyle normal_button;
		public static GUIStyle red_button;
		public static GUIStyle green_button;
		public static GUIStyle yellow_button;
		public static GUIStyle red;
		public static GUIStyle yellow;
		public static GUIStyle green;
		public static GUIStyle white;
		public static GUIStyle label;
		public static GUIStyle slider;
		public static GUIStyle sliderText;

		public static GUIStyle listItem;
		public static GUIStyle listBox;

		private static bool initialized;
		
		public static void InitSkin()
		{
			if(skin != null) return;
			GUI.skin = null;
			skin = (GUISkin)GameObject.Instantiate(GUI.skin);
		}
		
		public static void InitGUI()
		{
			if (initialized) return;
			initialized = true;
			
			normal_button = new GUIStyle (GUI.skin.button);
			normal_button.normal.textColor = normal_button.focused.textColor = Color.white;
			normal_button.hover.textColor = normal_button.active.textColor = Color.yellow;
			normal_button.onNormal.textColor = normal_button.onFocused.textColor = normal_button.onHover.textColor = normal_button.onActive.textColor = Color.yellow;
			normal_button.padding = new RectOffset (4, 4, 4, 4);
			
			red_button = new GUIStyle (GUI.skin.button);
			red_button.normal.textColor = red_button.focused.textColor = Color.red;
			red_button.hover.textColor = red_button.active.textColor = Color.yellow;
			red_button.onNormal.textColor = red_button.onFocused.textColor = red_button.onHover.textColor = red_button.onActive.textColor = Color.yellow;
			red_button.padding = new RectOffset (4, 4, 4, 4);
			
			green_button = new GUIStyle (GUI.skin.button);
			green_button.normal.textColor = green_button.focused.textColor = Color.green;
			green_button.hover.textColor = green_button.active.textColor = Color.yellow;
			green_button.onNormal.textColor = green_button.onFocused.textColor = green_button.onHover.textColor = green_button.onActive.textColor = Color.yellow;
			green_button.padding = new RectOffset (4, 4, 4, 4);
			
			yellow_button = new GUIStyle (GUI.skin.button);
			yellow_button.normal.textColor = yellow_button.focused.textColor = Color.yellow;
			yellow_button.hover.textColor = yellow_button.active.textColor = Color.green;
			yellow_button.onNormal.textColor = yellow_button.onFocused.textColor = yellow_button.onHover.textColor = yellow_button.onActive.textColor = Color.green;
			yellow_button.padding = new RectOffset (4, 4, 4, 4);

			red = new GUIStyle (GUI.skin.box);
			red.padding = new RectOffset (4, 4, 4, 4);
			red.normal.textColor = red.focused.textColor = Color.red;

			yellow = new GUIStyle (GUI.skin.box);
			yellow.padding = new RectOffset (4, 4, 4, 4);
			yellow.normal.textColor = yellow.focused.textColor = Color.yellow;

			green = new GUIStyle (GUI.skin.box);
			green.padding = new RectOffset (4, 4, 4, 4);
			green.normal.textColor = green.focused.textColor = Color.green;

			white = new GUIStyle (GUI.skin.box);
			white.padding = new RectOffset (4, 4, 4, 4);
			white.normal.textColor = white.focused.textColor = Color.white;

			label = new GUIStyle (GUI.skin.label);
			label.normal.textColor = label.focused.textColor = Color.white;
			label.alignment = TextAnchor.MiddleCenter;

			slider = new GUIStyle (GUI.skin.horizontalSlider);
			slider.margin = new RectOffset (0, 0, 0, 0);

			sliderText = new GUIStyle (GUI.skin.label);
			sliderText.alignment = TextAnchor.MiddleCenter;
			sliderText.margin = new RectOffset (0, 0, 0, 0);

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
	
	
	abstract public class AddonWindowBase<T> : MonoBehaviour where T : AddonWindowBase<T>
	{
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
			gui_enabled = configfile.GetValue<bool>(mangleName("gui_enabled"), true);
		}

		virtual public void SaveSettings()
		{
			configfile.SetValue(mangleName("gui_enabled"), gui_enabled);
			configfile.save();
		}
		
		virtual public void OnGUI()
		{
			Styles.InitSkin();
			GUI.skin = Styles.skin;
			Styles.InitGUI();
		}
	}
}

