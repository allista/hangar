//   Addons.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
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
            MODULES = new List<Type>();
            MODULES.AddRange(Assembly.GetExecutingAssembly().GetExportedTypes()
                             .Where(t => !t.IsAbstract && typeof(HangarMachinery).IsAssignableFrom(t)));
            MODULES.AddRange(Assembly.GetExecutingAssembly().GetExportedTypes()
                             .Where(t => !t.IsAbstract && typeof(HangarStorage).IsAssignableFrom(t)));
        }

        protected override bool filter(AvailablePart part)
        {
            return part.moduleInfos.Any(m => MODULES.Any(t => t.Name == m.moduleName));
        }
	}
}
