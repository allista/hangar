//   HangarMachinery.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
	public abstract partial class HangarMachinery : ControllableModuleBase
	{
		public enum HangarState { Inactive, Active }

		#region Configuration
		//hangar properties
		[KSPField (isPersistant = false)] public string AnimatorID = string.Empty;
		[KSPField (isPersistant = false)] public float  EnergyConsumption = 0.75f;
		[KSPField (isPersistant = false)] public bool   NoCrewTransfers;
		[KSPField (isPersistant = false)] public bool   NoResourceTransfers;
		[KSPField (isPersistant = false)] public bool   NoGUI;
		//vessel spawning
		[KSPField (isPersistant = false)] public Vector3 LaunchVelocity = Vector3.zero;
		[KSPField (isPersistant = true)]  public bool    LaunchWithPunch;
		[KSPField (isPersistant = false)] public string  CheckDockingPorts = string.Empty;
		//other
		[KSPField (isPersistant = false)] public string Trigger = string.Empty;
		#endregion

		#region Managed Storage
		public HangarStorage Storage { get; protected set; }

		public virtual Vector3 DockSize
		{ get { return Storage == null ? Vector3.zero : Storage.Size; } }

		public float Volume
		{ get { return Storage == null ? 0f : Storage.Volume; } }

		public int VesselsDocked
		{ get { return Storage == null ? 0 : Storage.TotalVesselsDocked; } }

		public float VesselsMass
		{ get { return Storage == null ? 0f : Storage.VesselsMass; } }

		public float VesselsCost
		{ get { return Storage == null ? 0f : Storage.VesselsCost; } }

		public float UsedVolume
		{ get { return Storage == null ? 0f : Storage.UsedVolume; } }

		public float UsedVolumeFrac { get { return UsedVolume/Volume; } }

		public List<StoredVessel> GetVessels()
		{ return Storage == null? new List<StoredVessel>() : Storage.GetVessels(); }

		//vessels storage
		protected List<HangarPassage> passage_checklist = new List<HangarPassage>();
		readonly protected List<ModuleDockingNode> docks_checklist = new List<ModuleDockingNode>();
		readonly public List<HangarStorage> ConnectedStorage = new List<HangarStorage>();
		public int   TotalVesselsDocked;
		public float TotalVolume;
		public float TotalUsedVolume;
		public float TotalStoredMass;
		public float TotalCostMass;
		public float TotalUsedVolumeFrac
		{ get { return TotalUsedVolume/TotalVolume; } }
		public bool CanRelocate
		{ get { return ConnectedStorage.Count > 1; } }
		#endregion

		#region Machinery
		public Metric PartMetric { get; private set; }

		protected MultiAnimator hangar_gates;
		public AnimatorState gates_state { get { return hangar_gates == null? AnimatorState.Opened : hangar_gates.State; } }
		public HangarState hangar_state { get; private set; }

		public VesselResources<Vessel, Part, PartResource> hangarResources { get; private set; }
		readonly public List<ResourceManifest> resourceTransferList = new List<ResourceManifest>();

		readonly Dictionary<Guid, MemoryTimer> probed_vessels = new Dictionary<Guid, MemoryTimer>();

		[KSPField (isPersistant = true)] Vector3 deltaV = Vector3.zero;
		[KSPField (isPersistant = true)] bool change_velocity;
		protected StoredVessel launched_vessel;

		public bool IsControllable { get { return vessel.IsControllable || part.protoModuleCrew.Count > 0; } }

		protected ResourcePump socket;
		#endregion

		#region GUI
		[KSPField (guiName = "Hangar Name",   guiActive = true, guiActiveEditor=true, isPersistant = true)]
		public string HangarName = "_none_";

		public override string GetInfo()
		{
			var info = "";
			//energy consumption
			var gates = part.GetAnimator(AnimatorID);
			if(EnergyConsumption.Equals(0) && (gates == null || gates.EnergyConsumption.Equals(0))) 
				info += "Simple cargo bay\n";
			else
			{
				info += "Energy Cosumption:\n";
				if(EnergyConsumption > 0)
					info += string.Format("- Hangar: {0}/sec\n", EnergyConsumption);
				if(gates != null && gates.EnergyConsumption > 0) 
					info += string.Format("- Doors: {0}/sec\n", gates.EnergyConsumption);
			}
			//vessel facilities
			if(NoCrewTransfers) info += "Crew transfer not available\n";
			if(NoResourceTransfers) info += "Resources transfer not available\n";
			if(LaunchVelocity != Vector3.zero) info += "Has integrated launch system\n";
			return info;
		}
		#endregion

		#region Setup
		public override void OnAwake()
		{
			base.OnAwake();
			vessels_window.WindowPos = new Rect(Screen.width/2-windows_width/2, 100, windows_width, 100);
			GameEvents.onVesselWasModified.Add(update_connected_storage);
			GameEvents.onEditorShipModified.Add(update_connected_storage);
			GameEvents.onVesselGoOffRails.Add(onVesselGoOffRails);
			GameEvents.onVesselLoaded.Add(onVesselLoaded);
		}

		void OnDestroy() 
		{ 
			GameEvents.onVesselWasModified.Remove(update_connected_storage);
			GameEvents.onEditorShipModified.Remove(update_connected_storage);
			GameEvents.onVesselGoOffRails.Remove(onVesselGoOffRails);
			GameEvents.onVesselLoaded.Remove(onVesselLoaded);
		}

		void update_resources()
		{ 
			if(vessel == null) return;
			hangarResources = new VesselResources<Vessel, Part, PartResource>(vessel); 
		}

		protected bool all_passages_ready { get { return passage_checklist.All(p => p.Ready); } }

		protected abstract List<HangarPassage> get_connected_passages();

		void build_connected_storage()
		{
			ConnectedStorage.Clear();
			var connected_passages = get_connected_passages();
			if(connected_passages == null) return;
			foreach(var p in connected_passages)
			{
				var other_storage = p as HangarStorage;
				if(other_storage != null) 
					ConnectedStorage.Add(other_storage);
			}
		}

		void update_total_values()
		{
			TotalVesselsDocked = 0;
			TotalVolume        = 0;
			TotalUsedVolume    = 0;
			TotalStoredMass    = 0;
			TotalCostMass      = 0;
			foreach(var s in ConnectedStorage)
			{
				TotalVesselsDocked += s.TotalVesselsDocked;
				TotalVolume        += s.Volume;
				TotalUsedVolume    += s.UsedVolume;
				TotalStoredMass    += s.VesselsMass;
				TotalCostMass      += s.VesselsCost;
			}
		}

		protected virtual void update_connected_storage()
		{
			build_connected_storage();
			update_total_values();
			clear_hangar_memory();
			Events["RelocateVessels"].guiActiveEditor = CanRelocate;
		}

		protected virtual void update_connected_storage(Vessel vsl)
		{ 
			if(vsl != part.vessel || !all_passages_ready) return;
			update_connected_storage(); 
		}

		void update_connected_storage(ShipConstruct ship)
		{ if(!all_passages_ready) return;
			update_connected_storage(); }

		IEnumerator<YieldInstruction> delayed_update_connected_storage()
		{
			while(!all_passages_ready) yield return null;
			update_connected_storage();
		}

		protected virtual void early_setup(StartState state)
		{
			var el = EditorLogic.fetch;
			if(el != null) 
			{
				//set vessel type
				facility = el.ship.shipFacility;
				//prevent triggers to catch raycasts
				if(Trigger != string.Empty)
				{
					var triggers = part.FindModelComponents<Collider>(Trigger);
					foreach(var c in triggers) c.gameObject.layer = 21; //Part Triggers
				}
			}
			//setup hangar name
			if(HangarName == "_none_") HangarName = part.Title();
			//initialize resources
			update_resources();
			//initialize Animator
			hangar_gates = part.GetAnimator(AnimatorID);
			if(hangar_gates == null)
			{
				Events["Open"].active = false;
				Events["Close"].active = false;
			}
			if(EnergyConsumption > 0) 
				socket = part.CreateSocket();
			//get docking ports that are inside hangar sapace
			var docks = part.Modules.OfType<ModuleDockingNode>().ToList();
			foreach(var d in CheckDockingPorts.Split(new []{' '}, StringSplitOptions.RemoveEmptyEntries))
				docks_checklist.AddRange(docks.Where(m => m.referenceAttachNode == d));
			//get all passages in the vessel
			passage_checklist = part.AllModulesOfType<HangarPassage>();
		}

		protected virtual void start_coroutines()
		{ StartCoroutine(delayed_update_connected_storage()); }

		/// <summary>
		/// Sets up internal properties that depend on Storage
		/// and may change with resizing.
		/// Overrides should always check if Storage is not null.
		/// </summary>
		/// <param name="reset">If set to <c>true</c> reset state befor setup.</param>
		public virtual void Setup(bool reset = false) 
		{ PartMetric = new Metric(part); }

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			early_setup(state);
			if(Storage == null)
				this.EnableModule(false);
			Setup();
			start_coroutines();
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
		public virtual void FixedUpdate()
		{
			if(HighLogic.LoadedSceneIsFlight)
			{
				//change vessel velocity if requested
				if(change_velocity)
				{
					vessel.ChangeWorldVelocity(deltaV);
					change_velocity = false;
					deltaV = Vector3.zero;
				}
				//consume energy if hangar is operational
				if(socket != null && hangar_state == HangarState.Active)
				{
					socket.RequestTransfer(EnergyConsumption*TimeWarp.fixedDeltaTime);
					if(socket.TransferResource() && socket.PartialTransfer)
					{
						Utils.Message("Not enough energy. The hangar has deactivated.");
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
		bool hangar_is_ready(Vessel vsl)
		{
			if(vsl == null || vsl == vessel || !vsl.enabled || vsl.isEVA) return false;
			//if hangar is not ready, return
			if(hangar_state == HangarState.Inactive) 
			{
				Utils.Message("Activate the hangar first");
				return false;
			}
			//always check relative velocity and acceleration
			Vector3 rv = vessel.GetObtVelocity()-vsl.GetObtVelocity();
			if(rv.sqrMagnitude > Globals.Instance.MaxSqrRelVelocity)
			{
				Utils.Message("Cannot accept a moving vessel");
				return false;
			}
			Vector3 ra = vessel.acceleration - vsl.acceleration;
			if(ra.sqrMagnitude > Globals.Instance.MaxSqrRelAcceleration)
			{
				Utils.Message("Cannot accept an accelerating vessel");
				return false;
			}
			return true;
		}

		protected virtual bool try_store_vessel(PackedVessel v)
		{ return Storage.TryStoreVessel(v); }

		StoredVessel try_store_vessel(Vessel vsl)
		{
			//check vessel crew
			var vsl_crew = vsl.GetCrewCount();
			if(NoCrewTransfers && vsl_crew > 0)
			{
				Utils.Message("Crew cannot enter through this hangar. Leave your ship before docking.");
				return null;
			}
			if(vsl_crew > vessel.GetCrewCapacity()-vessel.GetCrewCount())
			{
				Utils.Message("Not enough space for the crew of a docking vessel");
				return null;
			}
			//check vessel metrics
			var sv = new StoredVessel(vsl, Storage.ComputeHull);
			return try_store_vessel(sv) ? sv : null;
		}

		void clear_hangar_memory()
		{
			foreach(MemoryTimer timer in probed_vessels.Values)
				StopCoroutine(timer);
			probed_vessels.Clear();
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
			if(!hangar_is_ready(vsl)) return;
			//if the state is OK, try to store the vessel
			var stored_vessel = try_store_vessel(vsl);
			//if failed, remember it
			if(stored_vessel == null)
			{
				timer = new MemoryTimer();
				timer.EndAction += () => { if(probed_vessels.ContainsKey(vsl.id)) probed_vessels.Remove(vsl.id); };
				probed_vessels.Add(vsl.id, timer);
				StartCoroutine(timer);
				return;
			}
			//deactivate the hangar
			Deactivate();
			//calculate velocity change to conserve momentum
			deltaV = (vsl.orbit.vel-vessel.orbit.vel).xzy*stored_vessel.mass/vessel.GetTotalMass();
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
			Utils.Message("\"{0}\" has been docked inside the hangar", stored_vessel.name);
		}

		//called every frame while part collider is touching the trigger
		void OnTriggerStay(Collider col)
		{
			if(hangar_state != HangarState.Active
				||  Storage == null
				||  col == null
				|| !col.CompareTag("Untagged")
			    ||  col.gameObject.name == Globals.Instance.KethaneMapCollider
				||  col.attachedRigidbody == null)
				return;
			//get part and try to store vessel
			Part p = col.attachedRigidbody.GetComponent<Part>();
			if(p == null || p.vessel == null) return;
			process_vessel(p.vessel);
		}
		#endregion

		#region Restore
		#region Positioning
		protected abstract Vector3 get_vessel_offset(Transform launch_transform, StoredVessel sv);
		protected virtual Transform get_spawn_transform(PackedVessel pv) { return Storage.GetSpawnTransform(pv); }
		public virtual Transform GetSpawnTransform() { return Storage.AutoPositionVessel? null : Storage.GetSpawnTransform(); }
		protected virtual void on_vessel_positioned() {}
		protected virtual void before_vessel_launch() {}

		/// <summary>
		/// Set vessel orbit, transform, coordinates.
		/// </summary>
		void position_launched_vessel()
		{
			var pv = launched_vessel.proto_vessel;
			//state
			pv.situation = vessel.situation;
			pv.splashed  = vessel.Splashed;
			pv.landed    = vessel.Landed;
			pv.landedAt  = vessel.landedAt;
			//rotation
			//rotate spawn_transform.rotation to protovessel's reference frame
			var spawn_transform = get_spawn_transform(launched_vessel);
			pv.rotation = vessel.mainBody.bodyTransform.rotation.Inverse() * spawn_transform.rotation;
			//set vessel's orbit
			var UT    = Planetarium.GetUniversalTime();
			var horb  = vessel.orbit;
			var vorb  = new Orbit();
			var d_pos = spawn_transform.position+get_vessel_offset(spawn_transform, launched_vessel) - vessel.CurrentCoM;
			var vpos  = horb.pos + new Vector3d(d_pos.x, d_pos.z, d_pos.y) 
				- horb.GetRotFrameVel(horb.referenceBody)*TimeWarp.fixedDeltaTime;
			var vvel  = horb.vel;
			if(LaunchWithPunch && !LaunchVelocity.IsZero())
			{
				//honor the momentum conservation law
				//:calculate launched vessel velocity
				var hM = vessel.GetTotalMass();
				var tM = hM + launched_vessel.mass;
				var d_vel = (Vector3d)part.transform.TransformDirection(LaunchVelocity);
				launched_vessel.dV = d_vel*hM/tM;
				vvel += launched_vessel.dV.xzy;
				//:calculate hangar's vessel velocity
				deltaV = d_vel*(-launched_vessel.mass)/tM; 
				change_velocity = true;
			}
			vorb.UpdateFromStateVectors(vpos, vvel, horb.referenceBody, UT);
			pv.orbitSnapShot = new OrbitSnapshot(vorb);
			//position on a surface
			if(vessel.LandedOrSplashed)
				vpos = spawn_transform.position+get_vessel_offset(spawn_transform, launched_vessel);
			else vpos = vessel.mainBody.position + vpos.xzy;
			pv.longitude = vessel.mainBody.GetLongitude(vpos);
			pv.latitude  = vessel.mainBody.GetLatitude(vpos);
			pv.altitude  = vessel.mainBody.GetAltitude(vpos);
			on_vessel_positioned();
		}

		void onVesselGoOffRails(Vessel vsl)
		{
			if(launched_vessel == null) return;
			if(launched_vessel.vessel != vsl) return;
			launched_vessel = null;
			FlightGlobals.ForceSetActiveVessel(vsl);
		}

		void onVesselLoaded(Vessel vsl)
		{
			if(launched_vessel == null) return;
			if(launched_vessel.vessel != vsl) return;
			launched_vessel.vessel.parts.ForEach(p => p.partTransform = p.transform);
		}

		IEnumerator<YieldInstruction> launch_vessel(StoredVessel sv)
		{
			launched_vessel = sv;
			disable_collisions();
			before_vessel_launch();	
			transferResources(launched_vessel);
			CrewTransfer.addCrew(launched_vessel.proto_vessel, 
			                     CrewTransfer.delCrew(vessel, launched_vessel.crew));
			yield return null;
			yield return new WaitForFixedUpdate();
			position_launched_vessel();
			launched_vessel.proto_vessel.Load(HighLogic.CurrentGame.flightState);
			yield return new WaitForFixedUpdate();
			disable_collisions(false);
			var vsl = launched_vessel.vessel;
			if(vsl == null) yield break;
			if(vessel.LandedOrSplashed)
			{
				var pos = vsl.transform.position;
				var rot = vsl.transform.rotation;
				while(vsl.packed) 
				{
					if(vsl == null) yield break;
					rot = vsl.transform.rotation;
					pos = vsl.transform.position;
					vsl.GoOffRails();
					if(!vsl.packed) break;
					yield return new WaitForFixedUpdate();
				}
				if(vsl == null) yield break;
				vsl.SetPosition(pos);
				vsl.SetRotation(rot);
			}
			else
			{
				vsl.Load();
				var spawn_transform = get_spawn_transform(launched_vessel);
				var spos = spawn_transform.position+get_vessel_offset(spawn_transform, launched_vessel)
					-vsl.transform.TransformDirection(launched_vessel.CoM);
				var svel = part.rb.velocity+launched_vessel.dV;
				var vvel = vessel.rb_velocity;
				while(vsl.packed) 
				{
					if(vsl == null) yield break;
					vsl.SetPosition(spos);
					if(!vsl.packed) break;
					spos += (svel+vessel.rb_velocity-vvel)*TimeWarp.fixedDeltaTime;
					yield return new WaitForFixedUpdate();
				}
			}
		}

		protected virtual void disable_collisions(bool disable=true) {}
		#endregion

		#region Resources
		public void prepareResourceList(StoredVessel sv)
		{
			if(resourceTransferList.Count > 0) return;
			foreach(var r in sv.resources.resourcesNames)
			{
				if(hangarResources.ResourceCapacity(r) <= 0) continue;
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
				if(res_def.density <= 0) continue;
				float dM = (float)a*res_def.density;
				float dC = (float)a*res_def.unitCost;
				sv.mass += dM; sv.cost += dC;
				Storage.UpdateParams();
			}
			resourceTransferList.Clear();
		}
		#endregion

		protected virtual bool can_restore(PackedVessel v)
		{
			//if hangar is not ready
			if(hangar_state == HangarState.Inactive) 
			{
				Utils.Message("Activate the hangar first");
				return false;
			}
			if(launched_vessel != null)
			{
				Utils.Message("Launch is in progress");
				return false;
			}
			if(hangar_gates != null && hangar_gates.State != AnimatorState.Opened) 
			{
				Utils.Message("Open hangar gates first");
				return false;
			}
			//if something is docked to the hangar docking port (if its present)
			if(!docks_checklist.TrueForAll(d => d.vesselInfo == null))
			{
				Utils.Message("Cannot launch a vessel while another one is docked");
				return false;
			}
			//if rotating
			if(vessel.angularVelocity.sqrMagnitude > Globals.Instance.MaxSqrAngularVelocity)
			{
				Utils.Message("Cannot launch a vessel while rotating");
				return false;
			}
			//if on the ground
			if(vessel.LandedOrSplashed)
			{
				if(vessel.srf_velocity.sqrMagnitude > Globals.Instance.MaxSqrSurfaceVelocity)
				{
					Utils.Message("Cannot launch a vessel while moving");
					return false;
				}
			}
			else //if flying
			{
				if(vessel.geeForce > Globals.Instance.MaxGeeForce)
				{
					Utils.Message("Cannot launch a vessel under gee force above {0}", 
					                           Globals.Instance.MaxGeeForce);
					return false;
				}
				if(vessel.mainBody.atmosphere && 
				   vessel.staticPressurekPa > Globals.Instance.MaxStaticPressure)
				{
					Utils.Message("Cannot launch a vessel in low atmosphere");
					return false;
				}
			}
			return true;
		}

		public void TryRestoreVessel(StoredVessel stored_vessel)
		{
			if(!can_restore(stored_vessel)) return;
			//clean up
			if(!Storage.RemoveVessel(stored_vessel))
			{
				Utils.Message("WARNING: restored vessel is not found in the Stored Vessels: {0}\n" +
					"This should never happen!", stored_vessel.id);
				return;
			}
			Utils.Message("Launching \"{0}\"...", stored_vessel.name);
			//switch hangar state
			Deactivate();
			//restore vessel
			StartCoroutine(launch_vessel(stored_vessel));
		}
		#endregion

		#region Events&Actions
		//events
		[KSPEvent (guiActiveEditor = true, guiName = "Open gates", active = true)]
		public void Open() 
		{ 
			if(hangar_gates == null) return;
			hangar_gates.Open();
			Events["Open"].active = false;
			Events["Close"].active = true;
			Activate();
		}

		[KSPEvent (guiActiveEditor = true, guiName = "Close gates", active = false)]
		public void Close()	
		{ 
			if(hangar_gates == null) return;
			hangar_gates.Close(); 
			Events["Open"].active = true;
			Events["Close"].active = false;
			Deactivate();
		}

		public void Activate() { hangar_state = HangarState.Active;	}

		public void Deactivate() 
		{ 
			hangar_state = HangarState.Inactive;
			clear_hangar_memory();
		}

		public void Toggle()
		{
			if(hangar_state == HangarState.Active) Deactivate();
			else Activate();
		}

		//actions
		[KSPAction("Open Gates")]
		public void OpenGatesAction(KSPActionParam param) { Open(); }

		[KSPAction("Close Gates")]
		public void CloseGatesAction(KSPActionParam param) { Close(); }

		[KSPAction("Toggle Gates")]
		public void ToggleGatesAction(KSPActionParam param) { if(hangar_gates != null) hangar_gates.Toggle(); }

		[KSPAction("Activate Hangar")]
		public void ActivateStateAction(KSPActionParam param) { Activate(); }

		[KSPAction("Deactivate Hangar")]
		public void DeactivateStateAction(KSPActionParam param) { Deactivate(); }

		[KSPAction("Toggle Hangar")]
		public void ToggleStateAction(KSPActionParam param) { Toggle(); }
		#endregion

		#region ControllableModule
		public override bool CanDisable() 
		{ 
			if(EditorLogic.fetch == null && hangar_state == HangarState.Active)
			{
				Utils.Message("Deactivate the hangar before disabling");
				return false;
			}
			if(hangar_gates != null && hangar_gates.State != AnimatorState.Closed)
			{
				Utils.Message("Close hangar doors before disabling");
				return false;
			}
			return true;
		}

		public override void Enable(bool enable) 
		{ 
			if(enable) Setup();
			base.Enable(enable);
		}
		#endregion
	}

	public class HangarMachineryUpdater : ModuleUpdater<HangarMachinery>
	{ 
		protected override void on_rescale(ModulePair<HangarMachinery> mp, Scale scale)
		{
			mp.module.Setup(!scale.FirstTime);
			mp.module.EnergyConsumption = mp.base_module.EnergyConsumption * scale.absolute.quad * scale.absolute.aspect; 
		}
	}
}