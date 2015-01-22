using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class HangarGateway : HangarMachinery
	{
		/// <summary>
		/// The name of a docking space mesh. 
		/// The mesh should be opened from the door's side.
		/// </summary>
		[KSPField] public string DockingSpace   = string.Empty;
		MeshFilter docking_space;
		Transform  check_transform;

		[KSPField] public string SpawnTransform = string.Empty;
		Transform spawn_transform;

		HangarPassage entrance;

		protected override List<HangarPassage> get_connected_passages()
		{ return entrance == null ? null : entrance.ConnectedPassages(); }

		protected override void update_connected_storage()
		{
			base.update_connected_storage();
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
			//get docking space
			if(DockingSpace != string.Empty)
				docking_space = part.FindModelComponent<MeshFilter>(DockingSpace);
			//get spawn transform
			if(SpawnTransform != string.Empty)
				spawn_transform = part.FindModelTransform(SpawnTransform);
			if(spawn_transform == null) spawn_transform = part.transform;
			//add check transform
			var launch_empty = new GameObject();
			launch_empty.transform.SetParent(spawn_transform);
			check_transform = launch_empty.transform;
		}

		bool vessel_fits_docking_space(PackedVessel v)
		{
			if(docking_space == null) return true;
			check_transform.position = 
				spawn_transform.TransformPoint(Vector3.up*v.size.y/2);
			return v.metric.FitsAligned(check_transform, docking_space.transform, docking_space.sharedMesh);
		}

		protected override bool try_store_vessel(PackedVessel v)
		{
			if(!vessel_fits_docking_space(v))
			{
				ScreenMessager.showMessage(6, "Vessel clearance is insufficient for safe docking.\n\n" +
				                           "\"{0}\" cannot be stored", v.name);
				return false;
			}
			if(!entrance.CanTransferTo(v, Storage))
			{
				ScreenMessager.showMessage(8, "There's no room in the hangar for this vessel,\n" +
					"OR vessel clearance is insufficient for safe docking.\n\n" +
					"\"{0}\" cannot be stored", v.name);
				return false;
			}
			Storage.StoreVessel(v);
			return true;
		}

		protected override Vector3 get_vessel_offset(Transform launch_transform, StoredVessel sv)
		{
			return vessel.LandedOrSplashed ? 
				launch_transform.TransformDirection(-sv.CoG + Vector3.up*sv.size.y/2) : 
				launch_transform.TransformDirection(sv.CoM - sv.CoG + Vector3.up*sv.size.y/2);
		}

		protected override Transform get_spawn_transform(PackedVessel pv) { return spawn_transform; }
		public override Transform GetSpawnTransform() { return null; }

		protected override bool can_restore(PackedVessel v)
		{ 
			if(!base.can_restore(v)) return false;
			if(!vessel_fits_docking_space(v))
			{
				ScreenMessager.showMessage(6, "Vessel clearance is insufficient for safe launch.\n\n" +
				                           "\"{0}\" cannot be launched", v.name);
				return false;
			}
			return true;
		}
	}
}

