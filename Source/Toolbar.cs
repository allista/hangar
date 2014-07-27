using UnityEngine;
using Toolbar;

namespace AtHangar {
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class HangarToolbar : MonoBehaviour
	{
		IButton HangarButton;

		public void Awake ()
		{
			HangarButton = ToolbarManager.Instance.add ("Hangar", "HangarButton");
			HangarButton.TexturePath = "Hangar/Textures/icon_button";
			HangarButton.ToolTip = "Hangar controls and info";
			HangarButton.OnClick += e => HangarWindow.ToggleGUI ();
		}

		void OnDestroy() { HangarButton.Destroy(); }
	}
}
