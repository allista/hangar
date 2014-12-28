using UnityEngine;

namespace AtHangar {
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class HangarToolbar : MonoBehaviour
	{
		const string ICON = "Hangar/Icons/icon_button";

		IButton HangarToolbarButton;
		ApplicationLauncherButton HangarButton;

		public void Awake ()
		{
			if(ToolbarManager.ToolbarAvailable)
			{
				HangarToolbarButton = ToolbarManager.Instance.add ("Hangar", "HangarButton");
				HangarToolbarButton.TexturePath = ICON;
				HangarToolbarButton.ToolTip = "Hangar controls and info";
				HangarToolbarButton.OnClick += e => HangarWindow.ToggleGUI();
			}
			else 
				GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
		}

		void OnDestroy() 
		{ 
			GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
			if(HangarButton != null)
				ApplicationLauncher.Instance.RemoveModApplication(HangarButton);
			if(HangarToolbarButton != null)
				HangarToolbarButton.Destroy(); 
		}

		void OnGUIAppLauncherReady()
		{
			if (ApplicationLauncher.Ready)
			{
				HangarButton = ApplicationLauncher.Instance.AddModApplication(
					onAppLaunchToggleOn,
					onAppLaunchToggleOff,
					DummyVoid, DummyVoid, DummyVoid, DummyVoid,
					ApplicationLauncher.AppScenes.SPH|ApplicationLauncher.AppScenes.VAB|ApplicationLauncher.AppScenes.FLIGHT,
					GameDatabase.Instance.GetTexture(ICON, false));
			}
		}

		void onAppLaunchToggleOn() { HangarWindow.ShowGUI(); }

		void onAppLaunchToggleOff() { HangarWindow.HideGUI(); }

		void DummyVoid() {}
	}
}
