using UnityEngine;

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
		public static GUIStyle slider_text;

		public static GUIStyle list_item;
		public static GUIStyle list_box;

		static bool initialized;
		
		public static void InitSkin()
		{
			if(skin != null) return;
			GUI.skin = null;
			skin = (GUISkin)Object.Instantiate(GUI.skin);
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

			slider_text = new GUIStyle (GUI.skin.label);
			slider_text.alignment = TextAnchor.MiddleCenter;
			slider_text.margin = new RectOffset (0, 0, 0, 0);

			list_item = new GUIStyle(GUI.skin.box);
			Texture2D texInit = new Texture2D(1, 1);
			texInit.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.05f, 1f));
			texInit.Apply();
			list_item.normal.background = list_item.onNormal.background = list_item.hover.background = list_item.onHover.background = texInit;
			list_item.normal.textColor = list_item.focused.textColor = Color.white;
			list_item.hover.textColor = list_item.active.textColor = Color.yellow;
			list_item.onNormal.textColor = list_item.onFocused.textColor = list_item.onHover.textColor = list_item.onActive.textColor = Color.yellow;
			list_item.padding = new RectOffset(4, 4, 4, 4);

			list_box = new GUIStyle(GUI.skin.button);
			list_box.normal.textColor = list_box.focused.textColor = Color.yellow;
			list_box.hover.textColor = list_box.active.textColor = Color.green;
			list_box.onNormal.textColor = list_box.onFocused.textColor = list_box.onHover.textColor = list_box.onActive.textColor = Color.green;
			list_box.padding = new RectOffset (4, 4, 4, 4);
		}
		
		public static GUIStyle fracStyle(float frac)
		{
			if(frac < 0.1) return Styles.red;
			if(frac < 0.5) return Styles.yellow;
			if(frac < 0.8) return Styles.white;
			return Styles.green;
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
		const float update_interval = 0.2f;
		
		
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
		
		public static void onHideUI()
		{
			hide_ui = true;
			instance.UpdateGUIState();
		}

		public static void onShowUI()
		{
			hide_ui = false;
			instance.UpdateGUIState();
		}
		
		virtual public void UpdateGUIState() { enabled = !hide_ui && gui_enabled; }
		
		//update-init-destroy
		abstract public void OnUpdate();
		
		virtual public void Update() 
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

