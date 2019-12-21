using UnityEngine;

namespace AtHangar
{
    public class HangarVesselModule : VesselModule
    {
        [KSPField(isPersistant = true)] public bool ShowHangarWindow;
    }
}
