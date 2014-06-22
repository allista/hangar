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
			public List<ProtoCrewMember> crew;
			public Metric metric;
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
		Vector3 launch_offset;
		public DockedVesselInfo dock_info;
		Part launched_root;
		
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
		
		
		private void onVesselSituationChange(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> vs)
		{ if (vs.host != vessel) return; }

		void onVesselWasModified(Vessel v)
		{
			if (v == vessel) 
			{
				if(launched_root != null && launched_root.vessel != vessel) 
				{
					launched_root = null;
					ReleaseVessel();
				}
			}
		}
		
		
		//all initialization goes here instead of the constructor as documented in Unity API
		public override void OnAwake()
		{
			base.OnAwake ();
			usefull_volume_ratio = (float)Math.Pow(usefull_volume_ratio, 1/3f);
			GameEvents.onVesselSituationChange.Add (onVesselSituationChange);
			GameEvents.onVesselWasModified.Add (onVesselWasModified);
		}
		
		void OnDestroy ()
		{
			GameEvents.onVesselSituationChange.Remove (onVesselSituationChange);
			GameEvents.onVesselWasModified.Remove (onVesselWasModified);
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
//            else
//            {
//                Events["Open"].guiActiveEditor = true;
//                Events["Close"].guiActiveEditor = true;
//            }
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
			if(hangar_metric == null || hangar_metric.empy())
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
		
		//dock launched vessel to the hangar to prevent its krakenization 
		private void couple_vessel()
		{
			launched_root = launched_vessel.rootPart;
			Debug.Log(string.Format("Vessel root: {0}, parts count: {1}", launched_root, launched_vessel.parts.Count));
			dock_info = new DockedVesselInfo ();
			Debug.Log("5.1");
			dock_info.name = launched_vessel.vesselName;
			Debug.Log("5.2");
			dock_info.vesselType = launched_vessel.vesselType;
			Debug.Log("5.3");
			dock_info.rootPartUId = launched_root.flightID;
			Debug.Log(string.Format("5.4: parts {0}", launched_vessel.parts.Count));
			launched_root.Couple(part);
			Debug.Log(string.Format("5.5: parts {0}", launched_vessel.parts.Count));
			if (vessel != FlightGlobals.ActiveVessel)
				FlightGlobals.ForceSetActiveVessel(vessel);
			Events["ReleaseVessel"].active = true;
		}
		
		
		//set vessel orbit, transform, coordinates
		private void PositionVessel(ProtoVessel pv)
		{
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
			//surface
			if(vessel.LandedOrSplashed)
			{
				Vector3d v   = Vector3d.zero+launchTransform.position;
				pv.longitude = vessel.mainBody.GetLongitude(v);
				pv.latitude  = vessel.mainBody.GetLatitude(v);
				pv.altitude  = vessel.mainBody.GetAltitude(v);
			}
			else //setup new orbit
			{
				Orbit horb = vessel.orbit;
				Orbit vorb = new Orbit();
				Vector3 d_pos = launchTransform.position-vessel.findWorldCenterOfMass();
				Vector3d vpos = horb.pos+new Vector3d(d_pos.x, d_pos.z, d_pos.y);
				vorb.UpdateFromStateVectors(vpos, horb.vel, horb.referenceBody, Planetarium.GetUniversalTime());
				pv.orbitSnapShot = new OrbitSnapshot(vorb);
			}
		}
		
		private void PositionVessel(Vessel vsl)
		{
			//state
			vsl.Splashed  = false;//vessel.Landed;
			vsl.Landed    = false;//vessel.Splashed;
			//rotation
			//surface
			if(vessel.LandedOrSplashed)
			{
				Vector3 v  = launchTransform.position;
				v = new Vector3d(v.x, v.y, v.z);
				vsl.longitude = vessel.mainBody.GetLongitude(v);
				vsl.latitude  = vessel.mainBody.GetLatitude(v);
				vsl.altitude  = vessel.mainBody.GetAltitude(v);
			}
			else
			{
				//orbit
				vsl.orbitDriver.SetOrbitMode(OrbitDriver.UpdateMode.UPDATE);
				vsl.orbit.UpdateFromOrbitAtUT(vessel.orbit, Planetarium.GetUniversalTime(), vessel.mainBody);
			}
		}
		
		
		//position launched vessel part by part
		private IEnumerator<YieldInstruction> capture_vessel()
		{
			Debug.Log(string.Format("1: parts {0}", launched_vessel.parts.Count));
			while(true) 
			{
				bool partsInitialized = true;
				Debug.Log(string.Format("2.1: parts {0}", launched_vessel.parts.Count));
				foreach (Part p in launched_vessel.parts) 
				{
					Debug.Log(string.Format("2.2: {0}", p.name));
					if (!p.started)
					{
						Debug.Log("2.2.1");
						partsInitialized = false;
						break;
					}
				}
				Debug.Log("2.3");
				launched_vessel.SetPosition(launch_offset);
				launched_vessel.SetRotation(launchTransform.rotation);
				Debug.Log(string.Format("2.4: parts {0}", launched_vessel.parts.Count));
				if(partsInitialized) break;
				OrbitPhysicsManager.HoldVesselUnpack(2);
				Debug.Log(string.Format("2.5: parts {0}", launched_vessel.parts.Count));
				yield return null;
			}
			Debug.Log(string.Format("3: parts {0}", launched_vessel.parts.Count));
			FlightGlobals.overrideOrbit = false;
			Debug.Log(string.Format("4: parts {0}", launched_vessel.parts.Count));
			launched_vessel.GoOffRails ();
			Debug.Log(string.Format("5: parts {0}", launched_vessel.parts.Count));
			couple_vessel();
			Debug.Log(string.Format("6: parts {0}", launched_vessel.parts.Count));
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
			if(!metric.fits(hangar_metric))
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
			used_volume += Metric.Volume(vsl);
			vessels_mass += vsl.GetTotalMass();
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
//			Events["Prepare"].active = true;
			//set restored vessel orbit
			get_launch_transform();
			PositionVessel(stored_vessel.vessel);
			//restore vessel
			stored_vessel.vessel.Load(FlightDriver.FlightStateCache.flightState);
			stored_vessel.vessel.LoadObjects();
			//get restored vessel from the world
			launched_vessel = stored_vessel.vessel.vesselRef;
//			launch_offset = launchTransform.position;//+launchTransform.up*(new Metric(launched_vessel.parts)).size.y/2f;
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
				used_volume  -= Metric.Volume(launched_vessel);
				vessels_mass -= launched_vessel.GetTotalMass();
			}
			SetMass();
			//switch to restored vessel
//			launched_vessel.Landed = launched_vessel.Splashed = false;
//			FlightGlobals.overrideOrbit = true;
			FlightGlobals.ForceSetActiveVessel(launched_vessel);
			Staging.beginFlight();
//			StartCoroutine(capture_vessel());
			Debug.Log(string.Format("[Hangar] actual orb position: {0}", launched_vessel.orbit.pos));
			Debug.Log(string.Format("[Hangar] actual position: {0}", launched_vessel.vesselTransform.position));
			Debug.Log(string.Format("[Hangar] actual rotation: {0}", launched_vessel.vesselTransform.rotation.eulerAngles));
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
		
		//release docked vessel
//		[KSPEvent (guiActive = true, guiName = "Release vessel", active = false)]
		public void ReleaseVessel()
		{
			if(launched_root != null) 
			{
				launched_root.Undock(dock_info);
				Vessel vsl = FlightGlobals.Vessels[FlightGlobals.Vessels.Count - 1];
				FlightGlobals.ForceSetActiveVessel(vsl);
			}
			dock_info = null;
			launched_vessel = null;
			launched_root = null;
			Events["ReleaseVessel"].active = false;
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
			hangar_d = Utils.formatDimensions(hangar_metric.size);
			vessels_docked = String.Format ("{0}", stored_vessels.Count);
			total_m = Utils.formatMass(part.mass);
			crew_capacity = part.CrewCapacity.ToString();
		}
		
	}
}