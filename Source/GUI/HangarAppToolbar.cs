//   Toolbar.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using KSP.UI.Screens;
using AT_Utils;

namespace AtHangar
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class HangarAppToolbar : AppToolbar<HangarAppToolbar>
    {
        protected override string TB_ICON => "Hangar/Icons/toolbar-icon";
        protected override string AL_ICON => "Hangar/Icons/applauncher-icon";

        protected override ApplicationLauncher.AppScenes AP_SCENES =>
        ApplicationLauncher.AppScenes.FLIGHT |
            ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.VAB;

        protected override GameScenes[] TB_SCENES =>
        new[] { GameScenes.FLIGHT, GameScenes.EDITOR };

        protected override string button_tooltip => "Hangar controls and info";

        protected override bool ForceAppLauncher => Globals.Instance.UseStockAppLauncher;

        protected override void onLeftClick()
        {
            HangarWindow.ToggleWithButton(ALButton);
        }
    }
}
