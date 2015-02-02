//   AsteroidMassConverter.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AtHangar
{
	public class AsteroidMassConverter : AnimatedConverterBase
	{
		[KSPField] public string OutputResource;
		[KSPField] public float  Efficiency = 0.92f; // 8% of mass is lost
		[KSPField] public float  ConversionRate = 0.01f; // tons per electric charge

		ResourcePump pump;
		float dM_buffer;

		#region Parts & Modules
		HangarPassage entrance;
		List<HangarPassage> passage_checklist;
		SingleUseGrappleNode grapple_node;
		HangarStorageDynamic storage;
		Part asteroid;
		AsteroidInfo asteroid_info;
		PartResourceDefinition resource;
		#endregion

		#region Setup
		public override string GetInfo()
		{
			var info = base.GetInfo();
			var mass_flow = ConversionRate*EnergyConsumption*RatesMultiplier;
			info += string.Format("Mass Extraction: {0}/sec\n", Utils.formatMass(mass_flow));
			resource = PartResourceLibrary.Instance.GetDefinition(OutputResource);
			if(resource != null)
				info += string.Format("Produces {0}: {1}/sec", 
					OutputResource, Utils.formatUnits(mass_flow*Efficiency/resource.density));
			return info;
		}

		public override void OnAwake()
		{
			base.OnAwake();
			GameEvents.onVesselWasModified.Add(update_state);
		}

		public override void OnDestroy() 
		{ 
			base.OnDestroy();
			GameEvents.onVesselWasModified.Remove(update_state); 
		}

		void update_state(Vessel vsl)
		{ 
			if(vsl != part.vessel || !all_passages_ready) return;
			update_state(); 
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			entrance = part.GetPassage();
			if(entrance == null) return;
			passage_checklist = part.AllModulesOfType<HangarPassage>();
			resource = this.GetResourceDef(OutputResource);
			if(resource == null) return;
			pump = new ResourcePump(part, resource.id);
			StartCoroutine(delayed_update_state());
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			Title = "Mining";
		}
		#endregion

		#region Asteroid
		bool all_passages_ready { get { return passage_checklist.All(p => p.Ready); } }

		IEnumerator<YieldInstruction> delayed_update_state()
		{
			while(!all_passages_ready) yield return null;
			update_state();
		}

		void update_state()
		{
			try
			{
				//get asteroid hatch
				var hatch = entrance.ConnectedPartWithModule<SingleUseGrappleNode>();
				grapple_node = hatch.GetModule<SingleUseGrappleNode>();
				storage = hatch.GetModule<HangarStorageDynamic>();
				if(grapple_node == null || storage == null) throw new Exception();
				//get asteroid
				asteroid = hatch.AttachedPartWithModule<ModuleAsteroid>();
				asteroid_info = asteroid.GetModule<AsteroidInfo>();
				if(!asteroid_info.AsteroidIsUsable) 
				{
					ScreenMessager.showMessage(6, "This asteroid is used by Asteroid Recycling machinery.\n" +
						"Mining it is prohibited for safety reasons.");
					throw new Exception();
				}
			}
			catch
			{ 
				asteroid = null;
				asteroid_info = null;
				grapple_node = null;
				storage = null;
				dM_buffer = 0;
				if(pump != null)
					pump.Clear();
			}
			Converting &= can_convert();
			update_events();
		}
		#endregion

		#region Mining
		protected override bool can_convert(bool report = false)
		{
			if(!report)
				return asteroid != null
					&& asteroid_info != null
					&& storage != null
					&& grapple_node != null 
					&& grapple_node.Fixed;
			if(storage == null)
			{
				ScreenMessager.showMessage("The mining can only be performed " +
					"through an access port with Dynamic Storage (TM) capabilities.");
				return false;
			}
			if(grapple_node == null || !grapple_node.Fixed)
			{
				ScreenMessager.showMessage("The mining can only be performed " +
					"through a permanentely fixed acces port.");
				return false;
			}
			if(asteroid == null)
			{
				ScreenMessager.showMessage("No asteroid to mine");
				return false;
			}
			if(asteroid_info == null)
			{
				ScreenMessager.showMessage("Asteroid does not contain AsteroidInfo module.\n" +
					"This should never happen!");
				return false;
			}
			if(!storage.CanAddVolume)
			{
				ScreenMessager.showMessage("The space inside the asteroid is already in use. " +
					"Cannot start mining.");
				return false;
			}
			return true;
		}

		//ode to the imprecision of floating point calculations
		bool produce(float consumed)
		{
			//check if it is possible to get so little from the asteroid
			dM_buffer -= Mathf.Min(consumed*ConversionRate, asteroid.mass-asteroid_info.MinMass);
			var new_mass = asteroid.mass+dM_buffer;
			if(asteroid.mass == new_mass) return true;
			//if it seems possible, try to produce the resource
			pump.RequestTransfer(dM_buffer/resource.density*Efficiency);
			dM_buffer = 0;
			if(!pump.TransferResource()) return true;
			var dM = pump.Result*resource.density/Efficiency;
			//if the transfer was partial and what was produced is still less 
			//then required to change asteroid mass, revert
			new_mass = asteroid.mass+dM;
			if(asteroid.mass == new_mass)
			{ 
				ScreenMessager.showMessage("No space left for {0}", OutputResource);
				goto abort;
			}
			//if the storage cannot accept new volume, also revert
			if(!storage.AddVolume(-dM/asteroid_info.Density))
			{
				ScreenMessager.showMessage("Mining was aborted");
				goto abort;
			}
			asteroid.mass = new_mass;
			return true;
			abort:
			{
				pump.Revert();
				pump.Clear();
				return false;
			}
		}

		protected override bool convert()
		{
			//consume energy, udpate conversion rate
			if(!consume_energy()) return true;
			//check asteroid first
			if(asteroid.mass <= asteroid_info.MinMass)
			{
				ScreenMessager.showMessage("Asteroid is depleted");
				dM_buffer = 0; pump.Clear();
				return false;
			}
			//try to produce resource
			if(!ShuttingOff && Rate >= MinimumRate) 
				ShuttingOff = !produce(Rate * CurrentEnergyDemand * TimeWarp.fixedDeltaTime);
			return above_threshold;
		}

		protected override void on_start_conversion()
		{ asteroid_info.LockAsteroid(); }

		protected override void on_stop_conversion()
		{ storage.Setup(); }
		#endregion

//		#region BackgroundProcessing
//		public static int GetBackgroundResourceCount()
//
//		public static void GetBackgroundResource(int index, out string resourceName, out float resourceRate)
//		#endregion
	}
}

