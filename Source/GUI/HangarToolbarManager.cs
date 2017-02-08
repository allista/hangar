//   Toolbar.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using UnityEngine;
using KSP.UI.Screens;
using AT_Utils;

namespace AtHangar {
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class HangarToolbarManager : MonoBehaviour
	{
		const string TOOLBAR_ICON = "Hangar/Icons/toolbar-icon";
        const string APPLAUNCHER_ICON = "Hangar/Icons/applauncher-icon";
		static Texture2D icon;
		static IButton HangarToolbarButton;
		static ApplicationLauncherButton HangarButton;

		public void Awake ()
		{
			if(!Globals.Instance.UseStockAppLauncher && 
				ToolbarManager.ToolbarAvailable &&
			   	HangarToolbarButton == null)
			{
				HangarToolbarButton = ToolbarManager.Instance.add ("Hangar", "HangarButton");
                HangarToolbarButton.TexturePath = TOOLBAR_ICON;
				HangarToolbarButton.ToolTip     = "Hangar controls and info";
				HangarToolbarButton.Visibility  = new GameScenesVisibility(GameScenes.FLIGHT,GameScenes.EDITOR);
				HangarToolbarButton.Visible     = true;
				HangarToolbarButton.OnClick    += e => HangarWindow.ToggleInstance();
			}
			else 
			{
                icon = GameDatabase.Instance.GetTexture(APPLAUNCHER_ICON, false);
				GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
			}
		}

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

		void onAppLaunchToggleOn() 
		{ HangarWindow.ToggleWithButton(HangarButton); }

		void onAppLaunchToggleOff() 
		{ HangarWindow.ToggleWithButton(HangarButton); }
	}
}
