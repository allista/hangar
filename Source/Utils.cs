// This code is based on Procedural Fairings plug-in by Alexey Volynskov, PMUtils class
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSPAPIExtensions;

namespace AtHangar
{
	public class Triangle : IEnumerable<int>
	{
		readonly protected int i1, i2, i3;

		public Triangle(int i1, int i2, int i3) //indecies need to be clockwise
		{ this.i1 = i1; this.i2 = i2; this.i3 = i3; }

		public virtual IEnumerator<int> GetEnumerator()
		{
			yield return i1;
			yield return i2;
			yield return i3;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{ return GetEnumerator(); }
	}

	public class Quad : Triangle
	{
		readonly int i4;

		public Quad(int i1, int i2, int i3, int i4) //indecies need to be clockwise
			: base(i1, i2, i3) { this.i4 = i4; }

		public override IEnumerator<int> GetEnumerator ()
		{
			yield return i1;
			yield return i2;
			yield return i3;

			yield return i3;
			yield return i4;
			yield return i1;
		}
	}

	public class Basis
	{
		public readonly Vector3 x, y, z;
		public Basis(Vector3 x, Vector3 y, Vector3 z)
		{ this.x = x; this.y = y; this.z = z; }
	}

	public class State<T>
	{
		T _current, _old;

		public T current 
		{ 
			get	{ return _current; }
			set
			{
				_old = _current;
				_current = value;
			}
		}

		public T old { get { return _old; } }

		public State(T cur, T old = default(T))
		{
			_current = cur;
			_old = EqualityComparer<T>.Default.Equals(old, default(T)) ? cur : old;
		}

		public static implicit operator T(State<T> s) { return s._current; }
	}

	public static class Utils
	{
		#region Techtree
		public static readonly string minSizeName   = "HANGAR_MINSCALE";
		public static readonly string maxSizeName   = "HANGAR_MAXSCALE";
		public static readonly string minAspectName = "HANGAR_MINASPECT";
		public static readonly string maxAspectName = "HANGAR_MAXASPECT";

		static bool haveTech(string name)
		{
			if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER)
				return name == "sandbox";
			return ResearchAndDevelopment.GetTechnologyState(name) == RDTech.State.Available;
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
					if(!hasValue || v < minVal) 
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
			if (fr != null) 
			{
				fr.minValue = minval;
				fr.maxValue = maxval;
			}
		
			var fe = field.uiControlEditor as UI_FloatEdit;
			if (fe != null) 
			{
				fe.minValue = minval;
				fe.maxValue = maxval;
			}
		}
		#endregion

		#region ControlLock
		//modified from Kerbal Alarm Clock mod
		public static void LockEditor(string LockName, bool Lock=true)
		{
			if(Lock && InputLockManager.GetControlLock(LockName) != ControlTypes.EDITOR_LOCK)
			{
				#if DEBUG
				Log("AddingLock: {0}", LockName);
				#endif
				InputLockManager.SetControlLock(ControlTypes.EDITOR_LOCK, LockName);
				return;
			}
			if(!Lock && InputLockManager.GetControlLock(LockName) == ControlTypes.EDITOR_LOCK) 
			{
				#if DEBUG
				Log("RemovingLock: {0}", LockName);
				#endif
				InputLockManager.RemoveControlLock(LockName);
			}
		}

		public static void LockIfMouseOver(string LockName, Rect WindowRect, bool Lock=true)
		{
			Lock &= WindowRect.Contains(Event.current.mousePosition);
			LockEditor(LockName, Lock);
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
		{ return string.Format("{0:F2}m x {1:F2}m x {2:F2}m", size.x, size.y, size.z); }
		
		
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
		public static string formatVector(Vector3 v)
		{ return string.Format("({0}, {1}, {2}); |v| = {3}", v.x, v.y, v.z, v.magnitude); }

		public static string formatVector(Vector3d v)
		{ return string.Format("({0}, {1}, {2}); |v| = {3}", v.x, v.y, v.z, v.magnitude); }

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
				crew_str += string.Format("\n{0}, seat {1}, seatIdx {2}, roster {3}, ref {4}", 
					c.name, c.seat, c.seatIdx, c.rosterStatus, c.KerbalRef);
			Log("Crew List:{0}", crew_str);
		}
		
		public static void logVectors(IEnumerable<Vector3> vecs)
		{ 
			string vs = "";
			foreach(Vector3 v in vecs) vs += "\n"+formatVector(v);
			Log("Vectors:{0}", vs);
		}

		public static Vector3d planetaryPosition(Vector3 v, CelestialBody planet) 
		{ 
			double lng = planet.GetLongitude(v);
			double lat = planet.GetLatitude(v);
			double alt = planet.GetAltitude(v);
			return planet.GetWorldSurfacePosition(lat, lng, alt);
		}

