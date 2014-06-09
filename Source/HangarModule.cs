using UnityEngine;

namespace Hangar
{
	public class HangarModule : PartModule
	{
		[KSPField] public float total_volume;
		[KSPField] public float used_volume;
		[KSPField (guiName = "State", guiActive = true)] public string status;
		
		public HangarModule ()
		{
		}
	}
}

