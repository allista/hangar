//This code is partly based on the code from Extraplanetary Launchpads plugin. ExLaunchPad and Recycler classes.
//Thanks Taniwha, I've learnd many things from your code and from our conversation.
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
		public enum HangarState{Active,Inactive};
		
		//internal properties
		private BaseHangarAnimator hangar_gates;
		private float usefull_volume_ratio = 0.7f; //only 70% of the volume may be used by docking vessels
		private float crew_volume_ratio    = 0.3f; //only 30% of the remaining volume may be used for crew (i.e. V*(1-usefull_r)*crew_r)
		public float vessels_mass = -1f;
		public float used_volume  = -1f;
		
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
		[KSPField (isPersistant = true)]  public float base_mass = -1f;
		
		//vessels storage
		private VesselsPack<StoredVessel> stored_vessels = new VesselsPack<StoredVessel>();
		private Dictionary<Guid, bool> probed_ids = new Dictionary<Guid, bool>();
		
		//vessel spawn
		[KSPField (isPersistant = false)] public float  LaunchHeightOffset = 0.0f;
		[KSPField (isPersistant = false)] public string LaunchTransform;
		[KSPField (isPersistant = false)] public string HangarSpace;
		Transform launchTransform;
		Vessel launched_vessel;

		//in-editor vessel docking
		private Rect eWindowPos = new Rect(Screen.width/2-200, 100, 400, 100);
		private Vector2 scroll_view = Vector2.zero;
		private static List<string> vessel_dirs = new List<string>{"VAB", "SPH", "../Subassemblies"};
		private VesselsPack<PackedConstruct> packed_constructs = new VesselsPack<PackedConstruct>();
		private CraftBrowser vessel_selector;
		private VesselType vessel_type;
		private bool editing_hangar = false;
		
		//gui fields
		[KSPField (guiName = "Volume",        guiActiveEditor=true)] public string hangar_v;
		[KSPField (guiName = "Dimensions",    guiActiveEditor=true)] public string hangar_d;
		[KSPField (guiName = "Crew capacity", guiActiveEditor=true)] public string crew_capacity;
		[KSPField (guiName = "Stored mass",   guiActiveEditor=true)] public string stored_mass;
		[KSPField (guiName = "Hangar doors", guiActive = true)] public string doors;
		[KSPField (guiName = "Hangar state", guiActive = true)] public string state;
		
		
		#region for-GUI
		public List<StoredVessel> GetVessels() { return stored_vessels.Values; }
		
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
			//recalculate volume and mass
			Setup();
			//store packed constructs if any
			if(packed_constructs.Count > 0) StartCoroutine(store_constructs());
		}
		
		public void Setup(bool reset_mass = false)	
		{ RecalculateVolume(); SetPartMass(reset_mass); }

		public void SetPartMass(bool reset = false) 
		{
			//reset values if needed
			if(base_mass < 0 || reset) base_mass = part.mass;
			if(vessels_mass < 0 || reset)
			{
				vessels_mass = 0;
				foreach(StoredVessel sv in stored_vessels.Values) vessels_mass += sv.mass;
				foreach(PackedConstruct pc in packed_constructs.Values) vessels_mass += pc.metric.mass;
				stored_mass = Utils.formatMass(vessels_mass);
			}
			//set part.mass
			part.mass = base_mass+vessels_mass;
			//update PartResizer display
			var resizer = part.Modules.OfType<HangarPartResizer>().SingleOrDefault();
			if(resizer != null) resizer.UpdateGUI();
		}

		private void change_mass(float mass, float volume)
		{
			vessels_mass += mass;
			used_volume  += volume;
			if(used_volume < 0) used_volume = 0;
			if(vessels_mass < 0) vessels_mass = 0;
			stored_mass = Utils.formatMass(vessels_mass);
			SetPartMass();
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
			packed_constructs.space = hangar_metric;
			//calculate crew capacity from remaining volume
			if(!StaticCrewCapacity)
				part.CrewCapacity = (int)((part_metric.volume-hangar_metric.volume)*crew_volume_ratio/VolumePerKerbal);
			//calculate used_volume
			used_volume = 0;
			foreach(StoredVessel sv in stored_vessels.Values) used_volume += sv.volume;
			foreach(PackedConstruct pc in packed_constructs.Values) used_volume += pc.metric.volume;
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
				ScreenMessager.showMessage("Activate the hangar first", 3);
				return false;
			}
			//check self state first
			switch(FlightGlobals.ClearToSave()) 
			{
			case ClearToSaveStatus.NOT_WHILE_ABOUT_TO_CRASH:
			{
				ScreenMessager.showMessage("Cannot accept the vessel while about to crush", 3);
				return false;
			}
			default:
				break;
			}
			//always check relative velocity and acceleration
			Vector3 rv = vessel.GetObtVelocity()-vsl.GetObtVelocity();
			if(rv.magnitude > 1f) 
			{
				ScreenMessager.showMessage("Cannot accept a vessel with a relative speed higher than 1m/s", 3);
				return false;
			}
			Vector3 ra = vessel.acceleration - vsl.acceleration;
			if(ra.magnitude > 0.1)
			{
				ScreenMessager.showMessage("Cannot accept an accelerating vessel", 3);
				return false;
			}
			return true;
		}
		
		private StoredVessel try_store(Vessel vsl)
		{
			//check vessel crew
			if(vsl.GetCrewCount() > vessel.GetCrewCapacity()-vessel.GetCrewCount())
			{
				ScreenMessager.showMessage("Not enough space for the crew of a docking vessel", 3);
				return null;
			}
			//check vessel metrics
			get_launch_transform();
			StoredVessel sv = new StoredVessel(vsl);
			if(!sv.metric.FitsAligned(launchTransform, part.partTransform, hangar_metric))
			{
				ScreenMessager.showMessage("The vessel does not fit into this hangar", 3);
				return null;
			}
			if(!stored_vessels.Add(sv))
			{
				ScreenMessager.showMessage("There's no room in the hangar for this vessel", 3);
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
			if(FlightGlobals.ActiveVessel == vsl)
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
			change_mass(stored_vessel.mass, stored_vessel.volume);
			//destroy vessel
			vsl.Die();
			ScreenMessager.showMessage("Vessel has been docked inside the hangar", 3);
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
				Utils.Log("LaunchTransform not found. Using offset.");
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
				Vector3 bounds_offset = launchTransform.TransformDirection(new Vector3(-sv.metric.center.x, 
				                                                                       sv.metric.size.y,
				            														   -sv.metric.center.z));
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
				SetPartMass();
			}
			resourceTransferList.Clear();
		}
		#endregion
		
		private bool can_restore()
		{
			//if hangar is not ready, return
			if(hangar_state == HangarState.Inactive) 
			{
				ScreenMessager.showMessage("Activate the hangar first", 3);
				return false;
			}
			if(hangar_gates.GatesState != HangarGates.Opened) 
			{
				ScreenMessager.showMessage("Open hangar gates first", 3);
				return false;
			}
			//if something is docked to the hangar docking port (if its present)
			ModuleDockingNode dport = part.Modules.OfType<ModuleDockingNode>().SingleOrDefault();
			if(dport != null && dport.vesselInfo != null)
			{
				ScreenMessager.showMessage("Cannot launch a vessel while another is docked", 3);
				return false;
			}
			//if in orbit or on the ground and not moving
			switch(FlightGlobals.ClearToSave()) 
			{
				case ClearToSaveStatus.NOT_IN_ATMOSPHERE:
				{
					ScreenMessager.showMessage("Cannot launch a vessel while flying in atmosphere", 3);
					return false;
				}
				case ClearToSaveStatus.NOT_UNDER_ACCELERATION:
				{
					ScreenMessager.showMessage("Cannot launch a vessel hangar is under accelleration", 3);
					return false;
				}
				case ClearToSaveStatus.NOT_WHILE_ABOUT_TO_CRASH:
				{
					ScreenMessager.showMessage("Cannot launch a vessel while about to crush", 3);
					return false;
				}
				case ClearToSaveStatus.NOT_WHILE_MOVING_OVER_SURFACE:
				{
					ScreenMessager.showMessage("Cannot launch a vessel while moving on the surface", 3);
					return false;
				}
				case ClearToSaveStatus.NOT_WHILE_THROTTLED_UP:
				{
					ScreenMessager.showMessage("Cannot launch a vessel while thottled up", 3);
					return false;
				}
				default: break;
			}
			if(vessel.angularVelocity.magnitude > 0.003)
			{
				ScreenMessager.showMessage("Cannot launch a vessel while rotating", 3);
				return false;
			}
			return true;
		}
		
		public void TryRestoreVessel(StoredVessel stored_vessel)
		{
			if(!can_restore()) return;
			ScreenMessager.showMessage(string.Format("Launching {0}...", stored_vessel.vessel.vesselName), 3);
			//clean up
			stored_vessels.Remove(stored_vessel.vessel.vesselID);
			//switch hangar state
			hangar_state = HangarState.Inactive;
			//transfer resources
			transferResources(stored_vessel);
			//set restored vessel orbit
			get_launch_transform();
			position_vessel(stored_vessel);
			//restore vessel
			stored_vessel.Load();
			//get restored vessel from the world
			launched_vessel = stored_vessel.vessel.vesselRef;
			//transfer crew back to the launched vessel
			List<ProtoCrewMember> crew_to_transfer = CrewTransfer.delCrew(vessel, stored_vessel.crew);
			//change volume and mass
			change_mass(-stored_vessel.mass, -stored_vessel.volume);
			//switch to restored vessel
			FlightGlobals.ForceSetActiveVessel(launched_vessel);
			Staging.beginFlight();
			SetupVessel(new LaunchedVessel(stored_vessel, launched_vessel, crew_to_transfer));
		}
		#endregion


		#region EditHangarContents
		private void vessel_selected(string filename, string flagname)
		{
			EditorLogic EL = EditorLogic.fetch;
			if(EL == null) return;
			//load vessel config
			vessel_selector = null;
			PackedConstruct pc = new PackedConstruct(filename, flagname);
			//check if it's possible to launch such vessel
			bool cant_launch = false;
			PreFlightCheck preFlightCheck = new PreFlightCheck(new Callback(() => cant_launch = false), new Callback(() => cant_launch = true));
			preFlightCheck.AddTest(new PreFlightTests.ExperimentalPartsAvailable(pc.construct));
			preFlightCheck.AddTest(new PreFlightTests.CanAffordLaunchTest(pc.construct, Funding.Instance));
			preFlightCheck.RunTests(); if(cant_launch) return;
			//cleanup loaded parts
			pc.UnloadConstruct();
			//check if it can be stored in this hangar
			get_launch_transform();
			if(!pc.metric.FitsAligned(launchTransform, part.partTransform, hangar_metric))
			{
				ScreenMessager.showMessage("The vessel does not fit into this hangar", 3);
				return;
			}
			if(!packed_constructs.Add(pc))
			{
				ScreenMessager.showMessage("There's no room in the hangar for this vessel", 3);
				return;
			}
			//change hangar mass
			change_mass(pc.metric.mass, pc.metric.volume);
		}

		private void remove_construct(PackedConstruct pc)
		{
			change_mass(-pc.metric.mass, -pc.metric.volume);
			packed_constructs.Remove(pc.id);
		}

		private void selection_canceled() { vessel_selector = null; }

		private IEnumerator<YieldInstruction> store_constructs()
		{
			if(FlightGlobals.fetch == null || 
			   FlightGlobals.ActiveVessel == null) 
				yield break;
			//wait for hangar.vessel to be loaded
			VesselWaiter self = new VesselWaiter(vessel);
			while(!self.launched) yield return null;
			//create vessels from constructs and store them
			HangarState cur_state = hangar_state;
			Deactivate();
			foreach(PackedConstruct pc in packed_constructs.Values)
			{
				remove_construct(pc);
				get_launch_transform();
				pc.LoadConstruct();
				ShipConstruction.PutShipToGround(pc.construct, launchTransform);
				ShipConstruction.AssembleForLaunch(pc.construct, "Hangar", pc.flag, 
				                                   FlightDriver.FlightStateCache,
				                                   new VesselCrewManifest());
				VesselWaiter vsl = new VesselWaiter(FlightGlobals.Vessels[FlightGlobals.Vessels.Count - 1]);
				FlightGlobals.ForceSetActiveVessel(vsl.vessel);
				//wait for vsl to be launched
				while(!vsl.launched) yield return null;
				store_vessel(vsl.vessel);
			}
			stored_mass = Utils.formatMass(vessels_mass);
			if(cur_state == HangarState.Active) Activate();
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

		[KSPEvent (guiActiveEditor = true, guiName = "Edit contents", active = true)]
		public void EditHangar() { editing_hangar = true; }

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
	

		#region Callbacks
		//save the hangar
		public override void OnSave(ConfigNode node)
		{
			//hangar state
			node.AddValue("hangarState", hangar_state.ToString());
			//save stored vessels
			if(stored_vessels.Count > 0)
				stored_vessels.Save(node.AddNode("STORED_VESSELS"));
			if(packed_constructs.Count > 0)
				packed_constructs.Save(node.AddNode("PACKED_CONSTRUCTS"));
		}
		
		//load the hangar
		public override void OnLoad(ConfigNode node)
		{ 
			//hangar state
			if(node.HasValue("hangarState"))
				hangar_state = (HangarState)Enum.Parse(typeof(HangarState), node.GetValue("hangarState"));
			//restore stored vessels
			if(node.HasNode("STORED_VESSELS"))
				stored_vessels.Load(node.GetNode("STORED_VESSELS"));
			if(node.HasNode("PACKED_CONSTRUCTS"))
				packed_constructs.Load(node.GetNode("PACKED_CONSTRUCTS"));
		}
		
		//update labels
		public override void OnUpdate ()
		{
			doors = hangar_gates.GatesState.ToString();
			state = hangar_state.ToString();
		}

//		public override void OnFixedUpdate()
//		{ if(packed_constructs.Count > 0) store_constructs(); }

		#region OnGUI
		private void hangar_content_editor(int windowID)
		{
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			//VAB / SPH / SubAss selection
			GUILayout.FlexibleSpace ();
			for(var T = VesselType.VAB; T <= VesselType.SubAssembly; T++)
			{ if(GUILayout.Toggle(vessel_type == T, T.ToString (), GUILayout.Width (80))) vessel_type = T; }
			GUILayout.FlexibleSpace ();
			//Vessel selector
			if(GUILayout.Button("Select Vessel", Styles.normal_button, GUILayout.ExpandWidth(true))) 
			{
				Rect sWindowPos = new Rect(eWindowPos) { height = 500 };
				vessel_selector = 
					new CraftBrowser(sWindowPos, 
					                 vessel_dirs[(int)vessel_type],
					                 HighLogic.SaveFolder, "Select a ship to store",
					                 vessel_selected,
					                 selection_canceled,
					                 HighLogic.Skin,
					                 EditorLogic.ShipFileImage, true);
			}
			GUILayout.EndHorizontal();
			List<PackedConstruct> constructs = packed_constructs.Values;
			constructs.Sort((a, b) => a.name.CompareTo(b.name));
			scroll_view = GUILayout.BeginScrollView(scroll_view, GUILayout.Height(200), GUILayout.Width(400));
			GUILayout.BeginVertical();
			foreach(PackedConstruct pc in constructs)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label(string.Format("{0}: {1}", pc.name, Utils.formatMass(pc.metric.mass)), 
				                Styles.label, GUILayout.ExpandWidth(true));
				if(GUILayout.Button("X", Styles.red_button, GUILayout.Width(25))) remove_construct(pc);
				GUILayout.EndHorizontal();
			}
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
			if(GUILayout.Button("Close", Styles.normal_button, GUILayout.ExpandWidth(true))) editing_hangar = false;
			GUILayout.EndVertical();
			GUI.DragWindow(new Rect(0, 0, 500, 20));
		}

		public void OnGUI() 
		{ 
			if(!editing_hangar) return;
			if(Event.current.type != EventType.Layout) return;
			if(EditorLogic.fetch == null) return;
			Styles.InitSkin();
			GUI.skin = Styles.skin;
			Styles.InitGUI();
			if(vessel_selector == null) 
			{
				eWindowPos = GUILayout.Window(GetInstanceID(), eWindowPos,
							                  hangar_content_editor,
							                  "Choose vessel type",
							                  GUILayout.Width(400));
			}
			else vessel_selector.OnGUI();
		}
		#endregion
		#endregion
	}
}