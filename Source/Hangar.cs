using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class Hangar : HangarMachinery
	{
		public ConfigNode ModuleConfig;

		protected override List<HangarPassage> get_connected_passages()
		{ return Storage == null ? null : Storage.ConnectedPassages(); }

		protected override void early_setup(StartState state)
		{
			base.early_setup(state);
			Storage = part.GetModule<HangarStorage>();
			if(Storage == null) 
			{ 
				ScreenMessager.showMessage("WARNING: \"{0}\" part has no HangarStorage module.\n" +
				"The part configuration is INVALID!", part.Title()); 
				return; 
			}
			//deprecated config conversion//
			if(ModuleConfig != null)
			{
				Storage.OnLoad(ModuleConfig);
				Storage.Setup();
			}
			Storage.GetSpawnTransform += GetLaunchTransform;
			//****************************//
		}

		protected override Vector3 get_vessel_offset(StoredVessel sv)
		{
			return vessel.LandedOrSplashed ? 
				launch_transform.TransformDirection(-sv.CoG) : 
				launch_transform.TransformDirection(sv.CoM - sv.CoG);
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			//deprecated config conversion//
			ModuleConfig = node.HasValue("base_mass")? node : null;
			//****************************//
		}
	}
}

