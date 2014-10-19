//This code is partly based on the code from Extraplanetary Launchpads plugin. ExLaunchPad and Recycler classes.
//Thanks Taniwha, I've learnd many things from your code and from our conversation.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AtHangar
{
	//this module adds the ability to store a vessel in a packed state inside
	public class Hangar : HangarStorage
	{
		public enum HangarState { Inactive, Active }

		#region Configuration
		//hangar properties
		[KSPField (isPersistant = false)] public bool   UseHangarSpaceMesh = false;
		[KSPField (isPersistant = false)] public string AnimatorID;
		[KSPField (isPersistant = false)] public float  EnergyConsumption = 0.75f;
		[KSPField (isPersistant = false)] public float  VolumePerKerbal = 6.7f; // m^3
		[KSPField (isPersistant = false)] public bool   StaticCrewCapacity = true;
		[KSPField (isPersistant = false)] public bool   NoTransfers = false;
		//vessel spawning
		[KSPField (isPersistant = false)] public float  LaunchHeightOffset;
		[KSPField (isPersistant = false)] public string LaunchTransform;
		[KSPField (isPersistant = false)] public string LaunchVelocity;
		[KSPField (isPersistant = true)]  public bool   LaunchWithPunch = false;
		#endregion

		#region Internals
		//physical properties
		const float crew_volume_ratio = 0.3f; //only 30% of the remaining volume may be used for crew (i.e. V*(1-usefull_r)*crew_r)

		//hangar machinery
		BaseHangarAnimator hangar_gates;
		MeshFilter hangar_space = null;
		public AnimatorState gates_state { get { return hangar_gates.State; } }
		public HangarState hangar_state { get; private set; }
		public VesselResources<Vessel, Part, PartResource> hangarResources { get; private set; }
		readonly public List<ResourceManifest> resourceTransferList = new List<ResourceManifest>();
		readonly Dictionary<Guid, MemoryTimer> probed_vessels = new Dictionary<Guid, MemoryTimer>();

		//vessels storage
		readonly protected List<HangarPassage> passage_checklist = new List<HangarPassage>();
		readonly protected List<HangarStorage> connected_storage = new List<HangarStorage>();
		public int   TotalVesselsDocked;
		public float TotalVolume;
		public float TotalUsedVolume;
		public float TotalStoredMass;
		public float TotalCostMass;
		public float TotalUsedVolumeFrac
		{ get { return TotalUsedVolume/TotalVolume; } }

		//vessel spawn
		public Vector3 launchVelocity;
		Transform launch_transform;
		Vessel launched_vessel;
		Vector3 deltaV = Vector3.zero;
		bool change_velocity = false;

		//in-editor vessel docking
		const string eLock  = "Hangar.EditHangar";
		const string scLock = "Hangar.LoadShipConstruct";
		static readonly List<string> vessel_dirs = new List<string>{"VAB", "SPH", "../Subassemblies"};
		Rect eWindowPos     = new Rect(Screen.width/2-200, 100, 400, 100);
		Rect neWindowPos    = new Rect(Screen.width/2-200, 100, 400, 50);
		Vector2 scroll_view = Vector2.zero;
		CraftBrowser vessel_selector;
		VesselType   vessel_type;
		#endregion

		#region GUI
		[KSPField (guiName = "Crew Capacity", guiActiveEditor=true)] public string crew_capacity;
		[KSPField (guiName = "Hangar Name",   guiActive = true, guiActiveEditor=true, isPersistant = true)]
		public string HangarName = "_none_";

		public override string GetInfo()
		{
			string info = "Energy Cosumption:\n";
			info += string.Format("Hangar: {0}/sec\n", EnergyConsumption);
			var gates = part.Modules.OfType<HangarAnimator>().FirstOrDefault(m => m.AnimatorID == AnimatorID);
			if(gates != null) info += string.Format("Doors: {0}/sec\n", gates.EnergyConsumption);
			return info;
		}
		#endregion
		
		#region For HangarWindow
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

		public bool IsControllable { get { return vessel.IsControllable || part.protoModuleCrew.Count > 0; } }
		#endregion
		
		
		#region Setup
		public override void OnAwake()
		{
			base.OnAwake();
			GameEvents.onVesselWasModified.Add(update_connected_storage);
			GameEvents.onEditorShipModified.Add(update_connected_storage);
		}

		void OnDestroy() 
		{ 
			GameEvents.onVesselWasModified.Remove(update_connected_storage);
			GameEvents.onEditorShipModified.Remove(update_connected_storage);
		}

		void update_resources()
		{ hangarResources = new VesselResources<Vessel, Part, PartResource>(vessel); }

		void build_passage_checklist()
		{
			passage_checklist.Clear();
			foreach(Part p in part.AllConnectedParts())
			{ if(p != part) passage_checklist.AddRange(p.Modules.OfType<HangarPassage>()); }
		}

		bool all_passages_ready
		{
			get
			{
				if(passage_checklist.Count == 0) return true;
				bool loaded = true;
				foreach(var p in passage_checklist)
				{ loaded &= p.Ready; if(!loaded) break; }
				return loaded;
			}
		}

		void build_connected_storage()
		{
			connected_storage.Clear();
			var connected_passages = GetConnectedPassages();
			foreach(var p in connected_passages)
			{
				var storage = p as HangarStorage;
				if(storage != null) connected_storage.Add(storage);
			}
			this.Log("Number of passage nodes: {0}", Nodes.Count);//debug
			this.Log("Number of connected passages: {0}", connected_passages.Count);//debug
			this.Log("Number of connected storage spaces: {0}", connected_storage.Count);//debug
		}

		void update_total_values()
		{
			TotalVesselsDocked = 0;
			TotalVolume        = 0;
			TotalUsedVolume    = 0;
			TotalStoredMass    = 0;
			TotalCostMass      = 0;
			foreach(var s in connected_storage)
			{
				TotalVesselsDocked += s.VesselsDocked;
				if(s.HangarMetric != null)
					TotalVolume    += s.HangarMetric.volume;
				TotalUsedVolume    += s.UsedVolume;
				TotalStoredMass    += s.VesselsMass;
				TotalCostMass      += s.VesselsCost;
			}
			this.Log("Totals: V {0}, UV {1}, UVf {2}", TotalVolume, TotalUsedVolume, TotalUsedVolumeFrac);//debug
		}

		void update_connected_storage()
		{
			build_connected_storage();
			update_total_values();
		}

		void update_connected_storage(Vessel vsl)
		{ if(vsl == part.vessel) 
			{ this.Log("Hangar.onVesselWasModified");//debug
				update_connected_storage(); } }

		void update_connected_storage(ShipConstruct ship)
		{ update_connected_storage(); 
			this.Log("Hangar.onEditorShipModified");//debug
		}

		IEnumerator<YieldInstruction> delayed_update_connected_storage()
		{
			while(!all_passages_ready) yield return null;
			this.Log("All HangarPassages are loaded");//debug
			update_connected_storage();
		}

		protected override void early_setup(StartState state)
		{
			base.early_setup(state);
			this.Log("Hangar.OnStart");//debug
			//set vessel type
			EditorLogic el = EditorLogic.fetch;
			if(el != null) vessel_type = el.editorType == EditorLogic.EditorMode.SPH ? VesselType.SPH : VesselType.VAB;
			//setup hangar name
			if(HangarName == "_none_") HangarName = part.partInfo.title;
			//initialize resources
			if(state != StartState.Editor) update_resources();
			//initialize Animator
			part.force_activate();
			hangar_gates = part.Modules.OfType<BaseHangarAnimator>().FirstOrDefault(m => m.AnimatorID == AnimatorID);
			if(hangar_gates == null)
			{
				hangar_gates = new BaseHangarAnimator();
				this.Log("Using BaseHangarAnimator");
			}
			else
			{
				Events["Open"].guiActiveEditor = true;
				Events["Close"].guiActiveEditor = true;
			}
			build_passage_checklist();
		}

		protected override void start_coroutines()
		{
			base.start_coroutines();
			StartCoroutine(delayed_update_connected_storage());
		}

		public override void Setup(bool reset = false)	
		{
			base.Setup(reset);
			this.Log("Hangar.Setup");//debug
			//get launch speed if it's defined
			try { launchVelocity = LaunchVelocity != "" ? ConfigNode.ParseVector3(LaunchVelocity) : Vector3.zero; }
			catch(Exception ex)
			{
				this.Log("Unable to parse LaunchVelocity '{0}'", LaunchVelocity);
				Debug.LogException(ex);
			}
			//calculate crew capacity from remaining volume
			if(!StaticCrewCapacity)
			{
				part.CrewCapacity = (int)((PartMetric.volume-HangarMetric.volume)*crew_volume_ratio/VolumePerKerbal);
				crew_capacity = part.CrewCapacity.ToString();
				Fields["crew_capacity"].guiActiveEditor = true;
			}
			else Fields["crew_capacity"].guiActiveEditor = false;
		}

		protected override void on_set_part_params()
		{
			var el = EditorLogic.fetch;
			if(el != null) GameEvents.onEditorShipModified.Fire(el.ship);
			else if(part.vessel != null) GameEvents.onVesselWasModified.Fire(part.vessel);
		}
		#endregion

		#region Save-Load
		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
			node.AddValue("hangarState", hangar_state.ToString());
		}

		public override void OnLoad(ConfigNode node)
		{ 
			base.OnLoad(node);
			if(node.HasValue("hangarState"))
				hangar_state = (HangarState)Enum.Parse(typeof(HangarState), node.GetValue("hangarState"));
		}
		#endregion

		#region Physics changes
		public void FixedUpdate()
		{
			if(HighLogic.LoadedSceneIsFlight)
			{
				//change vessel velocity if requested
				if(change_velocity)
				{
					vessel.ChangeWorldVelocity((Vector3d.zero+deltaV).xzy);
					change_velocity = false;
					deltaV = Vector3.zero;
				}
				//consume energy if hangar is operational
				if(hangar_state == HangarState.Active)
				{
					float request = EnergyConsumption*TimeWarp.fixedDeltaTime;
					if(part.RequestResource("ElectricCharge", request) < request)
					{
						ScreenMessager.showMessage("Not enough energy. The hangar has deactivated.");
						Deactivate();
					}
				}
			}
		}
		#endregion
		
		#region Store
		/// <summary>
		/// Checks if a vessel can be stored in the hangar right now.
		/// </summary>
		/// <param name="vsl">A vessel to check</param>
		bool can_store(Vessel vsl)
		{
			if(vsl == null || vsl == vessel || !vsl.enabled || vsl.isEVA) return false;
			//if hangar is not ready, return
			if(hangar_state == HangarState.Inactive) 
			{
				ScreenMessager.showMessage("Activate the hangar first");
				return false;
			}
			//check self state first
			switch(FlightGlobals.ClearToSave()) 
			{
			case ClearToSaveStatus.NOT_WHILE_ABOUT_TO_CRASH:
			{
				ScreenMessager.showMessage("Cannot accept the vessel while about to crush");
				return false;
			}
			}
			//always check relative velocity and acceleration
			Vector3 rv = vessel.GetObtVelocity()-vsl.GetObtVelocity();
			if(rv.magnitude > 1f) 
			{
				ScreenMessager.showMessage("Cannot accept a vessel with a relative speed higher than 1m/s");
				return false;
			}
			Vector3 ra = vessel.acceleration - vsl.acceleration;
			if(ra.magnitude > 0.1)
			{
				ScreenMessager.showMessage("Cannot accept an accelerating vessel");
				return false;
			}
			return true;
		}

		bool metric_fits_into_hangar_space(Metric m)
		{
			GetLaunchTransform();
			return hangar_space == null ? 
				m.FitsAligned(launch_transform, part.partTransform, HangarMetric) : 
				m.FitsAligned(launch_transform, hangar_space.transform, hangar_space.sharedMesh);
		}

		protected override bool try_store_vessel(PackedVessel v)
		{
			GetLaunchTransform();
			if(!metric_fits_into_hangar_space(v.metric))
			{
				ScreenMessager.showMessage(5, "Insufficient vessel clearance for safe docking\n" +
					"\"{0}\" cannot be stored in this hangar", v.name);
				return false;
			}
			return base.try_store_vessel(v);
		}

		bool compute_hull { get { return hangar_space != null; } }

		StoredVessel try_store_vessel(Vessel vsl)
		{
			//check vessel crew
			if(vsl.GetCrewCount() > vessel.GetCrewCapacity()-vessel.GetCrewCount())
			{
				ScreenMessager.showMessage("Not enough space for the crew of a docking vessel");
				return null;
			}
			//check vessel metrics
			var sv = new StoredVessel(vsl, compute_hull);
			return try_store_vessel(sv)? sv : null;
		}
		
		/// <summary>
		/// Process a vessel that triggered the hangar.
		/// </summary>
		/// <param name="vsl">Vessel</param>
		void process_vessel(Vessel vsl)
		{
			//check if this vessel was encountered before;
			//if so, reset the timer and return
			MemoryTimer timer;
			if(probed_vessels.TryGetValue(vsl.id, out timer))
			{ timer.Reset(); return; }
			//if the vessel is new, check momentary states
			if(!can_store(vsl))	return;
			//if the state is OK, try to store the vessel
		    StoredVessel stored_vessel = try_store_vessel(vsl);
			//if failed, remember it
			if(stored_vessel == null)
			{
				timer = new MemoryTimer();
				timer.EndAction += () => { if(probed_vessels.ContainsKey(vsl.id)) probed_vessels.Remove(vsl.id); };
				probed_vessels.Add(vsl.id, timer);
				StartCoroutine(timer);
				return;
			}
			//calculate velocity change to conserve impulse
			deltaV = (vsl.orbit.vel-vessel.orbit.vel)*stored_vessel.mass/vessel.GetTotalMass();
			change_velocity = true;
			//get vessel crew on board
			var _crew = new List<ProtoCrewMember>(stored_vessel.crew);
			CrewTransfer.delCrew(vsl, _crew);
			vsl.DespawnCrew();
			//first of, add crew to the hangar if there's a place
			CrewTransfer.addCrew(part, _crew);
			//then add to other vessel parts if needed
			CrewTransfer.addCrew(vessel, _crew);
			//switch to hangar vessel before storing
			if(FlightGlobals.ActiveVessel.id == vsl.id)
				FlightGlobals.ForceSetActiveVessel(vessel);
			//destroy vessel
			vsl.Die();
			ScreenMessager.showMessage("\"{0}\" has been docked inside the hangar", stored_vessel.name);
		}
		
		//called every frame while part collider is touching the trigger
		void OnTriggerStay (Collider col) //see Unity docs
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
			process_vessel(p.vessel);
		}
		
		//called when part collider exits the trigger
		void OnTriggerExit(Collider col)
		{
			if(col == null
				|| !col.CompareTag("Untagged")
				||  col.gameObject.name == "MapOverlay collider" // kethane
				||  col.attachedRigidbody == null)
				return;
			Part p = col.attachedRigidbody.GetComponent<Part>();
			if(p == null || p.vessel == null) return;
		}
		#endregion

		#region EditHangarContents
		IEnumerator<YieldInstruction> delayed_try_store_construct(PackedConstruct pc)
		{
			if(pc.construct == null) yield break;
			Utils.LockEditor(scLock);
			for(int i = 0; i < 3; i++)
				yield return new WaitForEndOfFrame();
			pc.UpdateMetric(compute_hull);
			try_store_vessel(pc);
			pc.UnloadConstruct();
			Utils.LockEditor(scLock, false);
		}

		void vessel_selected(string filename, string flagname)
		{
			EditorLogic EL = EditorLogic.fetch;
			if(EL == null) return;
			//load vessel config
			vessel_selector = null;
			var pc = new PackedConstruct(filename, flagname);
			if(pc.construct == null) 
			{
				Utils.Log("PackedConstruct: unable to load ShipConstruct from {0}. " +
					"This usually means that some parts are missing " +
					"or some modules failed to initialize.", filename);
				ScreenMessager.showMessage("Unable to load {0}", filename);
				return;
			}
			//check if the construct contains launch clamps
			if(Utils.HasLaunchClamp(pc.construct))
			{
				ScreenMessager.showMessage("\"{0}\" has launch clamps. Remove them before storing.", pc.name);
				pc.UnloadConstruct();
				return;
			}
			//check if it's possible to launch such vessel
			bool cant_launch = false;
			var preFlightCheck = new PreFlightCheck(new Callback(() => cant_launch = false), new Callback(() => cant_launch = true));
			preFlightCheck.AddTest(new PreFlightTests.ExperimentalPartsAvailable(pc.construct));
			preFlightCheck.RunTests(); 
			//cleanup loaded parts and try to store construct
			if(cant_launch) pc.UnloadConstruct();
			else StartCoroutine(delayed_try_store_construct(pc));
		}
		void selection_canceled() { vessel_selector = null; }
		#endregion

		#region Restore
		#region Positioning
		/// <summary>
		/// Calculate transform of restored vessel.
		/// </summary>
		public Transform GetLaunchTransform()
		{
			launch_transform = null;
			if(LaunchTransform != "")
				launch_transform = part.FindModelTransform(LaunchTransform);
			if(launch_transform == null)
			{
				Vector3 offset = Vector3.up * LaunchHeightOffset;
				Transform t = part.transform;
				var restorePos = new GameObject ();
				restorePos.transform.position = t.position;
				restorePos.transform.position += t.TransformDirection (offset);
				restorePos.transform.rotation = t.rotation;
				launch_transform = restorePos.transform;
				this.Log("LaunchTransform not found. Using offset.");
			}
			return launch_transform;
		}

		Transform get_transform_for_construct(PackedConstruct pc)
		{
			GetLaunchTransform();
			var tmp = new GameObject();
			Vector3 bounds_offset  = launch_transform.TransformDirection(pc.metric.center);
			tmp.transform.position = launch_transform.position+bounds_offset;
			tmp.transform.rotation = launch_transform.rotation;
			return tmp.transform;
		}
		
		/// <summary>
		/// Set vessel orbit, transform, coordinates.
		/// </summary>
		/// <param name="sv">Stored vessel</param>
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
			pv.rotation = proto_rot*hangar_rot.Inverse()*launch_transform.rotation;
			//calculate launch offset from vessel bounds
			Vector3 bounds_offset = launch_transform.TransformDirection(sv.CoM - sv.CoG);
			//set vessel's orbit
			double UT  = Planetarium.GetUniversalTime();
			Orbit horb = vessel.orbit;
			var vorb = new Orbit();
			Vector3 d_pos = launch_transform.position-vessel.findWorldCenterOfMass()+bounds_offset;
			Vector3d vpos = horb.pos+new Vector3d(d_pos.x, d_pos.z, d_pos.y);
			Vector3d vvel = horb.vel;
			if(LaunchWithPunch && launchVelocity != Vector3.zero)
			{
				//honor the impulse conservation law
				//:calculate launched vessel velocity
				float hM = vessel.GetTotalMass();
				float tM = hM + sv.mass;
				Vector3 d_vel = launch_transform.TransformDirection(launchVelocity);
				vvel += (Vector3d.zero + d_vel*hM/tM).xzy;
				//:calculate hangar's vessel velocity
				Vector3d hvel = horb.vel + (Vector3d.zero + d_vel*(-sv.mass)/tM).xzy;
				vessel.orbitDriver.SetOrbitMode(OrbitDriver.UpdateMode.UPDATE);
				horb.UpdateFromStateVectors(horb.pos, hvel, horb.referenceBody, UT);
			}
			vorb.UpdateFromStateVectors(vpos, vvel, horb.referenceBody, UT);
			pv.orbitSnapShot = new OrbitSnapshot(vorb);
			//position on a surface
			if(vessel.LandedOrSplashed)
			{
				//calculate launch offset from vessel bounds
				bounds_offset = launch_transform.TransformDirection(-sv.CoG);
				//set vessel's position
				vpos = Vector3d.zero+launch_transform.position+bounds_offset;
				pv.longitude  = vessel.mainBody.GetLongitude(vpos);
				pv.latitude   = vessel.mainBody.GetLatitude(vpos);
				pv.altitude   = vessel.mainBody.GetAltitude(vpos);
			}
		}

		//static coroutine launched from a DontDestroyOnLoad sentinel object allows to execute code while the scene is switching
		static IEnumerator<YieldInstruction> setup_vessel(UnityEngine.Object sentinel, LaunchedVessel lv)
		{
			while(!lv.loaded) yield return null;
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
			var obj = new GameObject();
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
				var rm = new ResourceManifest();
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
				sv.mass += dM; sv.cost += dC;
				stored_vessels.UpdateParams();
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
				ScreenMessager.showMessage("Activate the hangar first");
				return false;
			}
			if(hangar_gates.State != AnimatorState.Opened) 
			{
				ScreenMessager.showMessage("Open hangar gates first");
				return false;
			}
			//if something is docked to the hangar docking port (if its present)
			ModuleDockingNode dport = part.GetModule<ModuleDockingNode>();
			if(dport != null && dport.vesselInfo != null)
			{
				ScreenMessager.showMessage("Cannot launch a vessel while another is docked");
				return false;
			}
			//if in orbit or on the ground and not moving
			switch(FlightGlobals.ClearToSave()) 
			{
				case ClearToSaveStatus.NOT_IN_ATMOSPHERE:
				{
					ScreenMessager.showMessage("Cannot launch a vessel while flying in atmosphere");
					return false;
				}
				case ClearToSaveStatus.NOT_UNDER_ACCELERATION:
				{
					ScreenMessager.showMessage("Cannot launch a vessel hangar is under accelleration");
					return false;
				}
				case ClearToSaveStatus.NOT_WHILE_ABOUT_TO_CRASH:
				{
					ScreenMessager.showMessage("Cannot launch a vessel while about to crush");
					return false;
				}
				case ClearToSaveStatus.NOT_WHILE_MOVING_OVER_SURFACE:
				{
					ScreenMessager.showMessage("Cannot launch a vessel while moving over the surface");
					return false;
				}
			}
			if(vessel.angularVelocity.magnitude > 0.01)
			{
				ScreenMessager.showMessage("Cannot launch a vessel while rotating");
				return false;
			}
			return true;
		}
		
		public void TryRestoreVessel(StoredVessel stored_vessel)
		{
			if(!can_restore()) return;
			//clean up
			if(!stored_vessels.Remove(stored_vessel))
			{
				ScreenMessager.showMessage("WARNING: restored vessel is not found in the Stored Vessels: {0}\n" +
					"This should never happen!", stored_vessel.id);
				return;
			}
			ScreenMessager.showMessage("Launching \"{0}\"...", stored_vessel.name);
			//switch hangar state
			hangar_state = HangarState.Inactive;
			//transfer resources
			transferResources(stored_vessel);
			//change part mass
			set_part_params();
			//set restored vessel orbit
			GetLaunchTransform();
			position_vessel(stored_vessel);
			//restore vessel
			stored_vessel.Load();
			//get restored vessel from the world
			launched_vessel = stored_vessel.launched_vessel;
			//transfer crew back to the launched vessel
			List<ProtoCrewMember> crew_to_transfer = CrewTransfer.delCrew(vessel, stored_vessel.crew);
			//switch to restored vessel
			//:set launched vessel's state to flight
			// otherwise launched rovers are sometimes stuck to the ground despite of the launch_transform
			launched_vessel.Splashed = launched_vessel.Landed = false; 
			FlightGlobals.ForceSetActiveVessel(launched_vessel);
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

		[KSPEvent (guiActive = true, guiActiveEditor = true, guiName = "Rename Hangar", active = true)]
		public void EditName() 
		{ 
			editing_hangar_name = !editing_hangar_name; 
			Utils.LockIfMouseOver(eLock, neWindowPos, editing_hangar_name);
		}
		bool editing_hangar_name = false;
		
		public void Activate() { hangar_state = HangarState.Active;	}
		
		public void Deactivate() 
		{ 
			hangar_state = HangarState.Inactive;
			foreach(MemoryTimer timer in probed_vessels.Values)
				StopCoroutine(timer);
			probed_vessels.Clear(); 
		}
		
		public void Toggle()
		{
			if(hangar_state == HangarState.Active) Deactivate();
			else Activate();
		}

		[KSPEvent (guiActiveEditor = true, guiName = "Edit contents", active = true)]
		public void EditHangar() 
		{ 
			editing_hangar = !editing_hangar; 
			Utils.LockIfMouseOver(eLock, eWindowPos, editing_hangar);
		}
		bool editing_hangar = false;

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
				var sWindowPos  = new Rect(eWindowPos) { height = 500 };
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
			var used_frac = TotalUsedVolumeFrac;
			GUILayout.Label(string.Format("Used Volume: {0}   {1:F1}%", 
				Utils.formatVolume(TotalUsedVolume), used_frac*100f), 
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
				if(GUILayout.Button("+1", Styles.green_button, GUILayout.Width(25))) 
					try_store_vessel(pc.Clone());
				if(GUILayout.Button("X", Styles.red_button, GUILayout.Width(25))) RemoveVessel(pc);
				GUILayout.EndHorizontal();
			}
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
			if(GUILayout.Button("Clear", Styles.red_button, GUILayout.ExpandWidth(true)))
			{ packed_constructs.Clear(); set_part_params();}
			if(GUILayout.Button("Close", Styles.normal_button, GUILayout.ExpandWidth(true))) 
			{
				Utils.LockEditor(eLock, false);
				editing_hangar = false;
			}
			GUILayout.EndVertical();
			GUI.DragWindow(new Rect(0, 0, 500, 20));
		}

		void hangar_name_editor(int windowID)
		{
			GUILayout.BeginVertical();
			HangarName = GUILayout.TextField(HangarName, 50);
			if(GUILayout.Button("Close", Styles.normal_button, GUILayout.ExpandWidth(true))) 
			{
				Utils.LockEditor(eLock, false);
				editing_hangar_name = false;
			}
			GUILayout.EndVertical();
			GUI.DragWindow(new Rect(0, 0, 500, 20));
		}

		public void OnGUI() 
		{ 
			if(Event.current.type != EventType.Layout) return;
			if(!editing_hangar && !editing_hangar_name) return;
			if(editing_hangar && 
				(HighLogic.LoadedScene != GameScenes.EDITOR &&
				 HighLogic.LoadedScene != GameScenes.SPH)) return;
			//init skin
			Styles.InitSkin();
			GUI.skin = Styles.skin;
			Styles.InitGUI();
			//edit hangar
			if(editing_hangar)
			{
				if(vessel_selector == null) 
				{
					Utils.LockIfMouseOver(eLock, eWindowPos);
					eWindowPos = GUILayout.Window(GetInstanceID(), eWindowPos,
								                  hangar_content_editor,
								                  "Choose vessel type",
								                  GUILayout.Width(400));
					Utils.CheckRect(ref eWindowPos);
				}
				else 
				{
					Utils.LockIfMouseOver(eLock, vessel_selector.windowRect);
					vessel_selector.OnGUI();
				}
			}
			//edit name
			else if(editing_hangar_name)
			{
				Utils.LockIfMouseOver(eLock, neWindowPos);
				neWindowPos = GUILayout.Window(GetInstanceID(), neWindowPos,
											   hangar_name_editor,
											   "Rename Hangar",
											   GUILayout.Width(400));
				Utils.CheckRect(ref neWindowPos);
			}
		}
		#endregion

		#region ControllableModule
		public override bool CanDisable() 
		{ 
			if(!base.CanDisable()) return false;
			if(EditorLogic.fetch == null && hangar_state == HangarState.Active)
			{
				ScreenMessager.showMessage("Deactivate the hangar before deflating it");
				return false;
			}
			if(hangar_gates.State != AnimatorState.Closed)
			{
				ScreenMessager.showMessage("Close hangar doors before deflating it");
				return false;
			}
			return true;
		}
		#endregion
	}
}