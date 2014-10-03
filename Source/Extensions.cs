using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

namespace AtHangar
{
	public static class CollectionsExtension
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
		{ foreach(TSource e in a) action(e); }

		public static TSource Pop<TSource>(this LinkedList<TSource> l)
		{
			TSource e = l.Last.Value;
			l.RemoveLast();
			return e;
		}
	}

	public static class PartExtension
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

		public static float TotalCost(this Part p) { return p.partInfo.cost; }

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

		public static Part RootPart(this Part p) 
		{ return p.parent == null ? p : p.parent.RootPart(); }

		public static List<Part> AllChildren(this Part p)
		{
			List<Part> all_children = new List<Part>{};
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
			List<Part> all_parts = new List<Part>{p};
			all_parts.AddRange(p.AllChildren());
			return all_parts;
		}

		public static void BreakConnectedStruts(this Part p)
		{
			//break strut connectors
			foreach(Part part in p.AllConnectedParts())
			{
				StrutConnector s = part as StrutConnector;
				if(s == null || s.target == null) continue;
				if(s.parent == p || s.target == p)
				{
					s.BreakJoint();
					s.targetAnchor.gameObject.SetActive(false);
					s.direction = Vector3.zero;
				}
			}
		}

		public static WheelUpdater AddWheelUpdater(this Part p, uint trigger_id = default(uint))
		{
			Utils.Log("Trying to add WheelUpdater to {0}, {1}", p.name, p.flightID);//debug
			WheelUpdater updater = (p.Modules.OfType<WheelUpdater>().FirstOrDefault() ?? 
				p.AddModule("WheelUpdater") as WheelUpdater);
			if(updater == null) return null;
			if(trigger_id != default(uint))
				updater.RegisterTrigger(trigger_id);
			return updater;
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
	}

	public class ModuleGUIState
	{
		readonly public List<string> EditorFields    = new List<string>();
		readonly public List<string> GUIFields       = new List<string>();
		readonly public List<string> InactiveEvents  = new List<string>();
		readonly public List<string> InactiveActions = new List<string>();
	}

	public static class PartModuleExtension
	{
		public static ModuleGUIState SaveGUIState(this PartModule pm)
		{
			ModuleGUIState state = new ModuleGUIState();
			foreach(BaseField f in pm.Fields)
			{
				if(f.guiActive) state.GUIFields.Add(f.name);
				if(f.guiActiveEditor) state.EditorFields.Add(f.name);
			}
			foreach(BaseEvent e in pm.Events)
				if(!e.active) state.InactiveEvents.Add(e.name);
			foreach(BaseAction a in pm.Actions)
				if(!a.active) state.InactiveActions.Add(a.name);
			return state;
		}

		public static ModuleGUIState DeactivateGUI(this PartModule pm)
		{
			ModuleGUIState state = new ModuleGUIState();
			foreach(BaseField f in pm.Fields)
			{
				if(f.guiActive) state.GUIFields.Add(f.name);
				if(f.guiActiveEditor) state.EditorFields.Add(f.name);
				f.guiActive = f.guiActiveEditor = false;
			}
			foreach(BaseEvent e in pm.Events)
			{
				if(!e.active) state.InactiveEvents.Add(e.name);
				e.active = false;
			}
			foreach(BaseAction a in pm.Actions)
			{
				if(!a.active) state.InactiveActions.Add(a.name);
				a.active = false;
			}
			return state;
		}

		public static void ActivateGUI(this PartModule pm, ModuleGUIState state = null)
		{
			foreach(BaseField f in pm.Fields)
			{
				if(state.GUIFields.Contains(f.name)) f.guiActive = true;
				if(state.EditorFields.Contains(f.name)) f.guiActiveEditor = true;
			}
			foreach(BaseEvent e in pm.Events)
			{
				if(state.InactiveEvents.Contains(e.name)) continue;
				e.active = true;
			}
			foreach(BaseAction a in pm.Actions)
			{
				if(state.InactiveActions.Contains(a.name)) continue;
				a.active = true;
			}
		}
	}
}

