//   Utils.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

using UnityEngine;
using AT_Utils;

namespace AtHangar
{
	// This code is based on Procedural Fairings plug-in by Alexey Volynskov, PMUtils class
	static class HangarUtils
	{
		#region Misc
		//sound (from the KAS mod; KAS_Shared class)
		public static bool createFXSound(Part part, FXGroup group, string sndPath, bool loop, float maxDistance = 30f)
		{
			group.audio = part.gameObject.AddComponent<AudioSource>();
			group.audio.volume = GameSettings.SHIP_VOLUME;
			group.audio.rolloffMode = AudioRolloffMode.Logarithmic;
			group.audio.dopplerLevel = 0f;
			group.audio.maxDistance = maxDistance;
			group.audio.loop = loop;
			group.audio.playOnAwake = false;
			if(GameDatabase.Instance.ExistsAudioClip(sndPath))
			{
				group.audio.clip = GameDatabase.Instance.GetAudioClip(sndPath);
				return true;
			}
			Utils.Message(10, "Sound file : {0} has not been found, please check your Hangar installation", sndPath);
			return false;
		}

		public static bool HasLaunchClamp(IShipconstruct ship)
		{
			foreach(Part p in ship.Parts)
			{ if(p.HasModule<LaunchClamp>()) return true; }
			return false;
		}
		#endregion
	}
}
