// This code is based on Procedural Fairings plug-in by Alexey Volynskov, PMUtils class
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using KSPAPIExtensions;


namespace AtHangar
{
	public class Utils
	{
		static bool haveTech (string name)
		{
			if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER)
				return name == "sandbox";
			return ResearchAndDevelopment.GetTechnologyState (name) == RDTech.State.Available;
		}
		
		public static float getTechMinValue (string cfgname, float defVal)
		{
			bool hasValue = false;
			float minVal = 0;
		
			foreach (var tech in GameDatabase.Instance.GetConfigNodes(cfgname))
				for (int i=0; i<tech.values.Count; ++i) {
					var value = tech.values [i];
					if (!haveTech (value.name))	continue;
					float v = float.Parse (value.value);
					if (!hasValue || v < minVal) {
						minVal = v;
						hasValue = true;
					}
				}
		
			if (!hasValue) return defVal;
			return minVal;
		}
		
		public static float getTechMaxValue (string cfgname, float defVal)
		{
			bool hasValue = false;
			float maxVal = 0;
		
			foreach (var tech in GameDatabase.Instance.GetConfigNodes(cfgname))
				for (int i=0; i<tech.values.Count; ++i) {
					var value = tech.values [i];
					if (!haveTech (value.name))	continue;
					float v = float.Parse (value.value);
					if (!hasValue || v > maxVal) {
						maxVal = v;
						hasValue = true;
					}
				}
		
			if (!hasValue) return defVal;
			return maxVal;
		}
		
		public static void setFieldRange (BaseField field, float minval, float maxval)
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
		
		public static void updateAttachedPartPos(AttachNode node, Part part)
		{
			if (node == null || part == null) return;
		
			var ap = node.attachedPart;
			if (!ap) return;
		
			var an = ap.findAttachNodeByPart (part);
			if (an == null)	return;
		
			var dp =
				part.transform.TransformPoint (node.position) -
				ap.transform.TransformPoint (an.position);
		
			if (ap == part.parent) {
				while (ap.parent) ap = ap.parent;
				ap.transform.position += dp;
				part.transform.position -= dp;
			} else
				ap.transform.position += dp;
		}
		
		public static Vector3 ScaleVector(Vector3 v, float s, float l)
		{ return Vector3.Scale(v, new Vector3(s, s*l, s)); }
		
		//formatting
		public static string formatMass (float mass)
		{
			if (mass < 0.01f)
				return (mass * 1e3f).ToString("n3") + "kg";
			else
				return mass.ToString("n3") + "t";
		}
		
		public static string formatVolume (double volume)
		{
			if (volume < 0.1f)
				return (volume * 1e3f).ToString ("n0") + " L";
			else
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
            else
            {
                ScreenMessages.PostScreenMessage("Sound file : " + sndPath + " as not been found, please check your Hangar installation !", 10, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }     
        }
		
		#region Debug
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
		
		public static void logVectors(IEnumerable<Vector3> vecs)
		{ foreach(Vector3 v in vecs) Debug.Log(formatVector(v)); }
		
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
		#endregion
	}
	
	
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class FlightScreenMessager : MonoBehaviour
	{
		static float osdMessageTime = 0;
		static string osdMessageText = null;

		public static void showMessage (string msg, float delay)
		{
			osdMessageText = msg;
			osdMessageTime = Time.time + delay;
		}

		public void OnGUI ()
		{
			if (!HighLogic.LoadedSceneIsFlight)	return;

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
}

