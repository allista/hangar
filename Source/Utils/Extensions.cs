//   Extensions.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri

using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
	public static class PartExtensions
	{
		public static BaseHangarAnimator GetAnimator(this Part p, string ID)
		{
			var animator = p.Modules.OfType<BaseHangarAnimator>().FirstOrDefault(m => m.AnimatorID == ID);
			if(animator == null)
			{
				p.Log("Using BaseHangarAnimator");
				animator = new BaseHangarAnimator();
			}
			return animator;
		}

		public static HangarPassage GetPassage(this Part part)
		{
			var passage = part.GetModule<HangarPassage>();
			if(passage == null) 
				Utils.Message("WARNING: \"{0}\" part has no HangarPassage module.\n" +
					"The part configuration is INVALID!", part.Title());
			return passage;
		}

		public static ResourcePump CreateSocket(this Part p)
		{ return new ResourcePump(p, Utils.ElectricChargeID); }
	}


	public static class PartModuleExtensions
	{
		public static void ConfigurationInvalid(this PartModule pm, string msg, params object[] args)
		{
			Utils.Message(6, "WARNING: {0}.\n" +
			                           "Configuration of \"{1}\" is INVALID.", 
			                           string.Format(msg, args), 
			                           pm.Title());
			pm.enabled = pm.isEnabled = false;
			return;
		}

		public static PartResourceDefinition GetResourceDef(this PartModule pm, string name)
		{
			var res = PartResourceLibrary.Instance.GetDefinition(name);
			if(res == null) 
				pm.ConfigurationInvalid("no '{0}' resource in the library", name);
			return res;
		}
	}
}

