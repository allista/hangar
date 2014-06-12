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
		public enum HangarState{Ready,Busy};
		
		public class VesselInfo
		{
			public Guid vid;
			public string vesselName;
		}
		
		//internal properties
		private IHangarAnimator hangar_gates;
		public HangarGates gates_state { get { return hangar_gates.GatesState; } }
		public HangarState hangar_state { get; private set;}
		private double total_volume = 0;
		private double usefull_volume_ratio = 0.7; //only 70% of the volume may be used by docking vessels
		//persistent private fields
		[KSPField (isPersistant = true)] private double used_volume = 0;
		[KSPField (isPersistant = true)] private float base_mass    = 0;
		[KSPField (isPersistant = true)] private float vessels_mass = 0;
		
		//vessels
		private Dictionary<Guid, ProtoVessel> stored_vessels = new Dictionary<Guid, ProtoVessel>();
		private List<Guid> stored_vids = new List<Guid>();
		private Dictionary<Guid, bool> probed_ids = new Dictionary<Guid, bool>();
		
		//vessel spawn
		[KSPField (isPersistant = false)] public float  SpawnHeightOffset = 0.0f;
		[KSPField (isPersistant = false)] public string SpawnTransform;
		Transform restoreTransform;
		
		//gui fields
		[KSPField (guiName = "Volume", guiActive = true, guiActiveEditor=true)] public string total_v;
		[KSPField (guiName = "Mass", guiActive = true)] public string total_m;
		[KSPField (guiName = "Hangar doors", guiActive = true)] public string doors;
		[KSPField (guiName = "Hangar state", guiActive = true)] public string state;
		[KSPField (guiName = "Volume used", guiActive = true)] public string used_v;
		[KSPField (guiName = "Vessels stored", guiActive = true)] public string vessels;
		
		
		//for GUI
		public List<VesselInfo> GetVessels()
		{
			List<VesselInfo> _vessels = new List<VesselInfo>();
			foreach(Guid vid in stored_vessels.Keys)
			{
				VesselInfo vinfo = new VesselInfo();
				vinfo.vid = vid; vinfo.vesselName = stored_vessels[vid].vesselName;
				_vessels.Add(vinfo);
			}
			return _vessels;
		}
		
		public void UpdateMenus (bool visible)
		{
			Events["HideUI"].active = visible;
			Events["ShowUI"].active = !visible;
		}
		
		[KSPEvent (guiActive = true, guiName = "Hide Controls", active = false)]
		public void HideUI () { HangarWindow.HideGUI (); }

		[KSPEvent (guiActive = true, guiName = "Show Controls", active = false)]
		public void ShowUI () { HangarWindow.ShowGUI (); }
		
		public int numVessels() { return stored_vessels.Count; }
		
		
		//all initialization goes here instead of the constructor as documented in Unity API
		public override void OnStart(StartState state)
		{
			//base OnStart
			base.OnStart(state);
			if (state == StartState.None) return;
			Setup(); //recalculate volume and mass
			//if in editor, nothing is left to do
			if(state == StartState.Editor) return;
			//if not in editor, initialize Animator
			part.force_activate();
            hangar_gates = part.Modules.OfType<IHangarAnimator>().SingleOrDefault();
			if (hangar_gates == null)
			{
                hangar_gates = new DummyHangarAnimator();
				Debug.Log("[Hangar] Using DummyHangarAnimator");
			}
            else
            {
                Events["Open"].guiActiveEditor = true;
                Events["Close"].guiActiveEditor = true;
            }
		}
		
		public void Setup()	{ SetMass (); RecalculateVolume(); }
		
		public void SetMass() 
		{ 
			if(base_mass == 0) base_mass = part.mass;
			part.mass = base_mass+vessels_mass; 
		}
		
		//calculate part volume as the volume of the biggest of bounding boxes of model meshes
		public static double PartVolume(Part p)
		{
			if(p == null) return 0;
			var model = p.FindModelTransform("model");
			if (model == null) return 0;
			float V_scale = model.localScale.x*model.localScale.y*model.localScale.z;
			double vol = 0;
//			Debug.Log (String.Format("[Hangar] calculating volume of '{0}'; scale is: {1}", p.name, model.localScale));
			foreach(MeshFilter m in p.FindModelComponents<MeshFilter>())
			{
				Vector3 s = Vector3.Scale(m.mesh.bounds.size, m.transform.localScale);
				double mvol = s.x*s.y*s.z*V_scale;
				if(mvol > vol) vol = mvol;
//				Debug.Log (String.Format("[Hangar] mesh {0}: local scale {1}, scale {2}, local size {3}, size {4}, volume {5}", 
//				                         m.transform.name, 
//				                         m.transform.localScale, Vector3.Scale(m.transform.localScale, model.localScale), 
//				                         s, Vector3.Scale(s, model.localScale), 
//				                         mvol));
			}
			return vol;
		}
		
		public void RecalculateVolume()
		{
			total_volume = PartVolume(part)*usefull_volume_ratio;
			total_v = Utils.formatVolume(total_volume);
		}
		
		//calculate transform of restored vessel
		private Transform GetRestoreTransform ()
		{
			if (SpawnTransform != "")
				restoreTransform = part.FindModelTransform(SpawnTransform);
			else
			{
				Vector3 offset = Vector3.up * SpawnHeightOffset;
				Transform t = part.transform;
				GameObject restorePos = new GameObject ();
				restorePos.transform.position = t.position;
				restorePos.transform.position += t.TransformDirection (offset);
				restorePos.transform.rotation = t.rotation;
				restoreTransform = restorePos.transform;
			}
			return restoreTransform;
		}
		
		//set vessel orbit, transform, coordinates
		private void PositionVessel(ProtoVessel pv)
		{
			//state
			pv.splashed  = vessel.Landed;
			pv.landed    = vessel.Splashed;
			//rotation
			pv.rotation  = restoreTransform.rotation; //TODO: is this correct?
			//surface
			if(vessel.LandedOrSplashed)
			{
				Vector3 v  = restoreTransform.position;
				v = new Vector3d(v.x, v.y, v.z);
				pv.longitude = vessel.mainBody.GetLongitude(v);
				pv.latitude  = vessel.mainBody.GetLatitude(v);
				pv.altitude  = vessel.mainBody.GetAltitude(v);
			}
			else
			{
				//transform
				pv.position = part.partTransform.InverseTransformPoint(restoreTransform.position);
				pv.rotation = restoreTransform.rotation;
				//orbit
				pv.orbitSnapShot = new OrbitSnapshot(vessel.orbit);
			}
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
			//if hangar is not ready, return
			if(hangar_state == HangarState.Busy) 
			{
				FlightScreenMessager.showMessage("Prepare hangar first.", 3);
				return;
			}
			//check self state first
			switch(FlightGlobals.ClearToSave()) 
			{
			case ClearToSaveStatus.NOT_WHILE_ABOUT_TO_CRASH:
			{
				FlightScreenMessager.showMessage("Cannot accept vessel while about to crush.", 3);
				return;
			}
			default:
				break;
			}
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
			if(stored_vessels.ContainsKey(vsl.id)) return;
			stored_vessels.Add(vsl.id, saved_vessel);
			stored_vids.Add(vsl.id);
			used_volume += VesselVolume(vsl);
			vessels_mass += vsl.GetTotalMass();
			SetMass();
			vsl.Die ();
			FlightScreenMessager.showMessage("Vessel has been docked inside the hangar", 3);
			Events["Restore"].active = true;
			Close();
		}
		
		//restore vessel
		public void TryRestoreVessel(Guid vid)
		{
			//if hangar is not ready, return
			if(hangar_state == HangarState.Busy) 
			{
				FlightScreenMessager.showMessage("Prepare hangar first.", 3);
				return;
			}
			//if in orbit or on the ground and not moving
			switch(FlightGlobals.ClearToSave()) 
			{
			case ClearToSaveStatus.NOT_IN_ATMOSPHERE:
			{
				FlightScreenMessager.showMessage("Cannot launch vessel while flying in atmosphere.", 3);
				return;
			}
			case ClearToSaveStatus.NOT_UNDER_ACCELERATION:
			{
				FlightScreenMessager.showMessage("Cannot launch vessel hangar is under accelleration.", 3);
				return;
			}
			case ClearToSaveStatus.NOT_WHILE_ABOUT_TO_CRASH:
			{
				FlightScreenMessager.showMessage("Cannot launch vessel while about to crush.", 3);
				return;
			}
			case ClearToSaveStatus.NOT_WHILE_MOVING_OVER_SURFACE:
			{
				FlightScreenMessager.showMessage("Cannot launch vessel while moving on the surface.", 3);
				return;
			}
			case ClearToSaveStatus.NOT_WHILE_THROTTLED_UP:
			{
				FlightScreenMessager.showMessage("Cannot launch vessel while thottled up.", 3);
				return;
			}
			default:
				FlightScreenMessager.showMessage("Launching vessel...", 3);
				break;
			}
			//get vessel
			ProtoVessel stored_vessel;
			if(!stored_vessels.TryGetValue(vid, out stored_vessel)) return;
			//clean up
			stored_vessels.Remove(vid);
			stored_vids.Remove(vid);
			//set restored vessel orbit
			GetRestoreTransform();
			PositionVessel(stored_vessel);
			//restore vessel
			Game state = FlightDriver.FlightStateCache;
			stored_vessel.Load(state.flightState);
			//get restored vessel from the world
			Vessel vsl = FlightGlobals.Vessels[FlightGlobals.Vessels.Count-1];
			//change volume and mass
			if(stored_vessels.Count < 1) 
			{
				used_volume = 0;
				vessels_mass = 0;
				Events["Restore"].active = false;
			}
			else
			{
				used_volume -= VesselVolume(vsl);
				vessels_mass -= vsl.GetTotalMass();
			}
			SetMass();
			hangar_state = HangarState.Busy;
			Events["Prepare"].active = true;
			//switch to restored vessel
			FlightGlobals.ForceSetActiveVessel(vsl);
			Staging.beginFlight();
			Open();
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
			if (hangar_gates.GatesState != HangarGates.Opened
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
			hangar_gates.Open();
			Events["Open"].active = false;
			Events["Close"].active = true;
		}
	
		//close event
		[KSPEvent (guiActive = true, guiName = "Close hangar", active = false)]
		public void Close()
		{
			hangar_gates.Close();
			Events["Open"].active = true;
			Events["Close"].active = false;
		}
		
		//restore event
		[KSPEvent (guiActive = true, guiName = "Launch last vessel", active = false)]
		public void Restore()
		{
			if(hangar_gates.GatesState != HangarGates.Opened)
				FlightScreenMessager.showMessage("Open the hangar first", 3);
			else TryRestoreLastVessel();
		}
		
		//prepare event
		[KSPEvent (guiActive = true, guiName = "Prepare hangar", active = false)]
		public void Prepare()
		{
			hangar_state = HangarState.Ready;
			Events["Prepare"].active = false;
		}
		
		
		//actions
		[KSPAction("Open hangar")]
        public void OpenHangarAction(KSPActionParam param) { Open(); }
		
		[KSPAction("Close hangar")]
        public void CloseHangarAction(KSPActionParam param) { Close(); }
		
		[KSPAction("Toggle hangar")]
        public void ToggleHangarAction(KSPActionParam param) 
		{ 
			if(hangar_gates.Toggle())
			{
				Events["Open"].active = false;
				Events["Close"].active = true;
			}
			else 
			{
				Events["Open"].active = true;
				Events["Close"].active = false;
			}
		}
		
		[KSPAction("Prepare hangar")]
        public void PrepareHangarAction(KSPActionParam param) { Prepare(); }
		
	
		//save the hangar
		public override void OnSave(ConfigNode node)
		{
			//hangar state
			node.AddValue("hangarState", hangar_state.ToString());
			//save stored vessels
			foreach(ProtoVessel pv in stored_vessels.Values)
			{
				ConfigNode vessel_node = node.AddNode("VESSEL");
				pv.Save(vessel_node);
			}
		}
		
		//load the hangar
		public override void OnLoad(ConfigNode node)
		{ 
			//hangar state
			if (node.HasValue ("hangarState"))
				hangar_state = (HangarState)Enum.Parse(typeof(HangarState), node.GetValue("hangarState"));
			//restore stored vessels
			foreach(ConfigNode n in node.nodes)
			{
				if (n.name != "VESSEL") continue;
				ProtoVessel pv = new ProtoVessel(n, FlightDriver.FlightStateCache);
				stored_vessels.Add(pv.vesselID, pv);
			}
		}
		
		//update labels
		public override void OnUpdate ()
		{
			doors = hangar_gates.GatesState.ToString();
			state = hangar_state.ToString();
			used_v = Utils.formatVolume(used_volume);
			vessels = String.Format ("{0}", stored_vessels.Count);
			total_m = Utils.formatMass(part.mass);
		}
		
	}
}

