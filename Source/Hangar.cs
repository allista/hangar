using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AtHangar
{
	//this module adds the ability to store a vessel in a packed state inside
	public class Hangar : PartModule
	{
		//internal properties
		IHangarAnimator hangar_state;
		double usefull_volume_ratio = 0.7;
		double used_volume = 0;
		double total_volume = 0;
		float base_mass = 0;
		float vessels_mass = 0;
		
		//vessels
		Dictionary<Guid, ProtoVessel> stored_vessels = new Dictionary<Guid, ProtoVessel>();
		List<Guid> stored_vids = new List<Guid>();
		Dictionary<Guid, bool> probed_ids = new Dictionary<Guid, bool>();
		
		//gui fields
		[KSPField (guiName = "Volume", guiActive = true, guiActiveEditor=true)] public string total_v;
		[KSPField (guiName = "Hangar doors", guiActive = true)] public string doors;
		[KSPField (guiName = "Volume used", guiActive = true)] public string used_v;
		[KSPField (guiName = "Vessels stored", guiActive = true)] public string vessels;
		
		
		//info string
		//public override string GetInfo () { return ""; }
		
		//all initialization goes here instead of the constructor as documented in Unity API
		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			
			part.force_activate();
            hangar_state = part.Modules.OfType<IHangarAnimator>().SingleOrDefault();
			if (hangar_state == null)
			{
                hangar_state = new DummyHangarAnimator();
				Debug.Log("[Hangar] Using DummyHangarAnimator");
			}
            else
            {
                Events["Open"].guiActiveEditor = true;
                Events["Close"].guiActiveEditor = false;
            }
			
			if (HighLogic.LoadedSceneIsEditor) RecalculateVolume();
		}
		
		//calculate part volume as the sum of bounding boxes of its rendered components
		public static double PartVolume(Part p)
		{
			double vol = 0;
			foreach(MeshFilter m in p.gameObject.GetComponentsInChildren<MeshFilter>())
			{
				Vector3 s = m.mesh.bounds.size;
				vol += s.x*s.y*s.z;
			}
			return vol;
		}
		
		public void RecalculateVolume()
		{
			total_volume = PartVolume(part)*usefull_volume_ratio;
			total_v = String.Format ("{0:F1} m^3", total_volume);
		}
		
		//calculate approximate volume of a vessel 
		//as the sum of volumes of bounding boxes of its parts
		public static double VesselVolume(Vessel vsl)
		{
			if(vsl == null) return 0;
			double vol = 0;
			foreach (Part p in vsl.parts)
				vol += PartVolume(p);
			return vol;
		}
		
		//if a vessel can be stored in the hangar
		public bool CanStore(Vessel vsl)
		{
			if(vsl == null) return false;
			if(vsl.isEVA) 
			{
				FlightScreenMessager.showMessage("Kerbals cannot be docked", 3);
				return false;
			}
			double vol = VesselVolume(vsl);
			if(vol <= 0) return false;
			if(vol > total_volume-used_volume) 
			{
				FlightScreenMessager.showMessage("There's no room in the hangar for this vessel", 3);
				return false;
			}
			return true;
		}
		
		//store vessel
		private void TryStoreVessel(Vessel vsl)
		{
			//check stored value; if not found, store.
			bool can_store;
			if(!probed_ids.TryGetValue(vsl.id, out can_store))
			{
				can_store = CanStore(vsl);
				probed_ids.Add(vsl.id, can_store);
			}
			if(!can_store) return;
			//store vessel implementation//
			ProtoVessel saved_vessel = vsl.BackupVessel();
			used_volume += VesselVolume(vsl);
			vessels_mass += vsl.GetTotalMass();
			part.mass = base_mass+vessels_mass;
			stored_vessels.Add(vsl.id, saved_vessel);
			stored_vids.Add(vsl.id);
			vsl.Die ();
			FlightScreenMessager.showMessage("Vessel has been docked inside the hangar", 3);
			Events["Restore"].active = true;
		}
		
		//restore vessel
		private void TryRestoreVessel(Guid vid)
		{
			ProtoVessel stored_vessel;
			if(!stored_vessels.TryGetValue(vid, out stored_vessel)) return;
			//restore vessel
			Game state = FlightDriver.FlightStateCache;
			stored_vessel.Load(state.flightState);
			//get restored vessel from the world
			Vessel vsl = FlightGlobals.Vessels[FlightGlobals.Vessels.Count-1];
			//fly it
			FlightGlobals.ForceSetActiveVessel (vsl);
			Staging.beginFlight ();
			FlightGlobals.overrideOrbit = true;
			//clean up
			stored_vessels.Remove(vid);
			if(stored_vessels.Count < 1) Events["Restore"].active = false;
		}
		
		private void TryRestoreLastVessel()
		{
			if(stored_vessels.Count < 1) return;
			Guid vid = stored_vids[stored_vids.Count-1];
			TryRestoreVessel(vid);
		}
		
		//called every frame while part collider is touching the trigger
		public void OnTriggerStay (Collider col) //see Unity docs
		{
			if (hangar_state.CurrentState != HangarState.Opened
				|| !col.CompareTag ("Untagged")
				|| col.gameObject.name == "MapOverlay collider") // kethane
				return;
			//get part and try to store vessel
			Part p = col.attachedRigidbody.GetComponent<Part>();
			if (p == null) return;
			TryStoreVessel(p.vessel);
		}
		
		//called when part collider exits the trigger
		public void OnTriggerExit(Collider col)
		{
			Part p = col.attachedRigidbody.GetComponent<Part>();
			if (p == null) return;
			if(probed_ids.ContainsKey(p.vessel.id)) probed_ids.Remove(p.vessel.id);
		}
		
		
		//events
		//open event
		[KSPEvent (guiActive = true, guiName = "Open hangar", active = true)]
		public void Open()
		{
			hangar_state.Open();
			Events["Open"].active = false;
			Events["Close"].active = true;
		}
	
		//close event
		[KSPEvent (guiActive = true, guiName = "Close hangar", active = false)]
		public void Close()
		{
			hangar_state.Close();
			Events["Open"].active = true;
			Events["Close"].active = false;
		}
		
		//restore event
		[KSPEvent (guiActive = true, guiName = "Launch last vessel", active = false)]
		public void Restore()
		{
			if(hangar_state.CurrentState != HangarState.Opened)
				FlightScreenMessager.showMessage("Open the hangar first", 3);
			else TryRestoreLastVessel();
		}
		
		
		//actions
		[KSPAction("Open hangar")]
        public void OpenHangarAction(KSPActionParam param) { Open (); }
		
		[KSPAction("Close hangar")]
        public void CloseHangarAction(KSPActionParam param) { Close (); }
		
		[KSPAction("Toggle hangar")]
        public void ToggleHangarAction(KSPActionParam param) { hangar_state.Toggle(); }
	
		//save the hangar
		public override void OnSave(ConfigNode node)
		{
			if (base_mass != 0)
				node.AddValue ("baseMass", base_mass);
			if (vessels_mass != 0)
				node.AddValue ("vesselsMass", vessels_mass);
			//save stored vessels
			foreach(ProtoVessel pv in stored_vessels.Values)
			{
				ConfigNode vessel_node = node.AddNode("VESSEL");
				pv.Save(vessel_node);
			}
			return;
		}
		
		//load the hangar
		public override void OnLoad(ConfigNode node)
		{ 
			if(hangar_state != null) Close();
			if (node.HasValue ("baseMass"))
				float.TryParse (node.GetValue ("baseMass"), out base_mass);
			if (node.HasValue ("vesselsMass"))
				float.TryParse (node.GetValue ("vesselsMass"), out vessels_mass);
			else base_mass = part.mass;
			
		}
		
		
		public override void OnUpdate ()
		{
			doors = hangar_state.CurrentState.ToString();
			used_v = String.Format ("{0:F1} m^3", used_volume);
			vessels = String.Format ("{0}", stored_vessels.Count);
		}
		
	}
}

