using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class AsteroidMassConverter : AnimatedConverterBase
	{
		const string USI_PotatoInfoName = "USI_PotatoInfo";

		[KSPField] public string OutputResource;
		[KSPField] public float  Efficiency = 0.92f; // 8% of mass is lost
		[KSPField] public float  ConversionRate = 0.01f; // tons per electric charge
		[KSPField] public float  RateThreshold = 0.1f; // relative rate threshold

		[KSPField(guiActive = true, guiName = "Mining Rate", guiFormat = "n1", guiUnits = "%")]
		public float RateDisplay;
		float rate, last_rate;

		ResourcePump pump;
		float dM_buffer;

		#region Parts & Modules
		Part asteroid;
		AsteroidInfo asteroid_info;
		SingleUseGrappleNode grapple_node;
		HangarStorageDynamic storage;
		PartResourceDefinition resource;
		#endregion

		#region Setup
		public override string GetInfo()
		{
			var info = base.GetInfo();
			var mass_flow = ConversionRate*EnergyConsumption;
			info += string.Format("Mass Conversion: {0}/sec\n", Utils.formatMass(mass_flow));
			resource = PartResourceLibrary.Instance.GetDefinition(OutputResource);
			if(resource != null)
				info += string.Format("Produces {0}: {1}/sec", 
					OutputResource, mass_flow*Efficiency/resource.density);
			return info;
		}

		public override void OnAwake()
		{
			base.OnAwake();
			GameEvents.onVesselWasModified.Add(update_state);
		}

		void OnDestroy() 
		{ GameEvents.onVesselWasModified.Remove(update_state); }

		void update_state(Vessel vsl)
		{ if(vsl == part.vessel) update_state(); }

		IEnumerator<YieldInstruction> slow_update()
		{
			while(true)
			{
				if(rate != last_rate)
				{
					RateDisplay = rate*100f;
					if(emitter != null)
					{
						emitter.minEmission = (int)Mathf.Ceil(base_emission[0]*rate);
						emitter.maxEmission = (int)Mathf.Ceil(base_emission[1]*rate);
					}
					last_rate = rate;
				}
				yield return new WaitForSeconds(0.5f);
			}
		}

		public override void OnStart(StartState state)
		{
			StartEventGUIName = "Start Mining";
			StopEventGUIName = "Stop Mining";
			ActionGUIName = "Toggle Mining";
			base.OnStart(state);
			resource = this.GetResourceDef(OutputResource);
			if(resource == null) return;
			pump = new ResourcePump(part, resource.id);
			update_state();
			StartCoroutine(slow_update());
		}
		#endregion

		#region Asteroid
		void update_state()
		{
			try
			{
				//get asteroid
				asteroid = vessel.GetPart<ModuleAsteroid>();
				if(!asteroid_is_usable) throw new Exception();
				asteroid_info = asteroid.GetModule<AsteroidInfo>();
				if(asteroid_info == null) throw new Exception();
				//get asteroid hatch
				var hatch = vessel.GetPart<SingleUseGrappleNode>();
				grapple_node = hatch.GetModule<SingleUseGrappleNode>();
				storage = hatch.GetModule<HangarStorageDynamic>();
				if(grapple_node == null || storage == null) throw new Exception();
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

		bool asteroid_is_usable
		{
			get
			{
				if(asteroid == null) return false;
				try
				{
					var USI_PotatoInfo = asteroid.Modules[USI_PotatoInfoName];
					var explored = USI_PotatoInfo.Fields["Explored"];
					if((bool)explored.host) 
					{
						ScreenMessager.showMessage(6, "This asteroid is used by Asteroid Recycling machinery.\n" +
							"Mining it is prohibited for safety reasons.");
						return false;
					}
				} catch {}
				return true;
			}
		}

		void lock_asteroid()
		{
			if(asteroid == null) return;
			try { asteroid.Modules.Remove(asteroid.Modules[USI_PotatoInfoName]); }
			catch {}
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
			if(storage.VesselsDocked > 0)
			{
				ScreenMessager.showMessage("The space inside the asteroid is already in use. " +
					"Cannot start mining.");
				return false;
			}
			return true;
		}

		bool produce(float consumed)
		{
			//check if it is possible to get so little from the asteroid
			dM_buffer -= Mathf.Min(consumed*ConversionRate, asteroid.mass-asteroid_info.MinMass);
			var new_mass = asteroid.mass+dM_buffer;
			if(asteroid.mass == new_mass) return true;
			//if it seems possible, try to produce resource
			pump.RequestTransfer(dM_buffer/resource.density*Efficiency);
			dM_buffer = 0;
			if(!pump.TransferResource()) return true;
			var dM = pump.Result*resource.density/Efficiency;
			new_mass = asteroid.mass+dM;
			//if what produced is still less then is required to change asteroid mass, revert
			if(asteroid.mass == new_mass) 
			{ pump.Revert(); return false; }
			storage.AddVolume(-dM/asteroid_info.Density);
			asteroid.mass = new_mass;
			return true;
		}

		protected override bool convert()
		{
			//get energy
			socket.RequestTransfer(EnergyConsumption*TimeWarp.fixedDeltaTime);
			if(!socket.TransferResource()) return true;
			rate = socket.Ratio;
			if(rate < RateThreshold) 
			{
				ScreenMessager.showMessage("Not enough energy");
				return false;
			}
			//try to produce resource
			var produced = produce(socket.Result);
			//check results
			if(asteroid.mass <= asteroid_info.MinMass)
			{
				ScreenMessager.showMessage("Asteroid is depleted");
				dM_buffer = 0; pump.Clear();
				return false;
			}
			if(!produced)
			{
				ScreenMessager.showMessage("No space left for {0}", OutputResource);
				return false;
			}
			return true;
		}

		protected override void on_start_conversion()
		{ lock_asteroid(); }

		protected override void on_stop_conversion()
		{
			RateDisplay = 0;
			storage.UpdateMetric();
		}
		#endregion
	}
}

