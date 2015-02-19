//   Extensions.cs
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
using System.Linq;
using System;
using UnityEngine;

namespace AtHangar
{
	public static class CollectionsExtensions
	{
		public static TSource SelectMax<TSource>(this IEnumerable<TSource> s, Func<TSource, float> metric)
		{
			float max_v = -1;
			TSource max_e = default(TSource);
			foreach(TSource e in s)
			{
				float m = metric(e);
				if(m > max_v) { max_v = m; max_e = e; }
			}
			return max_e;
		}

		public static void ForEach<TSource>(this TSource[] a, Action<TSource> action)
		{ for(int i = 0; i < a.Length; i++) action(a[i]); }

		public static TSource Pop<TSource>(this LinkedList<TSource> l)
		{
			TSource e = l.Last.Value;
			l.RemoveLast();
			return e;
		}

		public static TSource Min<TSource>(params TSource[] args) where TSource : IComparable
		{
			if(args.Length == 0) throw new InvalidOperationException("Min: arguments list should not be empty");
			TSource min = args[0];
			foreach(var arg in args)
			{ if(min.CompareTo(arg) < 0) min = arg; }
			return min;
		}

		public static TSource Max<TSource>(params TSource[] args) where TSource : IComparable
		{
			if(args.Length == 0) throw new InvalidOperationException("Max: arguments list should not be empty");
			TSource max = args[0];
			foreach(var arg in args)
			{ if(max.CompareTo(arg) > 0) max = arg; }
			return max;
		}

		public static TValue Next<TKey, TValue>(this SortedList<TKey,TValue> list, TKey key)
		{
			try
			{
				var i = list.IndexOfKey(key);
				var ni = (i+1) % list.Count;
				return list.Values[ni];
			} catch { return default(TValue); }
		}

		public static TValue Prev<TKey, TValue>(this SortedList<TKey,TValue> list, TKey key)
		{
			try
			{
				var i = list.IndexOfKey(key);
				var ni = i > 0? i-1 : list.Count-1;
				return list.Values[ni];
			} catch { return default(TValue); }
		}
	}

	public static class PartExtensions
	{
		#region from MechJeb2 PartExtensions
		public static bool HasModule<T>(this Part p) where T : PartModule
		{ return p.Modules.OfType<T>().Any(); }

		public static T GetModule<T>(this Part p) where T : PartModule
		{ return p.Modules.OfType<T>().FirstOrDefault(); }

		public static bool IsPhysicallySignificant(this Part p)
		{
			bool physicallySignificant = (p.physicalSignificance != Part.PhysicalSignificance.NONE);
			// part.PhysicsSignificance is not initialized in the Editor for all part. but physicallySignificant is useful there.
			if (HighLogic.LoadedSceneIsEditor)
				physicallySignificant = physicallySignificant && p.PhysicsSignificance != 1;
			//Landing gear set physicalSignificance = NONE when they enter the flight scene
			//Launch clamp mass should be ignored.
			physicallySignificant &= !p.HasModule<ModuleLandingGear>() && !p.HasModule<LaunchClamp>();
			return physicallySignificant;
		}

		public static float TotalMass(this Part p) { return p.mass+p.GetResourceMass(); }
		#endregion

		#region Find Modules or Parts
		public static Part RootPart(this Part p) 
		{ return p.parent == null ? p : p.parent.RootPart(); }

		public static List<Part> AllChildren(this Part p)
		{
			var all_children = new List<Part>{};
			foreach(Part ch in p.children) 
			{
				all_children.Add(ch);
				all_children.AddRange(ch.AllChildren());
			}
			return all_children;
		}

		public static List<Part> AllConnectedParts(this Part p)
		{
			if(p.parent != null) return p.parent.AllConnectedParts();
			var all_parts = new List<Part>{p};
			all_parts.AddRange(p.AllChildren());
			return all_parts;
		}

		public static Part AttachedPartWithModule<T>(this Part p) where T : PartModule
		{
			if(p.parent != null && p.parent.HasModule<T>()) return p.parent;
			foreach(var c in p.children) if(c.HasModule<T>()) return c;
			return null;
		}

		public static List<ModuleT> AllModulesOfType<ModuleT>(this Part part, ModuleT exception = null)
			where ModuleT : PartModule
		{
			var passages = new List<ModuleT>();
			foreach(Part p in part.AllConnectedParts())
				passages.AddRange(from m in p.Modules.OfType<ModuleT>()
					where exception == null || m != exception
					select m);
			return passages;
		}

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
				ScreenMessager.showMessage("WARNING: \"{0}\" part has no HangarPassage module.\n" +
					"The part configuration is INVALID!", part.Title());
			return passage;
		}
		#endregion

		#region Part dependant factories
		public static ResourcePump CreateSocket(this Part p)
		{ return new ResourcePump(p, Utils.ElectricChargeID); }
		#endregion

