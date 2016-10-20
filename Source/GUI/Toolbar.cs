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
	public class HangarToolbar : MonoBehaviour
	{
		const string ICON = "Hangar/Icons/icon_button";
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
				HangarToolbarButton.TexturePath = ICON;
				HangarToolbarButton.ToolTip     = "Hangar controls and info";
				HangarToolbarButton.Visibility  = new GameScenesVisibility(GameScenes.FLIGHT,GameScenes.EDITOR);
				HangarToolbarButton.Visible     = true;
				HangarToolbarButton.OnClick    += e => HangarWindow.Toggle();
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

		void onAppLaunchToggleOn() { HangarWindow.Show(true); }
		void onAppLaunchToggleOff() { HangarWindow.Show(false); }
	}
}
