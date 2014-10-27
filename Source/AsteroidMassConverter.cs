using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class AsteroidMassConverter : PartModule
	{
		[KSPField] public string OutputResource;
		[KSPField] public float  Efficiency = 0.92f; // 3%
		[KSPField] public float  ConversionRate = 0.01f; // tons per electric charge
		[KSPField] public float  EnergyConsumption = 50f; // electric charge per second
		[KSPField] public float  RateThreshold = 0.1f; // relative rate threshold

		[KSPField(isPersistant = true)] public bool Converting;
		float dM_buffer;

		[KSPField] public string AnimatorID = "_none_";
		BaseHangarAnimator animator;

		[KSPField(guiActive = true, guiName = "Mining Rate", guiFormat = "n1", guiUnits = "%")]
		public float RelativeRate;

		#region Parts & Modules
		Part asteroid;
		AsteroidInfo asteroid_info;
		SingleUseGrappleNode grapple_node;
		HangarStorageDynamic storage;
		PartResourceDefinition resource;
		KSPParticleEmitter emitter;
		#endregion


		#region Setup
		public override void OnAwake()
		{
			base.OnAwake();
			GameEvents.onVesselWasModified.Add(update_state);
		}

		void OnDestroy() 
		{ GameEvents.onVesselWasModified.Remove(update_state); }

		void update_state(Vessel vsl)
		{ if(vsl == part.vessel) update_state(); }

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			resource = PartResourceLibrary.Instance.GetDefinition(OutputResource);
			if(resource == null)
			{
				this.Log("WARNING: no '{0}' resource in the library. Part config is INVALID.", OutputResource);
				enabled = isEnabled = false;
				return;
			}
			//initialize Animator
			part.force_activate();
			emitter = part.GetComponentsInChildren<KSPParticleEmitter>().FirstOrDefault();
			animator = part.Modules.OfType<BaseHangarAnimator>().FirstOrDefault(m => m.AnimatorID == AnimatorID);
			if(animator == null)
			{
				this.Log("Using BaseHangarAnimator");
				animator = new BaseHangarAnimator();
			}
			update_state();
		}
		#endregion

		#region Asteroid
		void update_state()
		{
			try
			{
				//get asteroid
				asteroid = vessel.GetPart<ModuleAsteroid>();
				if(!asteroid_is_usable(asteroid)) throw new Exception();
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
			}
			Converting &= can_convert();
			update_events();

		}

		bool asteroid_is_usable(Part ast)
		{
			if(asteroid == null) return false;
			try
			{
				var USI_PotatoInfo = ast.Modules["USI_PotatoInfo"];
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
		#endregion

		#region Mining
		bool can_convert(bool report = false)
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
			//if it seems possible, produce resource
			var request = dM_buffer/resource.density*Efficiency;
			var produced = part.RequestResource(resource.id, request);
			var dM = produced*resource.density/Efficiency;
			new_mass = asteroid.mass+dM;
			//if produced is less then is required to change asteroid mass, revert
			if(asteroid.mass == new_mass) 
			{
				part.RequestResource(resource.id, -produced);
				return false;
			}
			storage.AddVolume(-dM/asteroid_info.Density);
			asteroid.mass = new_mass;
			dM_buffer = 0;
			return true;
		}

		bool convert_asteroid_mass()
		{
			//get energy
			var request  = EnergyConsumption*TimeWarp.fixedDeltaTime;
			var consumed = part.RequestResource(Utils.ElectricChargeID, request);
			var rate = consumed/request; RelativeRate = rate*100f;
			if(rate < RateThreshold) 
			{
				ScreenMessager.showMessage("Not enough energy");
				return false;
			}
			//try to produce resource
			var produced = produce(consumed);
			//check results
			if(asteroid.mass <= asteroid_info.MinMass)
			{
				ScreenMessager.showMessage("Asteroid is depleted");
				dM_buffer = 0;
				return false;
			}
			if(!produced)
			{
				ScreenMessager.showMessage("No space left for {0}", OutputResource);
				return false;
			}
			return true;
		}

		public void FixedUpdate()
		{ if(Converting && !convert_asteroid_mass()) StopConversion(); }
		#endregion

		#region Events & Actions
		void update_events()
		{
			Events["StartConversion"].active = !Converting;
			Events["StopConversion"].active  =  Converting;
			if(emitter != null)
			{
				emitter.emit = Converting;
				emitter.enabled = Converting;
			}
			if(Converting) animator.Open();
			else animator.Close();
		}

		[KSPEvent (guiActive = true, guiName = "Start Mining", active = true)]
		public void StartConversion()
		{
			if(!can_convert(true)) return;
			Converting = true;
			animator.Open();
			update_events();
		}

		[KSPEvent (guiActive = true, guiName = "Stop Mining", active = true)]
		public void StopConversion()
		{

			Converting = false;
			RelativeRate = 0;
			storage.UpdateMetric();
			animator.Close();
			update_events();
		}

		[KSPAction("Toggle Mining")]
		public void ToggleConversionAction(KSPActionParam param)
		{
			if(Converting) StopConversion();
			else StartConversion();
		}
		#endregion
	}
}