		#region Resources and Phys-Props
		public static float TotalCost(this Part p) { return p.partInfo != null? p.partInfo.cost : 0; }

		public static float ResourcesCost(this Part p) 
		{ 
			return (float)p.Resources.Cast<PartResource>()
				.Aggregate(0.0, (a, b) => a + b.amount * b.info.unitCost); 
		}

		public static float MaxResourcesCost(this Part p) 
		{ 
			return (float)p.Resources.Cast<PartResource>()
				.Aggregate(0.0, (a, b) => a + b.maxAmount * b.info.unitCost); 
		}

		public static float DryCost(this Part p) { return p.TotalCost() - p.MaxResourcesCost(); }

		public static float MassWithChildren(this Part p)
		{
			float mass = p.TotalMass();
			p.children.ForEach(ch => mass += ch.MassWithChildren());
			return mass;
		}
		#endregion

		#region Actions
		public static void BreakConnectedStruts(this Part p)
		{
			//break strut connectors
			foreach(Part part in p.AllConnectedParts())
			{
				var s = part as StrutConnector;
				if(s == null || s.target == null) continue;
				if(s.parent == p || s.target == p)
				{
					s.BreakJoint();
					s.targetAnchor.gameObject.SetActive(false);
					s.direction = Vector3.zero;
				}
			}
		}

		public static void UpdateAttachedPartPos(this Part p, AttachNode node)
		{
			if(node == null) return;
			var ap = node.attachedPart; 
			if(ap == null) return;
			var an = ap.findAttachNodeByPart(p);	
			if(an == null) return;
			var dp =
				p.transform.TransformPoint(node.position) -
				ap.transform.TransformPoint(an.position);
			if(ap == p.parent) 
			{
				while (ap.parent) ap = ap.parent;
				ap.transform.position += dp;
				p.transform.position -= dp;
			} 
			else ap.transform.position += dp;
		}
		#endregion

		#region Logging
		public static string Title(this Part p) { return p.partInfo != null? p.partInfo.title : p.name; }

		public static void Log(this Part p, string msg, params object[] args)
		{
			var vname = p.vessel == null? "" : p.vessel.vesselName;
			var _msg = string.Format("{0}.{1} [{2}]: {3}", 
				vname, p.name, p.flightID, msg);
			Utils.Log(_msg, args);
		}
		#endregion

		#region Misc
		//directly from Part disassembly
		public static PartModule.StartState StartState(this Part part)
		{
			var _state = PartModule.StartState.None;
			if(HighLogic.LoadedSceneIsEditor)
				_state |= PartModule.StartState.Editor;
			else if(HighLogic.LoadedSceneIsFlight)
			{
				if(part.vessel.situation == Vessel.Situations.PRELAUNCH)
				{
					_state |= PartModule.StartState.PreLaunch;
					_state |= PartModule.StartState.Landed;
				}
				if(part.vessel.situation == Vessel.Situations.DOCKED)
					_state |= PartModule.StartState.Docked;
				if(part.vessel.situation == Vessel.Situations.ORBITING ||
					part.vessel.situation == Vessel.Situations.ESCAPING)
					_state |= PartModule.StartState.Orbital;
				if(part.vessel.situation == Vessel.Situations.SUB_ORBITAL)
					_state |= PartModule.StartState.SubOrbital;
				if(part.vessel.situation == Vessel.Situations.SPLASHED)
					_state |= PartModule.StartState.Splashed;
				if(part.vessel.situation == Vessel.Situations.FLYING)
					_state |= PartModule.StartState.Flying;
				if(part.vessel.situation == Vessel.Situations.LANDED)
					_state |= PartModule.StartState.Landed;
			}
			return _state;
		}
		#endregion
	}

	public static class PartModuleExtensions
	{
		public static string Title(this PartModule pm) 
		{ return pm.part.partInfo != null? pm.part.partInfo.title : pm.part.name; }

		public static void EnableModule(this PartModule pm, bool enable)
		{ pm.enabled = pm.isEnabled = enable; }

		public static void Log(this PartModule pm, string msg, params object[] args)
		{
			var vname = pm.part.vessel == null? "" : pm.part.vessel.vesselName;
			var _msg = string.Format("{0}.{1}.{2} [{3}]: {4}", 
				vname, pm.part.name, pm.GetType().Name, pm.GetInstanceID(), msg);
			Utils.Log(_msg, args);
		}

		public static void ConfigurationInvalid(this PartModule pm, string msg, params object[] args)
		{
			ScreenMessager.showMessage(6, "WARNING: {0}.\n" +
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

	public static class VesselExtensions
	{
		public static Part GetPart<T>(this Vessel v) where T : PartModule
		{ return v.parts.FirstOrDefault(p => p.HasModule<T>()); }

		public static bool PartsStarted(this Vessel v)
		{ return v.parts.TrueForAll(p => p.started); }
	}
}

