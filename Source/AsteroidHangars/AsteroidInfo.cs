using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class AsteroidInfo : PartModule
	{
		const string USI_PotatoInfoName = "USI_PotatoInfo";
		const float min_mass_ratio = 0.3f;

		[KSPField(isPersistant = true)] public float OrigMass = -1f;
		[KSPField(isPersistant = true)] public float CurMass  = -1f;
		[KSPField(isPersistant = true)] public float MinMass  = -1f;
		[KSPField(isPersistant = true)] public float Density  = -1f;
		[KSPField(isPersistant = true)] public bool  Locked;

		ModuleAsteroid asteroid;
		float orig_crash_tolerance;

		IEnumerator<YieldInstruction> init_params()
		{
			if(asteroid != null && OrigMass < 0)
			{
				while(asteroid.prefabBaseURL == string.Empty) 
					yield return new WaitForSeconds(0.5f);
				OrigMass = part.mass;
				CurMass  = OrigMass;
				MinMass  = OrigMass * min_mass_ratio;
				Density  = asteroid.density;
				this.Log("Set OrigMass: {0}", OrigMass);//debug
			}
			else if(CurMass < 0) CurMass = part.mass;
			else part.mass = CurMass;
			yield return StartCoroutine(slow_update());
		}

		IEnumerator<YieldInstruction> slow_update()
		{
			while(true)
			{
				CurMass = part.mass;
				part.crashTolerance = orig_crash_tolerance * part.mass/OrigMass;
				yield return new WaitForSeconds(0.5f);
			}
		}

		public void LockAsteroid()
		{
			Locked = true;
			try { part.Modules.Remove(part.Modules[USI_PotatoInfoName]); }
			catch {}
		}

		public bool AsteroidIsUsable
		{
			get
			{
				try
				{
					var USI_PotatoInfo = part.Modules[USI_PotatoInfoName];
					var explored = USI_PotatoInfo.Fields["Explored"];
					return !(bool)explored.host;
				} catch {}
				return true;
			}
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			part.fuelCrossFeed = false; //fuel? cross feed? through an asteroid?! >_<
			orig_crash_tolerance = part.crashTolerance;
			asteroid = part.GetModule<ModuleAsteroid>();
			if(Locked) LockAsteroid();
			StartCoroutine(init_params());
		}

		public override void OnSave(ConfigNode node)
		{
			CurMass = part.mass;
			base.OnSave(node);
		}
	}
}
