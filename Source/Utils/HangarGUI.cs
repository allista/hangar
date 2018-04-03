using System;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
    static class HangarGUI
    {
        #region Widgets
        public static Color UsedVolumeColor(HangarStorage s)
        {
            var frac = s.UsedVolumeFrac;
            return new Color(frac, 1f-frac, 0);
        }

        public static void UsedVolumeLabel(float UsedVolume, float UsedVolumeFrac, string label="Used Volume")
        {
            GUILayout.Label(string.Format("{0}: {1}   {2:P1}", label, 
                                          Utils.formatVolume(UsedVolume), UsedVolumeFrac), 
                            Styles.fracStyle(1-UsedVolumeFrac), GUILayout.ExpandWidth(true));
        }

        public static bool PackedVesselLabel(PackedVessel v, GUIStyle style = null)
        {
            return GUILayout.Button(string.Format("{0}: {1}   Cost: {2:F1}", 
                                                  v.name, Utils.formatMass(v.mass), v.cost), 
                                    style ?? Styles.label, GUILayout.ExpandWidth(true));
        }
        #endregion

        public static void DrawYZ(Metric M, Transform T)
        {
            Utils.GLVec(T.position, T.up*M.extents.y*0.8f, Color.green);
            Utils.GLVec(T.position, T.forward*M.extents.z*0.8f, Color.blue);
        }
    }
}

