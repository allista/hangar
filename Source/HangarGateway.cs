using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class HangarGateway : HangarMachinery
	{
		HangarPassage entrance;

		protected override bool compute_hull { get { return false; } }

		protected override List<HangarPassage> get_connected_passages()
		{ return entrance == null ? null : entrance.GetConnectedPassages(); }

		protected override void update_connected_storage()
		{
			base.update_connected_storage();
			if(ConnectedStorage.Count > 0 &&
				(Storage == null || !ConnectedStorage.Contains(Storage)))
			{
				Storage = ConnectedStorage[0];
				Setup();
				this.EnableModule(true);
			}
			else 
			{
				Storage = null;
				this.EnableModule(false);
			}
		}

		protected override void early_setup(StartState state)
		{
			base.early_setup(state);
			entrance = part.GetModule<HangarPassage>();
			if(entrance == null) 
				ScreenMessager.showMessage("WARNING: \"{0}\" part has no HangarPassage module.\n" +
				"The part configuration is INVALID!", part.Title()); 
		}

		protected override bool can_store_vessel(PackedVessel v)
		{
			if(entrance.CanTransferTo(v, Storage))
			{
				ScreenMessager.showMessage(5, "There's no room in the hangar for this vessel,\n" +
					"or vessel clearance is insufficient for safe docking.\n" +
					"\"{0}\" cannot be stored", v.name);
				return false;
			}
			return true;
		}

		protected override Vector3 get_vessel_offset(StoredVessel sv)
		{
			return vessel.LandedOrSplashed ? 
				launch_transform.TransformDirection(-sv.CoG - Vector3.up*sv.size.y) : 
				launch_transform.TransformDirection(sv.CoM - sv.CoG - Vector3.up*sv.size.y);
		}
	}
}

