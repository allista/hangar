//This code is partly based on the code from Extraplanetary Launchpads plugin. ExLaunchPad and Recycler classes.
//Thanks Taniwha, I've learnd many things from your code and from our conversation.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


namespace AtHangar
{
	public class StoredVessel
	{
		public ProtoVessel vessel;
		public Vector3 CoM;
		public Vector3 CoG; //center of geometry
		public float mass;
		public Metric metric;
		public List<ProtoCrewMember> crew;
		public int CrewCapacity;
		public VesselResources<ProtoVessel, ProtoPartSnapshot, ProtoPartResourceSnapshot> resources;
		
		public StoredVessel() {}
		
		public StoredVessel(Vessel vsl)
		{
			vessel = vsl.BackupVessel();
			metric = new Metric(vsl);
			CoM    = vsl.findLocalCenterOfMass();
			CoG    = metric.center;
			mass   = vsl.GetTotalMass();
			crew   = vsl.GetVesselCrew();
			CrewCapacity = vsl.GetCrewCapacity();
			resources = new VesselResources<ProtoVessel, ProtoPartSnapshot, ProtoPartResourceSnapshot>(vessel);
		}
		
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
			node.AddValue("CrewCapacity", CrewCapacity);
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
			CrewCapacity = int.Parse(node.GetValue("CrewCapacity"));
			resources = new VesselResources<ProtoVessel, ProtoPartSnapshot, ProtoPartResourceSnapshot>(vessel);
		}
	}
	
	
	public class LaunchedVessel
	{
		private List<ProtoCrewMember> crew;
		private StoredVessel sv;
		public Vessel vessel;
		
		public LaunchedVessel(StoredVessel sv, Vessel vsl, List<ProtoCrewMember> crew)
		{
			this.sv = sv;
			this.vessel = vsl;
			this.crew = crew;
		}
		
		public bool launched
		{
			get 
			{
				if(vessel.id != FlightGlobals.ActiveVessel.id) return false;
				else vessel = FlightGlobals.ActiveVessel;
				bool parts_inited = true;
				foreach(Part p in vessel.parts)
				{
					if(!p.started)
					{
						parts_inited = false;
						break;
					}
				}
				return parts_inited;
			}
		}
		
		public void transferCrew() { CrewTransfer.addCrew(vessel, crew); }
		
		public void tunePosition()
		{
			Vector3 dP = vessel.findLocalCenterOfMass() - sv.CoM;
			vessel.SetPosition(vessel.vesselTransform.TransformPoint(dP));
		}
	}
	
	
	//this module adds the ability to store a vessel in a packed state inside
	public class Hangar : PartModule
	{
		public enum HangarState{Active,Inactive};
		
		//internal properties
		private BaseHangarAnimator hangar_gates;
		private float usefull_volume_ratio = 0.7f; //only 70% of the volume may be used by docking vessels
		private float crew_volume_ratio    = 0.3f; //only 30% of the remaining volume may be used for crew (i.e. V*(1-usefull_r)*crew_r)
		
		public HangarGates gates_state { get { return hangar_gates.GatesState; } }
		public HangarState hangar_state { get; private set;}
		public Metric part_metric { get; private set;}
		public Metric hangar_metric { get; private set;}
		public float used_volume_frac { get { if(hangar_metric.Empty) return 0; else return used_volume/hangar_metric.volume; } }
		public VesselResources<Vessel, Part, PartResource> hangarResources { get; private set;}
		public List<ResourceManifest> resourceTransferList = new List<ResourceManifest>();
		
		//fields
		[KSPField (isPersistant = false)] public float VolumePerKerbal    = 3f; // m^3
		[KSPField (isPersistant = false)] public bool  StaticCrewCapacity = false;
		[KSPField (isPersistant = true)]  public float used_volume  = 0f;
		[KSPField (isPersistant = true)]  public float base_mass    = 0f;
		[KSPField (isPersistant = true)]  public float vessels_mass = 0f;
		
		//vessels storage
		private VesselsPack stored_vessels = new VesselsPack();
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
		
		
		#region for-GUI
		public List<StoredVessel> GetVessels() { return new List<StoredVessel>(stored_vessels.Values); }
		
		public StoredVessel GetVessel(Guid vid)
		{
			StoredVessel sv;
			if(!stored_vessels.TryGetValue(vid, out sv)) return null;
			return sv;
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
		#endregion
		
		
		#region Setup
		//all initialization goes here instead of the constructor as documented in Unity API
		private void update_resources()
		{ hangarResources = new VesselResources<Vessel, Part, PartResource>(vessel); }
		
		public override void OnAwake()
		{
			base.OnAwake ();
			usefull_volume_ratio = (float)Math.Pow(usefull_volume_ratio, 1/3f);
		}
		
		public override void OnStart(StartState state)
		{
			//base OnStart
			base.OnStart(state);
			if(state == StartState.None) return;
			//initialize resources
			if(state != StartState.Editor) update_resources();
			//recalculate volume and mass
			Setup(); 
			//initialize Animator
			part.force_activate();
            hangar_gates = part.Modules.OfType<BaseHangarAnimator>().SingleOrDefault();
			if (hangar_gates == null)
			{
                hangar_gates = new BaseHangarAnimator();
				Debug.Log("[Hangar] Using BaseHangarAnimator");
			}
			else
            {
                Events["Open"].guiActiveEditor = true;
                Events["Close"].guiActiveEditor = true;
            }
		}
		
		public void Setup()	{ RecalculateVolume(); SetMass(); }
		
		public void SetMass() 
		{
			if(base_mass == 0) base_mass = part.mass;
			part.mass = base_mass+vessels_mass; 
		}
		
		public void RecalculateVolume()
		{
			//recalculate part and hangar metrics
			part_metric = new Metric(part);
			if(HangarSpace != "") 
				hangar_metric = new Metric(part, HangarSpace);
			//if hangar metric is not provided, derive it from part metric
			if(hangar_metric == null || hangar_metric.Empty)
				hangar_metric = part_metric*usefull_volume_ratio;
			//setup vessels pack
			stored_vessels.space = hangar_metric;
			//calculate crew capacity from remaining volume
			if(!StaticCrewCapacity)
				part.CrewCapacity = (int)((part_metric.volume-hangar_metric.volume)*crew_volume_ratio/VolumePerKerbal);
			//display recalculated values
			crew_capacity = part.CrewCapacity.ToString();
			hangar_v = Utils.formatVolume(hangar_metric.volume);
			hangar_d = Utils.formatDimensions(hangar_metric.size);
		}
		#endregion
		
		
		#region Store
		//if a vessel can be stored in the hangar
		private bool can_store(Vessel vsl)
		{
			if(vsl == null || vsl == vessel || !vsl.enabled || vsl.isEVA) return false;
			//if hangar is not ready, return
			if(hangar_state == HangarState.Inactive) 
			{
				FlightScreenMessager.showMessage("Activate the hangar first", 3);
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
		
		private StoredVessel try_store(Vessel vsl)
		{
			//check vessel crew
			if(vsl.GetCrewCount() > vessel.GetCrewCapacity()-vessel.GetCrewCount())
			{
				FlightScreenMessager.showMessage("Not enough space for the crew of a docking vessel", 3);
				return null;
			}
			//check vessel metrics
			get_launch_transform();
			StoredVessel sv = new StoredVessel(vsl);
			if(!sv.metric.FitsAligned(launchTransform, part.partTransform, hangar_metric))
			{
				FlightScreenMessager.showMessage("The vessel does not fit into this hangar", 3);
				return null;
			}
			if(!stored_vessels.Add(sv))
			{
				FlightScreenMessager.showMessage("There's no room in the hangar for this vessel", 3);
				return null;
			}
			return sv;
		}
		
		//store vessel
		private void store_vessel(Vessel vsl)
		{
			//check momentary states
			if(!can_store(vsl)) return;
			//check if the vessel can be stored, if unknown, try to store
			bool storable;
			StoredVessel stored_vessel = new StoredVessel();
			if(!probed_ids.TryGetValue(vsl.id, out storable))
			{
				stored_vessel = try_store(vsl);
				storable = stored_vessel != null;
				probed_ids.Add(vsl.id, storable);
			}
			if(!storable) return;
			//switch to hangar vessel before storing
			if(FlightGlobals.ActiveVessel != vessel)
				FlightGlobals.ForceSetActiveVessel(vessel);
			//get vessel crew on board
			List<ProtoCrewMember> _crew = new List<ProtoCrewMember>(stored_vessel.crew);
			CrewTransfer.delCrew(vsl, _crew);
			vsl.DespawnCrew();
			//first of, add crew to the hangar if there's a place
			CrewTransfer.addCrew(part, _crew);
			//then add to other vessel parts if needed
			CrewTransfer.addCrew(vessel, _crew);
			//recalculate volume and mass
			used_volume  += stored_vessel.metric.volume;
			vessels_mass += stored_vessel.mass;
			SetMass();
			//destroy vessel
			vsl.Die();
			FlightScreenMessager.showMessage("Vessel has been docked inside the hangar", 3);
		}
		
		//called every frame while part collider is touching the trigger
		public void OnTriggerStay (Collider col) //see Unity docs
		{
			if(hangar_state != HangarState.Active
				||  col == null
				|| !col.CompareTag ("Untagged")
				||  col.gameObject.name == "MapOverlay collider"// kethane
				||  col.attachedRigidbody == null)
				return;
			//get part and try to store vessel
			Part p = col.attachedRigidbody.GetComponent<Part>();
			if(p == null || p.vessel == null) return;
			store_vessel(p.vessel);
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
		#endregion
		
		
		#region Restore
		#region Positioning
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
		private void position_vessel(StoredVessel sv)
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
			//position on a surface
			if(vessel.LandedOrSplashed)
			{
				//calculate launch offset from vessel bounds
				Vector3 bounds_offset = launchTransform.TransformDirection(sv.CoM - sv.CoG + Vector3.up*sv.metric.size.y);
				//set vessel's position
				Vector3d vpos = Vector3d.zero+launchTransform.position+bounds_offset;
				pv.longitude  = vessel.mainBody.GetLongitude(vpos);
				pv.latitude   = vessel.mainBody.GetLatitude(vpos);
				pv.altitude   = vessel.mainBody.GetAltitude(vpos);
			}
			else //set the new orbit
			{
				//calculate launch offset from vessel bounds
				Vector3 bounds_offset = launchTransform.TransformDirection(sv.CoM - sv.CoG + Vector3.up*sv.metric.extents.y);
				//set vessel's orbit
				Orbit horb = vessel.orbit;
				Orbit vorb = new Orbit();
				Vector3 d_pos = launchTransform.position-vessel.findWorldCenterOfMass()+bounds_offset;
				Vector3d vpos = horb.pos+new Vector3d(d_pos.x, d_pos.z, d_pos.y);
				vorb.UpdateFromStateVectors(vpos, horb.vel, horb.referenceBody, Planetarium.GetUniversalTime());
				pv.orbitSnapShot = new OrbitSnapshot(vorb);
			}
		}
		
		//static coroutine launched from a DontDestroyOnLoad sentinel object allows to execute code while the scene is switching
		static IEnumerator<YieldInstruction> setup_vessel(GameObject sentinel, LaunchedVessel lv)
		{
			while(!lv.launched) yield return null;
			lv.tunePosition();
			lv.transferCrew();
			//it seems you must give KSP a moment to sort it all out,
            //so delay the remaining steps of the transfer process. 
			//(from the CrewManifest: https://github.com/sarbian/CrewManifest/blob/master/CrewManifest/ManifestController.cs)
			yield return new WaitForSeconds(0.25f);
			lv.vessel.SpawnCrew();
			Destroy(sentinel);
		}
		
		public static void SetupVessel(LaunchedVessel lv)
		{
			GameObject obj = new GameObject();
			DontDestroyOnLoad(obj);
			obj.AddComponent<MonoBehaviour>().StartCoroutine(setup_vessel(obj, lv));
		}
		#endregion
		
		#region Resources
		public void prepareResourceList(StoredVessel sv)
		{
			if(resourceTransferList.Count > 0) return;
			foreach(var r in sv.resources.resourcesNames)
			{
				if(hangarResources.ResourceCapacity(r) == 0) continue;
				ResourceManifest rm = new ResourceManifest();
				rm.name          = r;
				rm.amount        = sv.resources.ResourceAmount(r);
				rm.capacity      = sv.resources.ResourceCapacity(r);
				rm.offset        = rm.amount;
				rm.host_amount   = hangarResources.ResourceAmount(r);
				rm.host_capacity = hangarResources.ResourceCapacity(r);
				rm.pool          = rm.host_amount + rm.offset;
				rm.minAmount     = Math.Max(0, rm.pool-rm.host_capacity);
				rm.maxAmount     = Math.Min(rm.pool, rm.capacity);
				resourceTransferList.Add(rm);
			}
		}
		
		public void updateResourceList()
		{
			update_resources();
			foreach(ResourceManifest rm in resourceTransferList)
			{
				rm.host_amount = hangarResources.ResourceAmount(rm.name);
				rm.pool        = rm.host_amount + rm.offset;
				rm.minAmount   = Math.Max(0, rm.pool-rm.host_capacity);
				rm.maxAmount   = Math.Min(rm.pool, rm.capacity);
			}
		}
		
		public void transferResources(StoredVessel sv)
		{
			if(resourceTransferList.Count == 0) return;
			foreach(var r in resourceTransferList)
			{
				//transfer resource between hangar and protovessel
				var a = hangarResources.TransferResource(r.name, r.offset-r.amount);
				a = r.amount-r.offset + a;
				var b = sv.resources.TransferResource(r.name, a);
				hangarResources.TransferResource(r.name, b);
				//update masses
				PartResourceDefinition res_def = PartResourceLibrary.Instance.GetDefinition(r.name);
				if(res_def.density == 0) continue;
				vessels_mass += (float)a*res_def.density;
				sv.mass += (float)a*res_def.density;
				SetMass();
			}
			resourceTransferList.Clear();
		}
		#endregion
		
		private bool can_restore()
		{
			//if hangar is not ready, return
			if(hangar_state == HangarState.Inactive) 
			{
				FlightScreenMessager.showMessage("Activate the hangar first", 3);
				return false;
			}
			if(hangar_gates.GatesState != HangarGates.Opened) 
			{
				FlightScreenMessager.showMessage("Open hangar gates first", 3);
				return false;
			}
			//if something is docked to the hangar docking port (if its present)
			ModuleDockingNode dport = part.Modules.OfType<ModuleDockingNode>().SingleOrDefault();
			if(dport != null && dport.vesselInfo != null)
			{
				FlightScreenMessager.showMessage("Cannot launch a vessel while another is docked", 3);
				return false;
			}
			//if in orbit or on the ground and not moving
			switch(FlightGlobals.ClearToSave()) 
			{
				case ClearToSaveStatus.NOT_IN_ATMOSPHERE:
				{
					FlightScreenMessager.showMessage("Cannot launch a vessel while flying in atmosphere", 3);
					return false;
				}
				case ClearToSaveStatus.NOT_UNDER_ACCELERATION:
				{
					FlightScreenMessager.showMessage("Cannot launch a vessel hangar is under accelleration", 3);
					return false;
				}
				case ClearToSaveStatus.NOT_WHILE_ABOUT_TO_CRASH:
				{
					FlightScreenMessager.showMessage("Cannot launch a vessel while about to crush", 3);
					return false;
				}
				case ClearToSaveStatus.NOT_WHILE_MOVING_OVER_SURFACE:
				{
					FlightScreenMessager.showMessage("Cannot launch a vessel while moving on the surface", 3);
					return false;
				}
				case ClearToSaveStatus.NOT_WHILE_THROTTLED_UP:
				{
					FlightScreenMessager.showMessage("Cannot launch a vessel while thottled up", 3);
					return false;
				}
				default: break;
			}
			if(vessel.angularVelocity.magnitude > 0.003)
			{
				FlightScreenMessager.showMessage("Cannot launch a vessel while rotating", 3);
				return false;
			}
			return true;
		}
		
		public void TryRestoreVessel(StoredVessel stored_vessel)
		{
			if(!can_restore()) return;
			FlightScreenMessager.showMessage(string.Format("Launching {0}...", stored_vessel.vessel.vesselName), 3);
			//switch hangar state
			hangar_state = HangarState.Inactive;
			//transfer resources
			transferResources(stored_vessel);
			//set restored vessel orbit
			get_launch_transform();
			position_vessel(stored_vessel);
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
			//clean up
			stored_vessels.Remove(stored_vessel.vessel.vesselID);
			//switch to restored vessel
			FlightGlobals.ForceSetActiveVessel(launched_vessel);
			Staging.beginFlight();
			SetupVessel(new LaunchedVessel(stored_vessel, launched_vessel, crew_to_transfer));
		}
		#endregion
		
		
		#region Events&Actions
		//events
		[KSPEvent (guiActiveEditor = true, guiName = "Open gates", active = true)]
		public void Open() 
		{ 
			hangar_gates.Open();
			Events["Open"].active = false;
			Events["Close"].active = true;
		}
	
		[KSPEvent (guiActiveEditor = true, guiName = "Close gates", active = false)]
		public void Close()	
		{ 
			hangar_gates.Close(); 
			Events["Open"].active = true;
			Events["Close"].active = false;
		}
		
		public void Activate() { hangar_state = HangarState.Active;	}
		
		public void Deactivate() { hangar_state = HangarState.Inactive;	probed_ids.Clear(); }
		
		public void Toggle()
		{
			if(hangar_state == HangarState.Active) Deactivate();
			else Activate();
		}
		
		
		//actions
		[KSPAction("Open gates")]
        public void OpenGatesAction(KSPActionParam param) { Open(); }
		
		[KSPAction("Close gates")]
        public void CloseGatesAction(KSPActionParam param) { Close(); }
		
		[KSPAction("Toggle gates")]
        public void ToggleGatesAction(KSPActionParam param) { hangar_gates.Toggle(); }
		
		[KSPAction("Activate hangar")]
        public void ActivateStateAction(KSPActionParam param) { Activate(); }
		
		[KSPAction("Deactivate hangar")]
        public void DeactivateStateAction(KSPActionParam param) { Deactivate(); }
		
		[KSPAction("Toggle hangar")]
        public void ToggleStateAction(KSPActionParam param) { Toggle(); }
		#endregion
	
		
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
				List<StoredVessel> vessels = new List<StoredVessel>();
				foreach(ConfigNode vn in vessels_node.nodes)
					vessels.Add(new StoredVessel(vn));
				stored_vessels.Set(vessels);
			}
		}
		
		//update labels
		public override void OnUpdate ()
		{
			doors = hangar_gates.GatesState.ToString();
			state = hangar_state.ToString();
		}
		
	}
}