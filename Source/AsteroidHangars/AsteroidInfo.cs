using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class AsteroidInfo : PartModule
	{
		const float min_mass_ratio = 0.3f;

		[KSPField(isPersistant = true)] public float OrigMass = -1f;
		[KSPField(isPersistant = true)] public float MinMass  = -1f;
		[KSPField(isPersistant = true)] public float Density  = -1f;

		ModuleAsteroid asteroid;
		float orig_crash_tolerance;

		IEnumerator<YieldInstruction> init_params()
		{
			if(asteroid == null || OrigMass > 0) yield break;
			while(asteroid.prefabBaseURL == string.Empty) 
				yield return new WaitForSeconds(0.5f);
			OrigMass = part.mass;
			MinMass  = OrigMass * min_mass_ratio;
			Density  = asteroid.density;
			yield return StartCoroutine(slow_update());
		}

		IEnumerator<YieldInstruction> slow_update()
		{
			while(true)
			{
				part.crashTolerance = orig_crash_tolerance * part.mass/OrigMass;
				yield return new WaitForSeconds(0.5f);
			}
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			orig_crash_tolerance = part.crashTolerance;
			asteroid = part.GetModule<ModuleAsteroid>();
			StartCoroutine(init_params());
		}
	}
}
