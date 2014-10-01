#if DEBUG
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public static partial class Utils
	{
		public static void logStamp(string msg = "") { Debug.Log("[Hangar] === " + msg); }

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
	}

	public class NamedStopwatch
	{
		readonly System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
		readonly string name;

		public NamedStopwatch(string name)
		{ this.name = name; }

		public void Start()
		{
			Utils.Log("{0}: start counting time", name);
			sw.Start();
		}

		public void Stamp()
		{
			Utils.Log("{0}: elapsed time: {1}us", name, 
				sw.ElapsedTicks/(System.Diagnostics.Stopwatch.Frequency/(1000000L)));
		}

		public void Stop() { sw.Stop(); Stamp(); }

		public void Reset() { sw.Stop(); sw.Reset(); }
	}
}
#endif

