//This code is partly based on the code from Extraplanetary Launchpad plugin. ExLaunchPad and Recycler classes.
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
			public Vector3 CoM;
			public Vector3 CoG; //center of geometry
			public float mass;
			public Metric metric;
			public List<ProtoCrewMember> crew;
			
			public StoredVessel() {}
			
			public StoredVessel(ConfigNode node) { Load(node); }
			
			public void Save(ConfigNode node)
			{
				//nodes
				ConfigNode vessel_node = node.AddNode("VESSEL");
				ConfigNode metric_node = node.AddNode("METRIC");
				ConfigNode crew_node   = node.AddNode("CREW");
				vessel.Save(vessel_node);
				metric.Save(metric_node);
				foreach(ProtoCrewMember c in crew)
				{
					ConfigNode n = crew_node.AddNode(c.name);
					c.Save(n);
				}
				//values
				node.AddValue("CoM", CoM);
				node.AddValue("CoG", CoG);
				node.AddValue("mass", mass);
			}
			
			public void Load(ConfigNode node)
			{
				ConfigNode vessel_node = node.GetNode("VESSEL");
				ConfigNode metric_node = node.GetNode("METRIC");
				ConfigNode crew_node   = node.GetNode("CREW");
				vessel = new ProtoVessel(vessel_node, FlightDriver.FlightStateCache);
				metric = new Metric(metric_node);
				crew   = new List<ProtoCrewMember>();
				foreach(ConfigNode cn in crew_node.nodes) crew.Add(new ProtoCrewMember(cn));
				CoM  = ConfigNode.ParseVector3(node.GetValue("CoM"));
				CoG  = ConfigNode.ParseVector3(node.GetValue("CoG"));
				mass = float.Parse(node.GetValue("mass"));
			}
		}
		
		//internal properties
		private BaseHangarAnimator hangar_gates;
		public HangarGates gates_state { get { return hangar_gates.GatesState; } }
		public HangarState hangar_state { get; private set;}
		public Metric hangar_metric;
		private float usefull_volume_ratio = 0.7f; //only 70% of the volume may be used by docking vessels
		private float crew_volume_ratio    = 0.3f; //only 30% of the remaining volume may be used for crew (i.e. V*(1-usefull_r)*crew_r)
		[KSPField (isPersistant = false)] public float volumePerKerbal = 3f; //m^3
		[KSPField (isPersistant = false)] public bool StaticCrewCapacity = false;
		//persistent private fields
		[KSPField (isPersistant = true)] private float used_volume  = 0f;
		[KSPField (isPersistant = true)] private float base_mass    = 0f;
		[KSPField (isPersistant = true)] private float vessels_mass = 0f;
		
		//vessels
		private Dictionary<Guid, StoredVessel> stored_vessels = new Dictionary<Guid, StoredVessel>();
		private Dictionary<Guid, bool> probed_ids = new Dictionary<Guid, bool>();
		
		//vessel spawn
		[KSPField (isPersistant = false)] public float  LaunchHeightOffset = 0.0f;
		[KSPField (isPersistant = false)] public string LaunchTransform;
		[KSPField (isPersistant = false)] public string HangarSpace;
		Transform launchTransform;
		Vessel launched_vessel;
		
		//gui fields
		[KSPField (guiName = "Volume", guiActive = true, guiActiveEditor=true)] public string hangar_v;
		[KSPField (guiName = "Dimensions", guiActive = true, guiActiveEditor=true)] public string hangar_d;
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
		public override void OnAwake()
		{
			base.OnAwake ();
			usefull_volume_ratio = (float)Math.Pow(usefull_volume_ratio, 1/3f);
		}
		
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
            hangar_gates = part.Modules.OfType<BaseHangarAnimator>().SingleOrDefault();
			if (hangar_gates == null)
			{
                hangar_gates = new BaseHangarAnimator();
				Debug.Log("[Hangar] Using BaseHangarAnimator");
			}
		}
		
		public void Setup()	{ SetMass (); RecalculateVolume(); }
		
		public void SetMass() 
		{ 
			if(base_mass == 0) base_mass = part.mass;
			part.mass = base_mass+vessels_mass; 
		}
		
		public void RecalculateVolume()
		{
			//recalculate total volume
			Metric part_metric = new Metric(part);
			if(HangarSpace != "") 
				hangar_metric = new Metric(part, HangarSpace);
			if(hangar_metric == null || hangar_metric.Empy())
				hangar_metric = part_metric*usefull_volume_ratio;
			//calculate crew capacity from remaining volume
			if(!StaticCrewCapacity)
			{
				part.CrewCapacity = (int)((part_metric.volume-hangar_metric.volume)*crew_volume_ratio/volumePerKerbal);
				crew_capacity = part.CrewCapacity.ToString();
			}
			//calculate hangar volume
			hangar_v = Utils.formatVolume(hangar_metric.volume);
			hangar_d = Utils.formatDimensions(hangar_metric.size);
		}
		
		//calculate transform of restored vessel
		private Transform get_launch_transform()
		{
			launchTransform = null;
			if(LaunchTransform != "")
				launchTransform = part.FindModelTransform(LaunchTransform);
			if(launchTransform == null)
			{
				Vector3 offset = Vector3.up * LaunchHeightOffset;
				Transform t = part.transform;
				GameObject restorePos = new GameObject ();
				restorePos.transform.position = t.position;
				restorePos.transform.position += t.TransformDirection (offset);
				restorePos.transform.rotation = t.rotation;
				launchTransform = restorePos.transform;
				Debug.Log(string.Format("LaunchTransform not found. Using offset."));
			}
			Debug.Log(string.Format("LaunchTransform used: {0}, {1}-{2}",launchTransform.name, launchTransform.position,  launchTransform.rotation.eulerAngles));
			return launchTransform;
		}
		
		//set vessel orbit, transform, coordinates
		private void PositionVessel(StoredVessel sv)
		{
			ProtoVessel pv = sv.vessel;
			//state
			pv.splashed = vessel.Landed;
			pv.landed   = vessel.Splashed;
			//rotation
			ProtoVessel hpv = vessel.BackupVessel();
			Quaternion proto_rot  = hpv.rotation;
			Quaternion hangar_rot = vessel.vesselTransform.rotation;
			pv.rotation = proto_rot*hangar_rot.Inverse()*launchTransform.rotation;
			//debug
			Debug.Log(string.Format("[Hangar] vessel CoM: {0}", vessel.findWorldCenterOfMass()));
			Debug.Log(string.Format("[Hangar] proto CoM: {0}", vessel.protoVessel.CoM));
			Debug.Log(string.Format("[Hangar] vessel rotation: {0}", vessel.vesselTransform.rotation.eulerAngles));
			Debug.Log(string.Format("[Hangar] vessel proto-rotation: {0}", hpv.rotation.eulerAngles));
			
			Debug.Log(string.Format("[Hangar] part position: {0}", part.partTransform.position));
			Debug.Log(string.Format("[Hangar] part rotation: {0}", part.partTransform.rotation.eulerAngles));
			
			Debug.Log(string.Format("[Hangar] orb position: {0}", vessel.orbit.pos));
			
			Debug.Log(string.Format("[Hangar] new position: {0}", pv.position));
			Debug.Log(string.Format("[Hangar] new rotation: {0}", pv.rotation.eulerAngles));
			//calculate launch launch offset from vessel bounds
			Vector3 bounds_offset = sv.CoM - sv.CoG + launchTransform.up*sv.metric.bounds.extents.y;
			//surface
			if(vessel.LandedOrSplashed)
			{
				Vector3d v   = Vector3d.zero+launchTransform.position+bounds_offset;
				pv.longitude = vessel.mainBody.GetLongitude(v);
				pv.latitude  = vessel.mainBody.GetLatitude(v);
				pv.altitude  = vessel.mainBody.GetAltitude(v);
			}
			else //setup new orbit
			{
				Orbit horb = vessel.orbit;
				Orbit vorb = new Orbit();
				Vector3 d_pos = launchTransform.position-vessel.findWorldCenterOfMass()+bounds_offset;
				Vector3d vpos = horb.pos+new Vector3d(d_pos.x, d_pos.z, d_pos.y);
				vorb.UpdateFromStateVectors(vpos, horb.vel, horb.referenceBody, Planetarium.GetUniversalTime());
				pv.orbitSnapShot = new OrbitSnapshot(vorb);
			}
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
		
		
		//if a vessel can be stored in the hangar
		public bool CanStore(Vessel vsl)
		{
			if(vsl == null || vsl == vessel) return false;
			//check if the vessel is EVA. Maybe get EVA on board too?
			if(vsl.isEVA) return false;
			//check vessel crew
			if(vsl.GetCrewCount() > vessel.GetCrewCapacity()-vessel.GetCrewCount())
			{
				FlightScreenMessager.showMessage("Not enough space for the crew of a docking vessel", 3);
				return false;
			}
			//check vessel metrics
			Metric metric = new Metric(vsl);
			if(metric.volume > hangar_metric.volume-used_volume) 
			{
				FlightScreenMessager.showMessage("There's no room in the hangar for this vessel", 3);
				return false;
			}
			if(!metric.Fits(hangar_metric))
			{
				FlightScreenMessager.showMessage("The vessel does not fit into this hangar", 3);
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
				FlightScreenMessager.showMessage("Prepare hangar first", 3);
				return;
			}
			//check self state first
			switch(FlightGlobals.ClearToSave()) 
			{
			case ClearToSaveStatus.NOT_WHILE_ABOUT_TO_CRASH:
			{
				FlightScreenMessager.showMessage("Cannot accept the vessel while about to crush", 3);
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
			//switch to hangar vessel before storing
			if(FlightGlobals.ActiveVessel != vessel)
				FlightGlobals.ForceSetActiveVessel(vessel);
			//store the vessel
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
			stored_vessel.metric = new Metric(vsl);
			stored_vessel.CoM    = vsl.findWorldCenterOfMass();
			stored_vessel.CoG    = vsl.vesselTransform.TransformPoint(stored_vessel.metric.bounds.center);
			stored_vessel.mass   = vsl.GetTotalMass();
			stored_vessels.Add(vsl.id, stored_vessel);
			//recalculate volume and mass
			used_volume  += stored_vessel.metric.volume;
			vessels_mass += stored_vessel.mass;
			SetMass();
			//destroy vessel
			vsl.Die();
			FlightScreenMessager.showMessage("Vessel has been docked inside the hangar", 3);
//			Close();
		}
		
		
		//restore vessel
		private bool CanRestore()
		{
			//if hangar is not ready, return
			if(hangar_state == HangarState.Busy) 
			{
				FlightScreenMessager.showMessage("Prepare hangar first", 3);
				return false;
			}
			if(hangar_gates.GatesState != HangarGates.Opened) 
			{
				FlightScreenMessager.showMessage("Open hangar gates first", 3);
				return false;
			}
			//if in orbit or on the ground and not moving
			switch(FlightGlobals.ClearToSave()) 
			{
			case ClearToSaveStatus.NOT_IN_ATMOSPHERE:
			{
				FlightScreenMessager.showMessage("Cannot launch vessel while flying in atmosphere", 3);
				return false;
			}
			case ClearToSaveStatus.NOT_UNDER_ACCELERATION:
			{
				FlightScreenMessager.showMessage("Cannot launch vessel hangar is under accelleration", 3);
				return false;
			}
			case ClearToSaveStatus.NOT_WHILE_ABOUT_TO_CRASH:
			{
				FlightScreenMessager.showMessage("Cannot launch vessel while about to crush", 3);
				return false;
			}
			case ClearToSaveStatus.NOT_WHILE_MOVING_OVER_SURFACE:
			{
				FlightScreenMessager.showMessage("Cannot launch vessel while moving on the surface", 3);
				return false;
			}
			case ClearToSaveStatus.NOT_WHILE_THROTTLED_UP:
			{
				FlightScreenMessager.showMessage("Cannot launch vessel while thottled up", 3);
				return false;
			}
			default:
				FlightScreenMessager.showMessage("Launching vessel...", 3);
				break;
			}
			return true;
		}
		
		public void TryRestoreVessel(Guid vid)
		{
			if(!CanRestore()) return;
			//get vessel
			StoredVessel stored_vessel;
			if(!stored_vessels.TryGetValue(vid, out stored_vessel)) return;
			//clean up
			stored_vessels.Remove(vid);
			//switch hangar state
			hangar_state = HangarState.Busy;
			//set restored vessel orbit
			get_launch_transform();
			PositionVessel(stored_vessel);
			//restore vessel
			stored_vessel.vessel.Load(FlightDriver.FlightStateCache.flightState);
			stored_vessel.vessel.LoadObjects();
			//get restored vessel from the world
			launched_vessel = stored_vessel.vessel.vesselRef;
			//transfer crew back to the launched vessel
			List<ProtoCrewMember> transfered_crew = new List<ProtoCrewMember>();
			foreach(Part p in vessel.parts)	transfered_crew.AddRange(delCrew(p, stored_vessel.crew));
			addCrew(launched_vessel, transfered_crew);
			//change volume and mass
			if(stored_vessels.Count < 1) 
			{
				used_volume  = 0;
				vessels_mass = 0;
			}
			else
			{
				used_volume  -= stored_vessel.metric.volume;
				vessels_mass -= stored_vessel.mass;
			}
			SetMass();
			//switch to restored vessel
			FlightGlobals.ForceSetActiveVessel(launched_vessel);
			Staging.beginFlight();
		}
		
		//called every frame while part collider is touching the trigger
		public void OnTriggerStay (Collider col) //see Unity docs
		{
			if(hangar_gates.GatesState != HangarGates.Opened
				||  col == null
				|| !col.CompareTag ("Untagged")
				||  col.gameObject.name == "MapOverlay collider"// kethane
				||  col.attachedRigidbody == null)
				return;
			//get part and try to store vessel
			Part p = col.attachedRigidbody.GetComponent<Part>();
			if(p == null || p.vessel == null) return;
			TryStoreVessel(p.vessel);
		}
		
		//called when part collider exits the trigger
		public void OnTriggerExit(Collider col)
		{
			if(col == null
				|| !col.CompareTag("Untagged")
				||  col.gameObject.name == "MapOverlay collider" // kethane
				||  col.attachedRigidbody == null)
				return;
			Part p = col.attachedRigidbody.GetComponent<Part>();
			if(p == null || p.vessel == null) return;
			if(probed_ids.ContainsKey(p.vessel.id)) probed_ids.Remove(p.vessel.id);
		}
		
		
		//events
		//open event
//		[KSPEvent (guiActive = true, guiName = "Open hangar", active = true)]
		public void Open()
		{
			hangar_gates.Open();
			Events["Open"].active = false;
			Events["Close"].active = true;
		}
	
		//close event
//		[KSPEvent (guiActive = true, guiName = "Close hangar", active = false)]
		public void Close()
		{
			hangar_gates.Close();
			Events["Open"].active = true;
			Events["Close"].active = false;
		}
		
		//prepare event
//		[KSPEvent (guiActive = true, guiName = "Prepare hangar", active = false)]
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
				sv.Save(stored_vessel_node);
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
					StoredVessel sv = new StoredVessel(vn);
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
			hangar_d = Utils.formatDimensions(hangar_metric.size);
			vessels_docked = String.Format ("{0}", stored_vessels.Count);
			total_m = Utils.formatMass(part.mass);
			crew_capacity = part.CrewCapacity.ToString();
		}
		
	}
}