using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Toolbar;

namespace AtHangar {
	
	[KSPAddon (KSPAddon.Startup.EditorAny, false)]
	public class HangarToolbar_Editor : MonoBehaviour
	{
		private IButton HangarEditorButton;

		public void Awake ()
		{
			HangarEditorButton = ToolbarManager.Instance.add ("Hangar", "HangarEditorButton");
			HangarEditorButton.TexturePath = "Hangar/Textures/icon_button";
			HangarEditorButton.ToolTip = "Hangar info display";
			HangarEditorButton.OnClick += (e) => VesselInfoWindow.ToggleGUI ();
		}

		void OnDestroy() { HangarEditorButton.Destroy(); }
	}

	[KSPAddon (KSPAddon.Startup.Flight, false)]
	public class HangarToolbar_Flight : MonoBehaviour
	{
		private IButton HangarFlightButton;

		public void Awake ()
		{
			HangarFlightButton = ToolbarManager.Instance.add ("Hangar", "HangarFlightButton");
			HangarFlightButton.TexturePath = "Hangar/Textures/icon_button";
			HangarFlightButton.ToolTip = "Hangar controls";
			HangarFlightButton.OnClick += (e) => HangarWindow.ToggleGUI ();
		}

		void OnDestroy() { HangarFlightButton.Destroy(); }
	}
}
