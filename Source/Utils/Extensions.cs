//   Extensions.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri

using AT_Utils;

namespace AtHangar
{
    public static class PartExtensions
    {
        public static HangarPassage GetPassage(this Part part)
        {
            var passage = part.Modules.GetModule<HangarPassage>();
            if(passage == null) 
                Utils.Message("WARNING: \"{0}\" part has no HangarPassage module.\n" +
                    "The part configuration is INVALID!", part.Title());
            return passage;
        }
    }
}

