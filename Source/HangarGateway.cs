using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class HangarGateway : HangarMachinery
	{
		public static 


		HangarPassage entrance;

		protected override bool compute_hull { get { return false; } }

		protected override List<HangarPassage> get_connected_passages()
		{ return entrance == null ? null : entrance.ConnectedPassages(); }

		protected override void update_connected_storage()
		{
			base.update_connected_storage();
			this.Log("Entrance '{0}', Connected Storages {1}, Storage '{2}'", entrance, ConnectedStorage.Count, Storage);//debug
			if(ConnectedStorage.Count == 0) Storage = null;
			else if(Storage == null || !ConnectedStorage.Contains(Storage))
			{ Storage = ConnectedStorage[0]; Setup(); }
			this.EnableModule(Storage != null);
		}

		protected override void update_connected_storage(Vessel vsl)
		{
			if(vsl != part.vessel || !all_passages_ready) return;
			update_connected_storage();
			if(!enabled && hangar_gates != null) Close();
		}

		protected override void early_setup(StartState state)
		{
			base.early_setup(state);
			entrance = part.GetPassage();
			this.Log("early_setup: entrance '{0}'", entrance);//debug
		}

		protected override bool can_store_vessel(PackedVessel v)
		{
			if(!entrance.CanTransferTo(v, Storage))
			{
				ScreenMessager.showMessage(8, "There's no room in the hangar for this vessel,\n" +
					"OR vessel clearance is insufficient for safe docking.\n\n" +
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

