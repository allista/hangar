//   Addons.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Linq;
using System.Reflection;
using AT_Utils;

namespace AtHangar
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class HangarFilterManager : SimplePartFilter
    {
        public HangarFilterManager()
        {
            SUBCATEGORY = "Hangars";
            FOLDER = "Hangar/Icons";
            ICON = "HangarCategory";
            SetMODULES(Assembly.GetExecutingAssembly().GetExportedTypes()
                       .Where(t => !t.IsAbstract && (typeof(HangarMachinery).IsAssignableFrom(t) ||
                                                     typeof(HangarStorage).IsAssignableFrom(t))));
        }
    }
}
