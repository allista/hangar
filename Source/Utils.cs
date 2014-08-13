// This code is based on Procedural Fairings plug-in by Alexey Volynskov, PMUtils class
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSPAPIExtensions;
using System.Runtime.InteropServices;


namespace AtHangar
{
	public static class Utils
	{
		#region Techtree
		static bool haveTech (string name)
		{
			if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER)
				return name == "sandbox";
			return ResearchAndDevelopment.GetTechnologyState (name) == RDTech.State.Available;
		}
		
		public static float getTechMinValue(string cfgname, float defVal)
		{
			float minVal  = 0;
			bool hasValue = false;
			foreach(var tech in GameDatabase.Instance.GetConfigNodes(cfgname))
				foreach(ConfigNode.Value value in tech.values) 
				{
					if(!haveTech(value.name)) continue;
					float v = float.Parse(value.value);
					if (!hasValue || v < minVal) 
					{
						minVal = v;
						hasValue = true;
					}
				}
			return hasValue ? minVal : defVal;
		}
		
		public static float getTechMaxValue(string cfgname, float defVal)
		{
			float maxVal = 0;
			bool hasValue = false;
			foreach(var tech in GameDatabase.Instance.GetConfigNodes(cfgname))
				foreach(ConfigNode.Value value in tech.values)
				{
					if(!haveTech(value.name)) continue;
					float v = float.Parse(value.value);
					if(!hasValue || v > maxVal)
					{
						maxVal = v;
						hasValue = true;
					}
				}
			return hasValue? maxVal : defVal;
		}
		
		public static void setFieldRange(BaseField field, float minval, float maxval)
		{
			var fr = field.uiControlEditor as UI_FloatRange;
			if (fr != null) {
				fr.minValue = minval;
				fr.maxValue = maxval;
			}
		
			var fe = field.uiControlEditor as UI_FloatEdit;
			if (fe != null) {
				fe.minValue = minval;
				fe.maxValue = maxval;
			}
		}
		#endregion

		#region ControlLock
		public static void LockIfMouseOver(string LockName, Rect WindowRect, bool Lock)
		//taken from Kerbal Alarm Clock mod
		{
			if(Lock && WindowRect.Contains(Event.current.mousePosition))
			{
				if(InputLockManager.GetControlLock(LockName) != ControlTypes.EDITOR_LOCK)
				{
					#if DEBUG
					Log("AddingLock: {0}", LockName);
					#endif
					InputLockManager.SetControlLock(ControlTypes.EDITOR_LOCK, LockName);
				}
			}
			else
			{
				if (InputLockManager.GetControlLock(LockName) == ControlTypes.EDITOR_LOCK) 
				{
					#if DEBUG
					Log("RemovingLock: {0}", LockName);
					#endif
					InputLockManager.RemoveControlLock(LockName);
				}
			}
		}
		#endregion
		
		#region Formatting
		public static string formatMass (float mass)
		{
			if(mass < 0.01f)
				return (mass * 1e3f).ToString("n3") + "kg";
			return mass.ToString("n3") + "t";
		}
		
		public static string formatVolume (double volume)
		{
			if(volume < 0.1f)
				return (volume * 1e3f).ToString ("n0") + " L";
			return volume.ToString ("n1") + "m^3";
		}
		
		public static string formatDimensions(Vector3 size)
		{ return string.Format("{0:F1}m x {1:F1}m x {2:F1}m", size.x, size.y, size.z); }
		
		
		//sound (from the KAS mod; KAS_Shared class)
		public static bool createFXSound(Part part, FXGroup group, string sndPath, bool loop, float maxDistance = 30f)
        {
            group.audio = part.gameObject.AddComponent<AudioSource>();
            group.audio.volume = GameSettings.SHIP_VOLUME;
            group.audio.rolloffMode = AudioRolloffMode.Linear;
            group.audio.dopplerLevel = 0f;
            group.audio.panLevel = 1f;
            group.audio.maxDistance = maxDistance;
            group.audio.loop = loop;
            group.audio.playOnAwake = false;
            if (GameDatabase.Instance.ExistsAudioClip(sndPath))
            {
                group.audio.clip = GameDatabase.Instance.GetAudioClip(sndPath);
                return true;
            }
            ScreenMessages.PostScreenMessage("Sound file : " + sndPath + " as not been found, please check your Hangar installation !", 10, ScreenMessageStyle.UPPER_CENTER);
            return false;
        }

		public static bool HasLaunchClamp(IEnumerable<Part> parts)
		{
			foreach(Part p in parts)
			{ if(p.HasModule<LaunchClamp>()) return true; }
			return false;
		}

