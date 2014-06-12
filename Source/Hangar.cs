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
		
		public class StoredVessel
		{
			public ProtoVessel vessel;
			public List<ProtoCrewMember> crew;
		}
		
		//internal properties
		private IHangarAnimator hangar_gates;
		public HangarGates gates_state { get { return hangar_gates.GatesState; } }
		public HangarState hangar_state { get; private set;}
		private double total_volume = 0;
		private double usefull_volume_ratio = 0.7; //only 70% of the volume may be used by docking vessels
		private double crew_volume_ratio = 0.3; //only 30% of the remaining volyme may be used for crew (i.e. V*(1-usefull_r)*crew_r)
		private double volume_per_kerbal = 1.3; //m^3
		//persistent private fields
		[KSPField (isPersistant = true)] private double used_volume = 0;
		[KSPField (isPersistant = true)] private float base_mass    = 0;
		[KSPField (isPersistant = true)] private float vessels_mass = 0;
		
		//vessels
		private Dictionary<Guid, StoredVessel> stored_vessels = new Dictionary<Guid, StoredVessel>();
		private Dictionary<Guid, bool> probed_ids = new Dictionary<Guid, bool>();
		
		//vessel spawn
		[KSPField (isPersistant = false)] public float  SpawnHeightOffset = 0.0f;
		[KSPField (isPersistant = false)] public string SpawnTransform;
		Transform restoreTransform;
		
		//gui fields
		[KSPField (guiName = "Volume", guiActive = true, guiActiveEditor=true)] public string total_v;
		[KSPField (guiName = "Volume used", guiActive = true)] public string used_v;
		[KSPField (guiName = "Vessels docked", guiActive = true)] public string vessels_docked;
		[KSPField (guiName = "Crew capacity", guiActive = true, guiActiveEditor=true)] public string crew_capacity;
		[KSPField (guiName = "Mass", guiActive = true)] public string total_m;
		[KSPField (guiName = "Hangar doors", guiActive = true)] public string doors;
		[KSPField (guiName = "Hangar state", guiActive = true)] public string state;
		
		
		//for GUI
		public List<VesselInfo> GetVessels()
		{
			List<VesselInfo> _vessels = new List<VesselInfo>();
			foreach(Guid vid in stored_vessels.Keys)
			{
				VesselInfo vinfo = new VesselInfo();
				vinfo.vid = vid; vinfo.vesselName = stored_vessels[vid].vessel.vesselName;
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
			foreach(MeshFilter m in p.FindModelComponents<MeshFilter>())
			{
				Vector3 s = Vector3.Scale(m.mesh.bounds.size, m.transform.localScale);
				double mvol = s.x*s.y*s.z*V_scale;
				if(mvol > vol) vol = mvol;
			}
			return vol;
		}
		
		public void RecalculateVolume()
		{
			//recalculate total volume
			double part_V =PartVolume(part);
			total_volume = part_V*usefull_volume_ratio;
			total_v = Utils.formatVolume(total_volume);
			//calculate crew capacity from remaining volume
			part.CrewCapacity = (int)(part_V*(1-usefull_volume_ratio)*crew_volume_ratio/volume_per_kerbal);
			crew_capacity = part.CrewCapacity.ToString();
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
			//check if the vessel is EVA. Maybe get EVA on board too?
			if(vsl.isEVA) 
			{
				FlightScreenMessager.showMessage("Kerbals cannot be docked", 3);
				return false;
			}
			//check vessel crew
			if(vsl.GetCrewCount() > vessel.GetCrewCapacity()-vessel.GetCrewCount())
			{
				FlightScreenMessager.showMessage("Not enough space for the crew of a docking vessel", 3);
				return false;
			}
			//check vessel volume
			double vol = VesselVolume(vsl);
			if(vol <= 0) return false;
			if(vol > total_volume-used_volume) 
			{
				FlightScreenMessager.showMessage("There's no room in the hangar for this vessel", 3);
				return false;
			}
			return true;
		}
		
		//add some crew to the part
		private static bool addCrew(Part p, List<ProtoCrewMember> crew)
		{
			if(crew.Count == 0) return false;
			if(p.CrewCapacity <= p.protoModuleCrew.Count) return false;
			while(p.protoModuleCrew.Count < p.CrewCapacity && crew.Count > 0)
			{
				crew[0].rosterStatus = ProtoCrewMember.RosterStatus.ASSIGNED;
				p.AddCrewmember(crew[0]);
				crew.RemoveAt(0);
			}
			return true;
		}
		
		//add some crew to the vessel
		private static void addCrew(Vessel vsl, List<ProtoCrewMember> crew)
		{
			foreach(Part p in vsl.parts)
			{
				if(crew.Count == 0) break;
				addCrew(p, crew);
			}
			vsl.SpawnCrew();
		}
		
		//remove crew from the part
		private static List<ProtoCrewMember> delCrew(Part p, List<ProtoCrewMember> crew)
		{
			List<ProtoCrewMember> deleted = new List<ProtoCrewMember>();
			if(p.CrewCapacity == 0 || p.protoModuleCrew.Count == 0) return deleted;
			foreach(ProtoCrewMember cr in p.protoModuleCrew)
			{ if(crew.Contains(cr)) deleted.Add(cr); }
			foreach(ProtoCrewMember cr in deleted) p.RemoveCrewmember(cr);
			return deleted;
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
				FlightScreenMessager.showMessage("Cannot accept the vessel while about to crush.", 3);
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
			if(!can_store || stored_vessels.ContainsKey(vsl.id)) return;
			//dock the vessel
			StoredVessel stored_vessel = new StoredVessel();
			//get vessel crew on board
			stored_vessel.crew = vsl.GetVesselCrew();
			List<ProtoCrewMember> _crew = new List<ProtoCrewMember>(stored_vessel.crew);
			vsl.DespawnCrew();
			//first of, add crew to the hangar if there's a place
			addCrew(part, _crew);
			//then add to other vessel parts if needed
			addCrew(vessel, _crew);
			//store vessel
			stored_vessel.vessel = vsl.BackupVessel();
			stored_vessels.Add(vsl.id, stored_vessel);
			used_volume += VesselVolume(vsl);
			vessels_mass += vsl.GetTotalMass();
			SetMass();
			//destroy vessel
			vsl.Die ();
			FlightScreenMessager.showMessage("Vessel has been docked inside the hangar", 3);
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
			StoredVessel stored_vessel;
			if(!stored_vessels.TryGetValue(vid, out stored_vessel)) return;
			//clean up
			stored_vessels.Remove(vid);
			//switch hangar state
			hangar_state = HangarState.Busy;
			Events["Prepare"].active = true;
			//set restored vessel orbit
			GetRestoreTransform();
			PositionVessel(stored_vessel.vessel);
			//restore vessel
			stored_vessel.vessel.Load(FlightDriver.FlightStateCache.flightState);
			//get restored vessel from the world
			Vessel vsl = FlightGlobals.Vessels[FlightGlobals.Vessels.Count-1];
			//transfer crew back to the launched vessel
			List<ProtoCrewMember> transfered_crew = new List<ProtoCrewMember>();
			foreach(Part p in vessel.parts)	transfered_crew.AddRange(delCrew(p, stored_vessel.crew));
			addCrew(vsl, transfered_crew);
			//change volume and mass
			if(stored_vessels.Count < 1) 
			{
				used_volume = 0;
				vessels_mass = 0;
			}
			else
			{
				used_volume -= VesselVolume(vsl);
				vessels_mass -= vsl.GetTotalMass();
			}
			SetMass();
			//switch to restored vessel
			FlightGlobals.ForceSetActiveVessel(vsl);
			Staging.beginFlight();
			Open();
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
			if(stored_vessels.Count == 0) return;
			ConfigNode vessels_node = node.AddNode("STORED_VESSELS");
			foreach(StoredVessel sv in stored_vessels.Values)
			{
				ConfigNode stored_vessel_node = vessels_node.AddNode("STORED_VESSEL");
				ConfigNode vessel_node = stored_vessel_node.AddNode("VESSEL");
				ConfigNode crew_node = stored_vessel_node.AddNode("CREW");
				sv.vessel.Save(vessel_node);
				foreach(ProtoCrewMember c in sv.crew)
				{
					ConfigNode n = crew_node.AddNode(c.name);
					c.Save(n);
				}
			}
		}
		
		//load the hangar
		public override void OnLoad(ConfigNode node)
		{ 
			//hangar state
			if(node.HasValue ("hangarState"))
				hangar_state = (HangarState)Enum.Parse(typeof(HangarState), node.GetValue("hangarState"));
			//restore stored vessels
			if(node.HasNode("STORED_VESSELS"))
			{
				ConfigNode vessels_node = node.GetNode("STORED_VESSELS");
				foreach(ConfigNode vn in vessels_node.nodes)
				{
					ConfigNode vessel_node = vn.GetNode("VESSEL");
					ConfigNode crew_node = vn.GetNode("CREW");
					StoredVessel sv = new StoredVessel();
					sv.vessel = new ProtoVessel(vessel_node, FlightDriver.FlightStateCache);
					sv.crew = new List<ProtoCrewMember>();
					foreach(ConfigNode cn in crew_node.nodes) sv.crew.Add(new ProtoCrewMember(cn));
					stored_vessels.Add(sv.vessel.vesselID, sv);
				}
			}
		}
		
		//update labels
		public override void OnUpdate ()
		{
			doors = hangar_gates.GatesState.ToString();
			state = hangar_state.ToString();
			used_v = Utils.formatVolume(used_volume);
			vessels_docked = String.Format ("{0}", stored_vessels.Count);
			total_m = Utils.formatMass(part.mass);
			crew_capacity = part.CrewCapacity.ToString();
		}
		
	}
}

