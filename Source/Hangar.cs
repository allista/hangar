using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class Hangar : HangarMachinery
	{
		public ConfigNode ModuleConfig;

		[KSPField (isPersistant = false)] public bool UseHangarSpaceMesh = false;
		MeshFilter hangar_space;
		protected override bool compute_hull { get { return hangar_space != null; } }

		protected override List<HangarPassage> get_connected_passages()
		{ return Storage == null ? null : Storage.GetConnectedPassages(); }

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
			//****************************//
			if(UseHangarSpaceMesh && Storage.HangarSpace != string.Empty)
				hangar_space = part.FindModelComponent<MeshFilter>(Storage.HangarSpace);
		}

		bool metric_fits_into_hangar_space(Metric m)
		{
			GetLaunchTransform();
			return hangar_space == null ? 
				m.FitsAligned(launch_transform, part.partTransform, Storage.HangarMetric) : 
				m.FitsAligned(launch_transform, hangar_space.transform, hangar_space.sharedMesh);
		}

		protected override bool can_store_vessel(PackedVessel v)
		{
			if(!metric_fits_into_hangar_space(v.metric))
			{
				ScreenMessager.showMessage(5, "Insufficient vessel clearance for safe docking\n" +
					"\"{0}\" cannot be stored in this hangar", v.name);
				return false;
			}
			return true;
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
			if(node.HasValue("base_mass"))
				ModuleConfig = node;
			//****************************//
		}
	}
}

