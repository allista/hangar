//This code is partly based on the code from Extraplanetary Launchpads plugin. ExLaunchPad and Recycler classes.
//Thanks Taniwha, I've learnd many things from your code and from our conversation.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AtHangar
{
	//this module adds the ability to store a vessel in a packed state inside
	public class Hangar : PartModule, IPartCostModifier, IControllableModule
	{
		public enum HangarState { Active, Inactive }

		//internal properties
		const float crew_volume_ratio = 0.3f; //only 30% of the remaining volume may be used for crew (i.e. V*(1-usefull_r)*crew_r)
		float usefull_volume_ratio = 0.7f;    //only 70% of the volume may be used by docking vessels
		public float vessels_mass = -1f;
		public float vessels_cost = -1f;
		public float used_volume  = -1f;
		BaseHangarAnimator hangar_gates;
		
		public AnimatorState gates_state { get { return hangar_gates.State; } }
		public HangarState hangar_state { get; private set;}
		public Metric part_metric { get; private set;}
		public Metric hangar_metric { get; private set;}
		public float used_volume_frac { get { if(hangar_metric.Empty) return 0; return used_volume/hangar_metric.volume; } }
		public VesselResources<Vessel, Part, PartResource> hangarResources { get; private set; }
		public List<ResourceManifest> resourceTransferList = new List<ResourceManifest>();
		
		//fields
		[KSPField (isPersistant = false)] public string AnimatorID;
		[KSPField (isPersistant = false)] public float VolumePerKerbal = 3f; // m^3
		[KSPField (isPersistant = false)] public bool  StaticCrewCapacity = false;
		[KSPField (isPersistant = true)]  public float base_mass = -1f;
		
		//vessels storage
		VesselsPack<StoredVessel> stored_vessels = new VesselsPack<StoredVessel>();
		Dictionary<Guid, bool> probed_ids = new Dictionary<Guid, bool>();
		
		//vessel spawn
		[KSPField (isPersistant = false)] public float  LaunchHeightOffset;
		[KSPField (isPersistant = false)] public string LaunchTransform;
		[KSPField (isPersistant = false)] public string HangarSpace;
		Transform launchTransform;
		Vessel launched_vessel;

		//in-editor vessel docking
		static readonly string eLock = "Hangar.EditHangarContents";
		static readonly List<string> vessel_dirs = new List<string>{"VAB", "SPH", "../Subassemblies"};
		Rect eWindowPos = new Rect(Screen.width/2-200, 100, 400, 100);
		Vector2 scroll_view = Vector2.zero;
		VesselsPack<PackedConstruct> packed_constructs = new VesselsPack<PackedConstruct>();
		CraftBrowser vessel_selector;
		VesselType vessel_type;
		bool editing_hangar = false;
		
		//gui fields
		[KSPField (guiName = "Volume",        guiActiveEditor=true)] public string hangar_v;
		[KSPField (guiName = "Dimensions",    guiActiveEditor=true)] public string hangar_d;
		[KSPField (guiName = "Crew Capacity", guiActiveEditor=true)] public string crew_capacity;
		[KSPField (guiName = "Stored Mass",   guiActiveEditor=true)] public string stored_mass;
		[KSPField (guiName = "Stored Cost",   guiActiveEditor=true)] public string stored_cost;
		[KSPField (guiName = "Hangar Doors",  guiActive = true)] public string doors;
		[KSPField (guiName = "Hangar State",  guiActive = true)] public string state;
		
		
		#region for-GUI
		public List<StoredVessel> GetVessels() { return stored_vessels.Values; }
		
		public StoredVessel GetVessel(Guid vid)
		{
			StoredVessel sv;
			return stored_vessels.TryGetValue(vid, out sv)? sv : null;
		}
		
		public void UpdateMenus(bool visible)
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
		void update_resources()
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
			hangar_gates = part.Modules.OfType<BaseHangarAnimator>().FirstOrDefault(m => m.AnimatorID == AnimatorID);
			if (hangar_gates == null)
			{
                hangar_gates = new BaseHangarAnimator();
				Utils.Log("Using BaseHangarAnimator");
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
			//set vessel type
			if(EditorLogic.fetch != null)
				vessel_type = EditorLogic.fetch.editorType == EditorLogic.EditorMode.SPH ? VesselType.SPH : VesselType.VAB;
		}
		
		public void Setup(bool reset = false)	
		{
			//recalculate part and hangar metrics
			part_metric = new Metric(part);
			if(HangarSpace != "") 
				hangar_metric = new Metric(part, HangarSpace);
			else hangar_metric = null;
			//if hangar metric is not provided, derive it from part metric
			if(hangar_metric == null || hangar_metric.Empty)
				hangar_metric = part_metric*usefull_volume_ratio;
			//setup vessels pack
			stored_vessels.space = hangar_metric;
			packed_constructs.space = hangar_metric;
			//display recalculated values
			hangar_v  = Utils.formatVolume(hangar_metric.volume);
			hangar_d  = Utils.formatDimensions(hangar_metric.size);
			//now recalculate used volume
			if(reset)
			{   //if resetting, try to repack vessels on resize
				List<PackedConstruct> constructs = packed_constructs.Values;
				packed_constructs.Clear();
				foreach(PackedConstruct pc in constructs)
					try_store_construct(pc);
				//no need to change_part_params as set_params is called later
			}
			//calculate used_volume
			used_volume = 0;
			foreach(StoredVessel sv in stored_vessels.Values) used_volume += sv.volume;
			foreach(PackedConstruct pc in packed_constructs.Values) used_volume += pc.metric.volume;
			//then set other part parameters
			set_part_params(reset);
		}

		void set_part_params(bool reset = false) 
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
			if(vessels_cost < 0 || reset)
			{
				vessels_cost = 0;
				foreach(StoredVessel sv in stored_vessels.Values) vessels_cost += sv.cost;
				foreach(PackedConstruct pc in packed_constructs.Values) vessels_cost += pc.metric.cost;
				stored_cost = vessels_cost.ToString();
			}
			//set part mass
			part.mass = base_mass+vessels_mass;
			//calculate crew capacity from remaining volume
			if(!StaticCrewCapacity)
				part.CrewCapacity = (int)((part_metric.volume-hangar_metric.volume)*crew_volume_ratio/VolumePerKerbal);
			crew_capacity = part.CrewCapacity.ToString();
			//update Editor counters and all other that listen
			if(EditorLogic.fetch != null) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		void change_part_params(Metric delta, float k = 1f)
		{
			vessels_mass += k*delta.mass;
			vessels_cost += k*delta.cost;
			used_volume  += k*delta.volume;
			if(used_volume < 0) used_volume = 0;
			if(vessels_mass < 0) vessels_mass = 0;
			if(vessels_cost < 0) vessels_cost = 0;
			stored_mass = Utils.formatMass(vessels_mass);
			stored_cost = vessels_cost.ToString();
			set_part_params();
		}

		public float GetModuleCost() { return vessels_cost; }
		#endregion
		
		
		#region Store
		//if a vessel can be stored in the hangar
		bool can_store(Vessel vsl)
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
		
		StoredVessel try_store(Vessel vsl)
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
		void store_vessel(Vessel vsl, bool perform_checks = true)
		{
			StoredVessel stored_vessel = new StoredVessel();
			if(perform_checks) //for normal operation
			{
				//check momentary states
				if(!can_store(vsl))
					return;
				//check if the vessel can be stored, if unknown, try to store
				bool storable;
				if(!probed_ids.TryGetValue(vsl.id, out storable))
				{
					stored_vessel = try_store(vsl);
					storable = stored_vessel != null;
					probed_ids.Add(vsl.id, storable);
				}
				if(!storable) return;
			}
			else //for storing packed constructs upon hangar launch
			{
				stored_vessel = new StoredVessel(vsl);
				stored_vessels.ForceAdd(stored_vessel);
			}
			//get vessel crew on board
			List<ProtoCrewMember> _crew = new List<ProtoCrewMember>(stored_vessel.crew);
			CrewTransfer.delCrew(vsl, _crew);
			vsl.DespawnCrew();
			//first of, add crew to the hangar if there's a place
			CrewTransfer.addCrew(part, _crew);
			//then add to other vessel parts if needed
			CrewTransfer.addCrew(vessel, _crew);
			//recalculate volume and mass
			change_part_params(stored_vessel.metric);
			//switch to hangar vessel before storing
			if(FlightGlobals.ActiveVessel.id == vsl.id)
				FlightGlobals.ForceSetActiveVessel(vessel);
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
		Transform get_launch_transform()
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
		void position_vessel(StoredVessel sv)
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
				Vector3 bounds_offset = launchTransform.TransformDirection(-sv.CoG);
				//set vessel's position
				Vector3d vpos = Vector3d.zero+launchTransform.position+bounds_offset;
				pv.longitude  = vessel.mainBody.GetLongitude(vpos);
				pv.latitude   = vessel.mainBody.GetLatitude(vpos);
				pv.altitude   = vessel.mainBody.GetAltitude(vpos);
			}
			else //set the new orbit
			{
				//calculate launch offset from vessel bounds
				Vector3 bounds_offset = launchTransform.TransformDirection(sv.CoM - sv.CoG);
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
		static IEnumerator<YieldInstruction> setup_vessel(UnityEngine.Object sentinel, LaunchedVessel lv)
		{
			while(!lv.launched) yield return new WaitForFixedUpdate();
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
				float dM = (float)a*res_def.density;
				float dC = (float)a*res_def.unitCost;
				vessels_mass += dM;
				vessels_cost += dC;
				sv.mass += dM;
				sv.cost += dC;
				set_part_params();
			}
			resourceTransferList.Clear();
		}
		#endregion
		
		bool can_restore()
		{
			//if hangar is not ready, return
			if(hangar_state == HangarState.Inactive) 
			{
				ScreenMessager.showMessage("Activate the hangar first", 3);
				return false;
			}
			if(hangar_gates.State != AnimatorState.Opened) 
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
					ScreenMessager.showMessage("Cannot launch a vessel while moving over the surface", 3);
					return false;
				}
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
			change_part_params(stored_vessel.metric, -1f);
			//switch to restored vessel
			FlightGlobals.ForceSetActiveVessel(launched_vessel);
			SetupVessel(new LaunchedVessel(stored_vessel, launched_vessel, crew_to_transfer));
		}
		#endregion


		#region EditHangarContents
		bool try_store_construct(PackedConstruct pc)
		{
			get_launch_transform();
			if(!pc.metric.FitsAligned(launchTransform, part.partTransform, hangar_metric))
			{
				ScreenMessager.showMessage(string.Format("{0} does not fit into this hangar", pc.name), 3);
				return false;
			}
			if(!packed_constructs.Add(pc))
			{
				ScreenMessager.showMessage(string.Format("There's no room in the hangar for {0}", pc.name), 3);
				return false;
			}
			return true;
		}

		void vessel_selected(string filename, string flagname)
		{
			EditorLogic EL = EditorLogic.fetch;
			if(EL == null) return;
			//load vessel config
			vessel_selector = null;
			PackedConstruct pc = new PackedConstruct(filename, flagname);
			//check if the construct contains launch clamps
			if(Utils.HasLaunchClamp(pc.construct))
			{
				ScreenMessager.showMessage(string.Format("{0} has launch clamps. Remove them before storing.", pc.name), 3);
				pc.UnloadConstruct();
				return;
			}
			//check if it's possible to launch such vessel
			bool cant_launch = false;
			PreFlightCheck preFlightCheck = new PreFlightCheck(new Callback(() => cant_launch = false), new Callback(() => cant_launch = true));
			preFlightCheck.AddTest(new PreFlightTests.ExperimentalPartsAvailable(pc.construct));
			preFlightCheck.RunTests(); 
			pc.UnloadConstruct();
			//cleanup loaded parts and try to store construct
			if(cant_launch) return;
			if(try_store_construct(pc)) 
				change_part_params(pc.metric);
		}
		void selection_canceled() { vessel_selector = null; }

		void remove_construct(PackedConstruct pc)
		{
			change_part_params(pc.metric, -1f);
			packed_constructs.Remove(pc.id);
		}

		void clear_constructs() 
		{ foreach(PackedConstruct pc in packed_constructs.Values) remove_construct(pc); }

		IEnumerator<YieldInstruction> store_constructs()
		{
			if(FlightGlobals.fetch == null || 
			   FlightGlobals.ActiveVessel == null) 
				yield break;
			//wait for hangar.vessel to be loaded
			VesselWaiter self = new VesselWaiter(vessel);
			while(!self.launched) yield return new WaitForFixedUpdate();
			//create vessels from constructs and store them
			HangarState cur_state = hangar_state; Deactivate();
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
				Staging.beginFlight();
				//wait for vsl to be launched
				while(!vsl.launched) yield return new WaitForFixedUpdate();
				store_vessel(vsl.vessel, false);
				//wait a 0.1 sec, otherwise the vessel may not be destroyed properly
				yield return new WaitForSeconds(0.1f); 

			}
			stored_mass = Utils.formatMass(vessels_mass);
			if(cur_state == HangarState.Active) Activate();
			//save game afterwards
			FlightGlobals.ForceSetActiveVessel(vessel);
			while(!self.launched) yield return null;
			yield return new WaitForSeconds(0.5f);
			GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
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
			doors = hangar_gates.State.ToString();
			state = hangar_state.ToString();
		}

		#region OnGUI
		void hangar_content_editor(int windowID)
		{
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			//VAB / SPH / SubAss selection
			GUILayout.FlexibleSpace();
			for(var T = VesselType.VAB; T <= VesselType.SubAssembly; T++)
			{ if(GUILayout.Toggle(vessel_type == T, T.ToString(), GUILayout.Width(100))) vessel_type = T; }
			GUILayout.FlexibleSpace();
			//Vessel selector
			if(GUILayout.Button("Select Vessel", Styles.normal_button, GUILayout.ExpandWidth(true))) 
			{
				Rect sWindowPos  = new Rect(eWindowPos) { height = 500 };
				var  diff  = HighLogic.CurrentGame.Parameters.Difficulty;
				bool stock = diff.AllowStockVessels;
				if(vessel_type == VesselType.SubAssembly) diff.AllowStockVessels = false;
				vessel_selector = 
					new CraftBrowser(sWindowPos, 
									 vessel_dirs[(int)vessel_type],
									 HighLogic.SaveFolder, "Select a ship to store",
					                 vessel_selected,
					                 selection_canceled,
					                 HighLogic.Skin,
					                 EditorLogic.ShipFileImage, true);
				diff.AllowStockVessels = stock;
			}
			GUILayout.EndHorizontal();
			//hangar info
			float used_frac = used_volume/hangar_metric.volume;
			GUILayout.Label(string.Format("Used Volume: {0}   {1:F1}%", 
			                              Utils.formatVolume(used_volume), used_frac*100f), 
			                Styles.fracStyle(1-used_frac), GUILayout.ExpandWidth(true));
			//hangar contents
			List<PackedConstruct> constructs = packed_constructs.Values;
			constructs.Sort((a, b) => a.name.CompareTo(b.name));
			scroll_view = GUILayout.BeginScrollView(scroll_view, GUILayout.Height(200), GUILayout.Width(400));
			GUILayout.BeginVertical();
			foreach(PackedConstruct pc in constructs)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label(string.Format("{0}: {1}   Cost: {2:F1}", 
				                              pc.name, Utils.formatMass(pc.metric.mass), pc.metric.cost), 
				                Styles.label, GUILayout.ExpandWidth(true));
				if(GUILayout.Button("X", Styles.red_button, GUILayout.Width(25))) remove_construct(pc);
				GUILayout.EndHorizontal();
			}
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
			if(GUILayout.Button("Clear", Styles.red_button, GUILayout.ExpandWidth(true))) clear_constructs();
			if(GUILayout.Button("Close", Styles.normal_button, GUILayout.ExpandWidth(true))) 
			{
				Utils.LockIfMouseOver(eLock, eWindowPos, false);
				editing_hangar = false;
			}
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
				Utils.LockIfMouseOver(eLock, eWindowPos, true);
				eWindowPos = GUILayout.Window(GetInstanceID(), eWindowPos,
							                  hangar_content_editor,
							                  "Choose vessel type",
							                  GUILayout.Width(400));
			}
			else 
			{
				Utils.LockIfMouseOver(eLock, vessel_selector.windowRect, true);
				vessel_selector.OnGUI();
			}
		}
		#endregion
		#endregion

		#region ControllableModule
		ModuleGUIState gui_state;
		public bool CanEnable() { return true; }
		public bool CanDisable() 
		{ 
			if(stored_vessels.Count > 0 || packed_constructs.Count > 0)
			{
				ScreenMessager.showMessage("Empty the hangar before deflating it", 3);
				return false;
			}
			if(EditorLogic.fetch == null && hangar_state == HangarState.Active)
			{
				ScreenMessager.showMessage("Deactivate the hangar before deflating it", 3);
				return false;
			}
			if(hangar_gates.State != AnimatorState.Closed)
			{
				ScreenMessager.showMessage("Close hangar doors before deflating it", 3);
				return false;
			}
			return true;
		}

		public void Enable(bool enable) 
		{ 
			if(enable) 
			{
				if(gui_state == null) gui_state = this.SaveGUIState();
				this.ActivateGUI(gui_state);
				Setup();
				enabled = true;
			}
			else
			{
				gui_state = this.DeactivateGUI();
				enabled = false;
			}
		}
		#endregion
	}
}