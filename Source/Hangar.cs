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
				node.AddValue("CoM", ConfigNode.WriteVector(CoM));
				node.AddValue("CoG", ConfigNode.WriteVector(CoG));
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
		//fields
		[KSPField (isPersistant = false)] public float VolumePerKerbal = 3f; //m^3
		[KSPField (isPersistant = false)] public bool StaticCrewCapacity = false;
		[KSPField (isPersistant = true)] public float used_volume  = 0f;
		[KSPField (isPersistant = true)] public float base_mass    = 0f;
		[KSPField (isPersistant = true)] public float vessels_mass = 0f;
		
		//vessels storage
		private Dictionary<Guid, StoredVessel> stored_vessels = new Dictionary<Guid, StoredVessel>();
		private Dictionary<Guid, bool> probed_ids = new Dictionary<Guid, bool>();
		
		//vessel spawn
		[KSPField (isPersistant = false)] public float  LaunchHeightOffset = 0.0f;
		[KSPField (isPersistant = false)] public string LaunchTransform;
		[KSPField (isPersistant = false)] public string HangarSpace;
		Transform launchTransform;
		Vessel launched_vessel;
		
		//gui fields
		[KSPField (guiName = "Volume", guiActiveEditor=true)] public string hangar_v;
		[KSPField (guiName = "Dimensions", guiActiveEditor=true)] public string hangar_d;
		[KSPField (guiName = "Crew capacity", guiActiveEditor=true)] public string crew_capacity;
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
				part.CrewCapacity = (int)((part_metric.volume-hangar_metric.volume)*crew_volume_ratio/VolumePerKerbal);
				crew_capacity = part.CrewCapacity.ToString();
			}
			//calculate hangar volume
			hangar_v = Utils.formatVolume(hangar_metric.volume);
			hangar_d = Utils.formatDimensions(hangar_metric.size);
		}
		
		//if a vessel can be stored in the hangar
		private bool CanStoreNow(Vessel vsl)
		{
			if(vsl == null || vsl == vessel || !vsl.enabled) return false;
			//if hangar is not ready, return
			if(hangar_state == HangarState.Busy) 
			{
				FlightScreenMessager.showMessage("Prepare hangar first", 3);
				return false;
			}
			//check self state first
			switch(FlightGlobals.ClearToSave()) 
			{
			case ClearToSaveStatus.NOT_WHILE_ABOUT_TO_CRASH:
			{
				FlightScreenMessager.showMessage("Cannot accept the vessel while about to crush", 3);
				return false;
			}
			default:
				break;
			}
			//always check relative velocity and acceleration
			Vector3 rv = vessel.GetObtVelocity()-vsl.GetObtVelocity();
			if(rv.magnitude > 1f) 
			{
				FlightScreenMessager.showMessage("Cannot accept a vessel with a relative speed higher than 1m/s", 3);
				return false;
			}
			Vector3 ra = vessel.acceleration - vsl.acceleration;
			if(ra.magnitude > 0.1)
			{
				FlightScreenMessager.showMessage("Cannot accept an accelerating vessel", 3);
				return false;
			}
			return true;
		}
		
		private bool CanStore(Vessel vsl)
		{
			//check if the vessel is EVA. Maybe get EVA on board too?
			if(vsl.isEVA) return false;
			//check vessel crew
			if(vsl.GetCrewCount() > vessel.GetCrewCapacity()-vessel.GetCrewCount())
			{
				FlightScreenMessager.showMessage("Not enough space for the crew of a docking vessel", 3);
				return false;
			}
			//check vessel metrics
			get_launch_transform();
			Metric metric = new Metric(vsl);
			if(metric.volume > hangar_metric.volume-used_volume) 
			{
				FlightScreenMessager.showMessage("There's no room in the hangar for this vessel", 3);
				return false;
			}
			if(!metric.FitsAligned(launchTransform, part.partTransform, hangar_metric))
			{
				FlightScreenMessager.showMessage("The vessel does not fit into this hangar", 3);
				return false;
			}	
			return true;
		}
		
		//store vessel
		private void TryStoreVessel(Vessel vsl)
		{
			//check momentary states
			if(!CanStoreNow(vsl)) return;
			//check stored value; if not found, calculate and store
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
			CrewTransfer.delCrew(vsl, _crew);
			vsl.DespawnCrew();
			//first of, add crew to the hangar if there's a place
			CrewTransfer.addCrew(part, _crew);
			//then add to other vessel parts if needed
			CrewTransfer.addCrew(vessel, _crew);
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
			//it is essential to use BackupVessel() instead of vessel.protoVessel, 
			//because in general the latter does not store the current flight state of the vessel
			ProtoVessel hpv = vessel.BackupVessel();
			Quaternion proto_rot  = hpv.rotation;
			Quaternion hangar_rot = vessel.vesselTransform.rotation;
			//rotate launchTransform.rotation to protovessel's reference frame
			pv.rotation = proto_rot*hangar_rot.Inverse()*launchTransform.rotation;
			//calculate launch offset from vessel bounds
			Vector3 bounds_offset = sv.CoM - sv.CoG + launchTransform.up*sv.metric.bounds.extents.y;
//			Debug.Log(string.Format("\nCoM: {0}\nCoG: {1}\ndC: {2}", sv.CoM, sv.CoG, Vector3d.zero+sv.CoM-sv.CoG));
			//position on a surface
			if(vessel.LandedOrSplashed)
			{
				Vector3d vpos = Vector3d.zero+launchTransform.position+bounds_offset;
//				Debug.Log(string.Format("\nvessel position: {3}\nlaunch position: {0}\noffset: {1}\nsum: {2}", 
//				                        launchTransform.position, bounds_offset, vpos, vessel.vesselTransform.position));
				pv.longitude  = vessel.mainBody.GetLongitude(vpos);
				pv.latitude   = vessel.mainBody.GetLatitude(vpos);
				pv.altitude   = vessel.mainBody.GetAltitude(vpos);
			}
			else //set the new orbit
			{
				Orbit horb = vessel.orbit;
				Orbit vorb = new Orbit();
				Vector3 d_pos = launchTransform.position-vessel.findWorldCenterOfMass()+bounds_offset;
				Vector3d vpos = horb.pos+new Vector3d(d_pos.x, d_pos.z, d_pos.y);
				vorb.UpdateFromStateVectors(vpos, horb.vel, horb.referenceBody, Planetarium.GetUniversalTime());
				pv.orbitSnapShot = new OrbitSnapshot(vorb);
			}
		}
		
		//static coroutine launched from a DontDestroyOnLoad sentinel object allows to execute code while the scene is switching
		static IEnumerator<YieldInstruction> transfer_crew(GameObject sentinel, Vessel vsl, List<ProtoCrewMember> crew)
		{
			Vessel _vsl = null;
			while(true)
			{
				if(_vsl == null)
				{
					if(vsl.id != FlightGlobals.ActiveVessel.id) 
					{
						yield return null;
						continue;
					}
					else _vsl = FlightGlobals.ActiveVessel;
				}
				bool parts_inited = true;
				foreach(Part p in _vsl.parts)
				{
					if(!p.started)
					{
						parts_inited = false;
						break;
					}
				}
				if(parts_inited) break;
				yield return null;
			}
			CrewTransfer.addCrew(vsl, crew);
			//it seems you must give KSP a moment to sort it all out,
            //so delay the remaining steps of the transfer process. 
			//(from the CrewManifest: https://github.com/sarbian/CrewManifest/blob/master/CrewManifest/ManifestController.cs)
			yield return new WaitForSeconds(0.25f);
			_vsl.SpawnCrew();
			Destroy(sentinel);
		}
		
		public static void TransferCrew(Vessel vsl, List<ProtoCrewMember> crew)
		{
			GameObject obj = new GameObject();
			DontDestroyOnLoad(obj);
			obj.AddComponent<MonoBehaviour>().StartCoroutine(transfer_crew(obj, vsl, crew));
		}
		
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
			if(vessel.angularVelocity.magnitude > 0.003)
			{
				FlightScreenMessager.showMessage("Cannot launch vessel while rotating", 3);
				return false;
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
			List<ProtoCrewMember> crew_to_transfer = CrewTransfer.delCrew(vessel, stored_vessel.crew);
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
			if(crew_to_transfer.Count > 0)
				TransferCrew(launched_vessel, crew_to_transfer);
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
		
		
		//open event
		public void Open() { hangar_gates.Open(); }
	
		//close event
		public void Close()	{ hangar_gates.Close(); }
		
		//prepare event
		public void Prepare() { hangar_state = HangarState.Ready;	}
		
		
		//actions
		[KSPAction("Open hangar")]
        public void OpenHangarAction(KSPActionParam param) { Open(); }
		
		[KSPAction("Close hangar")]
        public void CloseHangarAction(KSPActionParam param) { Close(); }
		
		[KSPAction("Toggle hangar")]
        public void ToggleHangarAction(KSPActionParam param) { hangar_gates.Toggle(); }
		
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
//			used_v = Utils.formatVolume(used_volume);
			hangar_d = Utils.formatDimensions(hangar_metric.size);
//			vessels_docked = String.Format ("{0}", stored_vessels.Count);
//			total_m = Utils.formatMass(part.mass);
			crew_capacity = part.CrewCapacity.ToString();
		}
		
	}
}