		public static void logPlanetaryPosition(Vector3 v, CelestialBody planet) 
		{ Log("Planetary position: {0}", planetaryPosition(v, planet));	}

		public static void logLongLatAlt(Vector3 v, CelestialBody planet) 
		{ 
			double lng = planet.GetLongitude(v);
			double lat = planet.GetLatitude(v);
			double alt = planet.GetAltitude(v);
			Log("Long: {0}, Lat: {1}, Alt: {2}", lng, lat, alt);
		}
		
		public static void logBounds(Bounds b)
		{
			Log("Bounds:\n" +
				"Center:  {0}\n" +
				"Extents: {1}\n" +
				"Min:     {2}\n" +
				"Max:     {3}\n" +
				"=========", 
				b.center, b.extents, b.min, b.max);
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

		#region Graphics
		static Material _material;
		public static Material  material
		{
			get
			{
				if (_material == null)
					_material = new Material(Shader.Find("GUI/Text Shader"));
				return new Material(_material);
			}
		}

		public static void DrawMesh(Vector3[] edges, IEnumerable<int> tris, Transform t, Color c = default(Color))
		{
			//make a mesh
			Mesh m = new Mesh();
			m.vertices  = edges;
			m.triangles = tris.ToArray();
			//recalculate normals and bounds
			m.RecalculateBounds();
			m.RecalculateNormals();
			//make own material
			Material mat = Utils.material;
			mat.color = (c == default(Color))? Color.white : c;
			//draw mesh in the world space
			Graphics.DrawMesh(m, t.localToWorldMatrix, mat, 0);
		}

		//		edges[0] = new Vector3(min.x, min.y, min.z); //left-bottom-back
		//	    edges[1] = new Vector3(min.x, min.y, max.z); //left-bottom-front
		//	    edges[2] = new Vector3(min.x, max.y, min.z); //left-top-back
		//	    edges[3] = new Vector3(min.x, max.y, max.z); //left-top-front
		//	    edges[4] = new Vector3(max.x, min.y, min.z); //right-bottom-back
		//	    edges[5] = new Vector3(max.x, min.y, max.z); //right-bottom-front
		//	    edges[6] = new Vector3(max.x, max.y, min.z); //right-top-back
		//	    edges[7] = new Vector3(max.x, max.y, max.z); //right-top-front
		public static void DrawBounds(Bounds b, Transform T, Color c)
		{
			Vector3[] edges = Metric.BoundsEdges(b);
			List<int> tris = new List<int>();
			tris.AddRange(new Quad(0, 1, 3, 2));
			tris.AddRange(new Quad(0, 2, 6, 4));
			tris.AddRange(new Quad(0, 1, 5, 4));
			tris.AddRange(new Quad(1, 3, 7, 5));
			tris.AddRange(new Quad(2, 3, 7, 6));
			tris.AddRange(new Quad(6, 7, 5, 4));
			Utils.DrawMesh(edges, tris, T, c);
		}

		public static void DrawPoint(Vector3 point, Transform T, Color c = default(Color))
		{ DrawBounds(new Bounds(point, Vector3.one*0.1f), T, c); }

		public static void DrawArrow(Vector3 ori, Vector3 dir, Transform T, Color c = default(Color))
		{
			float l = dir.magnitude;
			float w = l*0.02f;
			w = w > 0.05f ? 0.05f : (w < 0.01f ? 0.01f : w);
			Vector3 x = Mathf.Abs(Vector3.Dot(dir.normalized,Vector3.up)) < 0.9f ? 
				Vector3.Cross(dir, Vector3.up).normalized : Vector3.Cross(Vector3.forward, dir).normalized;
			Vector3 y = Vector3.Cross(x, dir).normalized*w; x *= w;
			Vector3[] edges = new Vector3[5];
			edges[0] = ori+dir; 
			edges[1] = ori-x-y;
			edges[2] = ori-x+y;
			edges[3] = ori+x+y;
			edges[4] = ori+x-y;
			List<int> tris = new List<int>();
			tris.AddRange(new Quad(1, 2, 3, 4));
			tris.AddRange(new Triangle(0, 1, 2));
			tris.AddRange(new Triangle(0, 2, 3));
			tris.AddRange(new Triangle(0, 3, 4));
			tris.AddRange(new Triangle(0, 4, 1));
			Utils.DrawMesh(edges, tris, T, c);
		}

		public static void DrawYZ(Metric M, Transform T)
		{
			Utils.DrawArrow(Vector3.zero, Vector3.up*M.extents.y*0.8f, T, Color.green);
			Utils.DrawArrow(Vector3.zero, Vector3.forward*M.extents.z*0.8f, T, Color.blue);
		}
		#endregion
	}


	/// <summary>
	/// Screen messager is an addon that displays on-screen 
	/// messages in the top-center of the screen.
	/// It is a part of the Hangar module.
	/// </summary>
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
				GUIStyle style = new GUIStyle("Label");
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

