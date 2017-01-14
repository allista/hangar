//   AsteroidMassConverter.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
	public class AsteroidMassConverter : ModuleAsteroidDrill
	{
		#region Parts & Modules
		HangarPassage entrance;
		List<HangarPassage> passage_checklist;
		SingleUseGrappleNode grapple_node;
		HangarStorageDynamic storage;
		ModuleAsteroid asteroid;
		ModuleAsteroidInfo asteroid_info;
		#endregion

		double lastMass;
		bool WasActivated;

		public override void OnAwake()
		{
			base.OnAwake();
			GameEvents.onVesselWasModified.Add(update_state);
		}

		public void OnDestroy() 
		{ 
			GameEvents.onVesselWasModified.Remove(update_state); 
		}

		void update_state(Vessel vsl)
		{ 
			if(vsl == null || vsl != vessel || !all_passages_ready) return;
			update_state(); 
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			entrance = part.GetPassage();
			if(entrance == null) return;
			passage_checklist = part.AllModulesOfType<HangarPassage>();
			StartCoroutine(delayed_update_state());
		}

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
				grapple_node = hatch.Modules.GetModule<SingleUseGrappleNode>();
				storage = hatch.Modules.GetModule<HangarStorageDynamic>();
				if(grapple_node == null || storage == null) throw new Exception();
				//get asteroid
				asteroid = hatch.GetModuleInAttachedPart<ModuleAsteroid>();
				asteroid_info = asteroid.part.Modules.GetModule<ModuleAsteroidInfo>();
				EnableModule();
			}
			catch
			{ 
				asteroid = null;
				asteroid_info = null;
				grapple_node = null;
				storage = null;
				DisableModule();
			}
			IsActivated &= can_convert();
		}

		protected virtual bool can_convert()
		{
			if(storage == null)
			{
				status = "Incompatible hatch";
				return false;
			}
			if(grapple_node == null || !grapple_node.Fixed)
			{
				status = "Hatch is not fixed";
				return false;
			}
			if(asteroid == null || asteroid_info == null)
			{
				status = "No asteroid to mine";
				return false;
			}
			if(!storage.CanAddVolume)
			{
				status = "Asteroid is in use";
				return false;
			}
			return true;
		}

		protected override ConversionRecipe PrepareRecipe(double deltaTime)
		{
			ConversionRecipe recipe = null;
			IsActivated &= can_convert();
			if(IsActivated)
			{
				WasActivated = true;
				lastMass = asteroid_info.currentMassVal;
				recipe = base.PrepareRecipe(deltaTime);
				IsActivated &= recipe != null && recipe.Outputs.Count != 0;
			}
			return recipe;
		}

		protected override void PostProcess(ConverterResults result, double deltaTime)
		{
			base.PostProcess(result, deltaTime);
			var dM = lastMass-asteroid_info.currentMassVal;
			if(dM > 1e-5f)
			{
				storage.AddVolume((float)(dM/asteroid.density));
				lastMass = asteroid_info.currentMassVal;
			}
		}

		protected override void PostUpdateCleanup()
		{
			base.PostUpdateCleanup();
			if(WasActivated && !IsActivated) 
			{
				storage.Setup();
				WasActivated = false;
			}
		}
	}
}