		public static void UpdateEditorGUI()
		{ if(EditorLogic.fetch != null)	GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship); }
		
		#region Debug
		public static void Log(string msg, params object[] args)
		{ 
			for(int i = 0; i < args.Length; i++) 
			{
				if(args[i] is Vector3) args[i] = formatVector((Vector3)args[i]);
				else if(args[i] is Vector3d) args[i] = formatVector((Vector3d)args[i]);
			}
			Debug.Log(string.Format("[Hangar] "+msg, args)); 
		}

		public static void logStamp(string msg = "") { Debug.Log("[Hangar] === " + msg); }

		#if DEBUG
		public static void logCrewList(List<ProtoCrewMember> crew)
		{
			string crew_str = "";
			foreach(ProtoCrewMember c in crew)
			{
				if(crew_str != "") crew_str += "\n";
				crew_str += string.Format("{0}, seat {1}, seatIdx {2}, roster {3}, ref {4}", c.name, c.seat, c.seatIdx, c.rosterStatus, c.KerbalRef);
			}
			Debug.Log(crew_str);
		}
		
		public static string formatVector(Vector3 v)
		{ return string.Format("({0}, {1}, {2}); |v| = {3}", v.x, v.y, v.z, v.magnitude); }

		public static string formatVector(Vector3d v)
		{ return string.Format("({0}, {1}, {2}); |v| = {3}", v.x, v.y, v.z, v.magnitude); }

		public static void logVector(Vector3 v) { Debug.Log(formatVector(v)); }
		public static void logVector(Vector3d v) { Debug.Log(formatVector(v)); }

		public static void logVectors(IEnumerable<Vector3> vecs)
		{ foreach(Vector3 v in vecs) Debug.Log(formatVector(v)); }

		public static Vector3d planetaryPosition(Vector3 v, CelestialBody planet) 
		{ 
			double lng = planet.GetLongitude(v);
			double lat = planet.GetLatitude(v);
			double alt = planet.GetAltitude(v);
			return planet.GetWorldSurfacePosition(lat, lng, alt);
		}

		public static void logPlanetaryPosition(Vector3 v, CelestialBody planet) 
		{ 
			double lng = planet.GetLongitude(v);
			double lat = planet.GetLatitude(v);
			double alt = planet.GetAltitude(v);
			Debug.Log(formatVector(planet.GetWorldSurfacePosition(lat, lng, alt)));
		}

		public static void logLongLatAlt(Vector3 v, CelestialBody planet) 
		{ 
			double lng = planet.GetLongitude(v);
			double lat = planet.GetLatitude(v);
			double alt = planet.GetAltitude(v);
			Debug.Log(string.Format("Long: {0}, Lat: {1}, Alt: {2}", lng, lat, alt));
		}
		
		public static void logBounds(Bounds b)
		{
			Debug.Log(string.Format("Center:  {0}", formatVector(b.center)));
			Debug.Log(string.Format("Extents: {0}", formatVector(b.extents)));
			Debug.Log(string.Format("Bounds:\n{0}\n{1}", formatVector(b.center+b.extents), formatVector(b.center-b.extents)));
		}
		
		public static void logProtovesselCrew(ProtoVessel pv)
		{
			for(int i = 0; i < pv.protoPartSnapshots.Count; i++)
			{
				ProtoPartSnapshot p = pv.protoPartSnapshots[i];
				Debug.Log(string.Format("Part{0}: {1}", i, p.partName));
				if(p.partInfo.partPrefab != null)
					Debug.Log(string.Format("partInfo.partPrefab.CrewCapacity {0}",p.partInfo.partPrefab.CrewCapacity));
				Debug.Log(string.Format("partInfo.internalConfig: {0}", p.partInfo.internalConfig));
				Debug.Log(string.Format("partStateValues.Count: {0}", p.partStateValues.Count));
				foreach(string k in p.partStateValues.Keys)
					Debug.Log (string.Format("{0} : {1}", k, p.partStateValues[k]));
				Debug.Log(string.Format("modules.Count: {0}", p.modules.Count));
				foreach(ProtoPartModuleSnapshot pm in p.modules)
					Debug.Log (string.Format("{0} : {1}", pm.moduleName, pm.moduleValues));
				foreach(string k in p.partStateValues.Keys)
					Debug.Log (string.Format("{0} : {1}", k, p.partStateValues[k]));
				Debug.Log(string.Format("customPartData: {0}", p.customPartData));
			}
		}
		#endif
		#endregion
		#endregion
	}


	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class ScreenMessager : MonoBehaviour
	{
		static float osdMessageTime  = 0;
		static string osdMessageText = null;

		public static void showMessage(string msg, float delay)
		{
			#if DEBUG
			Utils.Log(msg);
			#endif
			osdMessageText = msg;
			osdMessageTime = Time.time + delay;
		}

		public void OnGUI ()
		{
			if (Time.time < osdMessageTime) 
			{
				GUI.skin = HighLogic.Skin;
				GUIStyle style = new GUIStyle ("Label");
				style.alignment = TextAnchor.MiddleCenter;
				style.fontSize = 20;
				style.normal.textColor = Color.black;
				GUI.Label (new Rect (2, 2 + (Screen.height / 9), Screen.width, 50), osdMessageText, style);
				style.normal.textColor = Color.yellow;
				GUI.Label (new Rect (0, Screen.height / 9, Screen.width, 50), osdMessageText, style);
			}
		}
	}


	public static class PartExtension
	{
		#region from MechJeb2 PartExtensions
		public static bool HasModule<T>(this Part p) where T : PartModule
		{ return p.Modules.OfType<T>().Any(); }

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

		public static float DryCost(this Part p) { return p.TotalCost() - p.ResourcesCost(); }

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

