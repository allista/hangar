using UnityEngine;

namespace AtHangar {
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class HangarToolbar : MonoBehaviour
	{
		const string ICON = "Hangar/Icons/icon_button";
		static Texture2D icon;
		static IButton HangarToolbarButton;
		static ApplicationLauncherButton HangarButton;

		public void Awake ()
		{
			if(!HangarConfig.Globals.UseStockAppLauncher && 
				ToolbarManager.ToolbarAvailable &&
			   	HangarToolbarButton == null)
			{
				HangarToolbarButton = ToolbarManager.Instance.add ("Hangar", "HangarButton");
				HangarToolbarButton.TexturePath = ICON;
				HangarToolbarButton.ToolTip     = "Hangar controls and info";
				HangarToolbarButton.Visibility  = new GameScenesVisibility(GameScenes.FLIGHT,GameScenes.EDITOR);
				HangarToolbarButton.Visible     = true;
				HangarToolbarButton.OnClick    += e => HangarWindow.ToggleGUI();
			}
			else 
			{
				icon = GameDatabase.Instance.GetTexture(ICON, false);
				GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
			}
		}

//		void OnDestroy() 
//		{ 
//			GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
//			if(HangarButton != null)
//				ApplicationLauncher.Instance.RemoveModApplication(HangarButton);
//			if(HangarToolbarButton != null)
//				HangarToolbarButton.Destroy(); 
//		}

		void OnGUIAppLauncherReady()
		{
			if(ApplicationLauncher.Ready &&
			   HangarButton == null)
			{
				HangarButton = ApplicationLauncher.Instance.AddModApplication(
					onAppLaunchToggleOn,
					onAppLaunchToggleOff,
					null, null, null, null,
					ApplicationLauncher.AppScenes.SPH|ApplicationLauncher.AppScenes.VAB|ApplicationLauncher.AppScenes.FLIGHT,
					icon);
			}
		}

		void onAppLaunchToggleOn() { HangarWindow.ShowGUI(); }
		void onAppLaunchToggleOff() { HangarWindow.HideGUI(); }
	}
}
