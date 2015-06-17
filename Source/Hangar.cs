//   Hangar.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class Hangar : HangarMachinery
	{
		public override string GetInfo()
		{
			var info = base.GetInfo();
			var storage = part.GetModule<HangarStorage>();
			if(storage != null)
			{
				info += storage.AutoPositionVessel?
					"Free launch positioning\n" :
					"Strict launch positioning\n";
			}
			return info;
		}

		protected override List<HangarPassage> get_connected_passages()
		{ return Storage == null ? null : Storage.ConnectedPassages(); }

		protected override void early_setup(StartState state)
		{
			base.early_setup(state);
			Storage = part.GetModule<HangarStorage>();
			if(Storage == null) 
			{
				this.ConfigurationInvalid("\"{0}\" part has no HangarStorage module", part.Title());
				return;
			}
		}

		protected override Vector3 get_vessel_offset(Transform launch_transform, StoredVessel sv)
		{
			return vessel.LandedOrSplashed ? 
				launch_transform.TransformDirection(-sv.CoG) : 
				launch_transform.TransformDirection(sv.CoM - sv.CoG);
		}

		#if DEBUG
		[KSPEvent (guiActive = true, guiName = "Check Airlock", active = true)]
		public void CheckAirlock() 
		{ 
			if(part.airlock == null) return;
			RaycastHit raycastHit;
			if(Physics.Raycast(part.airlock.transform.position, (part.airlock.transform.position - part.transform.position).normalized, out raycastHit, 1, 32769))
			{
				this.Log("Airlock should be blocked:\n" +
				         "collider 'in front': {0}\n" +
				         "distance to it: {1}\n",
				         raycastHit.collider.name,
				         raycastHit.distance
				        );
			}
		}
		#endif
	}
}

