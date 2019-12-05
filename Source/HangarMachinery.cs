//   HangarMachinery.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AT_Utils;
using AT_Utils.UI;

namespace AtHangar
{
    public abstract partial class HangarMachinery : ControllableModuleBase, IPartMassModifier
    {
        public enum HangarState { Inactive, Active }

        #region Configuration
        //hangar properties
        [KSPField] public string AnimatorID = string.Empty;
        [KSPField] public string DamperID = string.Empty;
        [KSPField] public float EnergyConsumption = 0.75f;
        [KSPField] public bool NoCrewTransfers;
        [KSPField] public bool NoResourceTransfers;
        [KSPField] public bool NoGUI;
        [KSPField] public bool PayloadFixedInFlight;
        //vessel spawning
        [KSPField] public Vector3 LaunchVelocity = Vector3.zero;
        [KSPField(isPersistant = true)] public bool LaunchWithPunch;
        [KSPField] public string CheckDockingPorts = string.Empty;
        //other
        [KSPField] public string Trigger = string.Empty;
        #endregion

        #region Managed Storage
        HangarStorage storage;
        public HangarStorage Storage
        {
            get => storage;
            protected set
            {
                if(storage != value)
                {
                    if(storage != null)
                        on_storage_remove(storage);
                    if(value != null)
                        on_storage_add(value);
                    storage = value;
                }
            }
        }

        protected virtual void on_storage_remove(HangarStorage old_storage)
        {
            old_storage.OnVesselStored -= highlight_content_fit;
            old_storage.OnVesselUnfittedAdded -= highlight_content_unfit;
            old_storage.OnVesselRemoved -= disable_highlight;
            old_storage.OnVesselUnfittedRemoved -= disable_highlight;
            old_storage.OnStorageEmpty -= disable_highlight;
        }

        protected virtual void on_storage_add(HangarStorage new_storage)
        {
            new_storage.OnVesselStored += highlight_content_fit;
            new_storage.OnVesselUnfittedAdded += highlight_content_unfit;
            new_storage.OnVesselRemoved += disable_highlight;
            new_storage.OnVesselUnfittedRemoved += disable_highlight;
            new_storage.OnStorageEmpty += disable_highlight;
        }

        public virtual Vector3 DockSize => Storage == null ? Vector3.zero : Storage.Size;
        public float Volume => Storage == null ? 0f : Storage.Volume;
        public int VesselsDocked => Storage == null ? 0 : Storage.VesselsCount;
        public float VesselsMass => Storage == null ? 0f : Storage.VesselsMass;
        public float VesselsCost => Storage == null ? 0f : Storage.VesselsCost;
        public float UsedVolume => Storage == null ? 0f : Storage.UsedVolume;
        public float UsedVolumeFrac => Storage == null || Storage.Volume.Equals(0) ? 1 : UsedVolume / Volume;

        public List<PackedVessel> GetVessels() => Storage == null ? new List<PackedVessel>() : Storage.GetVessels();

        //vessels storage
        protected List<HangarPassage> passage_checklist = new List<HangarPassage>();
        readonly protected List<ModuleDockingNode> docks_checklist = new List<ModuleDockingNode>();
        readonly public List<HangarStorage> ConnectedStorage = new List<HangarStorage>();
        public int TotalVesselsDocked;
        public float TotalVolume;
        public float TotalUsedVolume;
        public float TotalStoredMass;
        public float TotalCostMass;
        public float TotalUsedVolumeFrac => TotalUsedVolume / TotalVolume;
        public bool CanRelocate => ConnectedStorage.Count > 1;
        #endregion

        #region Machinery
        public Metric PartMetric { get; private set; }

        protected virtual SpawnSpaceManager spawn_space_manager => Storage?.SpawnManager;
        protected VesselSpawner vessel_spawner;
        protected PackedVessel spawning_vessel;
        bool spawning_vessel_on_rails;

        protected MultiAnimator hangar_gates;
        protected ATMagneticDamper hangar_damper;
        public AnimatorState gates_state => hangar_gates == null ? AnimatorState.Opened : hangar_gates.State;
        public HangarState hangar_state { get; private set; }

        public VesselResources HangarResources { get; private set; }
        readonly public ResourceManifestList ResourceTransferList = new ResourceManifestList();

        readonly List<SpatialSensor> Triggers = new List<SpatialSensor>();
        readonly Dictionary<Guid, MemoryTimer> probed_vessels = new Dictionary<Guid, MemoryTimer>();

        [KSPField(isPersistant = true)] Vector3 momentumDelta = Vector3.zero;
        [KSPField(isPersistant = true)] bool apply_force;

        [SerializeField] public ConfigNode ModuleConfig;

        public bool IsControllable => vessel.CurrentControlLevel == Vessel.ControlLevel.FULL ||
                    vessel.CurrentControlLevel == Vessel.ControlLevel.PARTIAL_MANNED ||
                    part.protoModuleCrew.Count > 0;

        protected ResourcePump socket;
        #endregion

        #region GUI
        [KSPField(guiName = "Hangar Name", guiActive = true, guiActiveEditor = true, isPersistant = true)]
        public string HangarName = "_none_";

        public override string GetInfo()
        {
            var info = "";
            //energy consumption
            var gates = part.GetAnimator<MultiAnimator>(AnimatorID);
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
            //vessel spawner
            vessel_spawner = gameObject.AddComponent<VesselSpawner>();
            //content hull mesh
            var obj = new GameObject("ContentHullMesh", typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(gameObject.transform, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            content_hull_mesh = obj.GetComponent<MeshFilter>();
            content_hull_renderer = obj.GetComponent<MeshRenderer>();
            content_hull_renderer.material = Utils.no_z_material;
            content_hull_renderer.material.color = Colors.Good.Alpha(0.25f);
            content_hull_renderer.enabled = true;
            obj.SetActive(false);
            //content orientation hint mesh
            obj = new GameObject("ContentOrientationHint", typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(content_hull_mesh.transform, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            content_orientation_hint = obj.GetComponent<MeshFilter>();
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.material = Utils.no_z_material;
            renderer.material.color = Colors.Selected1.Alpha(0.25f);
            renderer.enabled = true;
            obj.SetActive(true);
            //utility components
            hangar_name_editor = gameObject.AddComponent<SimpleTextEntry>();
            hangar_name_editor.Title = "Rename Hangar";
            hangar_name_editor.yesCallback = () => HangarName = hangar_name_editor.Text;
            //game events
            GameEvents.onVesselWasModified.Add(update_connected_storage);
            GameEvents.onEditorShipModified.Add(update_connected_storage);
            GameEvents.onPartDie.Add(on_part_die);
        }

        public virtual void OnDestroy()
        {
            Destroy(vessel_spawner);
            Destroy(hangar_name_editor);
            Destroy(content_hull_mesh.gameObject);
            Destroy(content_orientation_hint.gameObject);
            if(vessels_window != null) Destroy(vessels_window);
            if(construct_loader != null) Destroy(construct_loader);
            GameEvents.onVesselWasModified.Remove(update_connected_storage);
            GameEvents.onEditorShipModified.Remove(update_connected_storage);
            GameEvents.onPartDie.Remove(on_part_die);
            Storage = null;
        }

        void update_resources()
        {
            if(vessel != null)
                HangarResources = new VesselResources(vessel);
        }

        protected bool all_passages_ready => passage_checklist.All(p => p.Ready);

        protected abstract List<HangarPassage> get_connected_passages();

        private void build_connected_storage()
        {
            ConnectedStorage.Clear();
            var connected_passages = get_connected_passages();
            if(connected_passages == null)
                return;
            foreach(var p in connected_passages)
            {
                if(p is HangarStorage other_storage)
                    ConnectedStorage.Add(other_storage);
            }
        }

        void update_total_values()
        {
            TotalVesselsDocked = 0;
            TotalVolume = 0;
            TotalUsedVolume = 0;
            TotalStoredMass = 0;
            TotalCostMass = 0;
            foreach(var s in ConnectedStorage)
            {
                TotalVesselsDocked += s.VesselsCount;
                TotalVolume += s.Volume;
                TotalUsedVolume += s.UsedVolume;
                TotalStoredMass += s.VesselsMass;
                TotalCostMass += s.VesselsCost;
            }
        }

        protected virtual void update_connected_storage()
        {
            build_connected_storage();
            update_total_values();
            Events["RelocateVessels"].guiActiveEditor = CanRelocate;
            this.EnableModule(Storage != null);
        }

        protected virtual void update_connected_storage(Vessel vsl)
        {
            if(vsl == null || vsl != part.vessel || !all_passages_ready) return;
            update_connected_storage();
        }

        void update_connected_storage(ShipConstruct ship)
        {
            if(!all_passages_ready) return;
            update_connected_storage();
        }

        IEnumerator<YieldInstruction> delayed_update_connected_storage()
        {
            while(!all_passages_ready)
                yield return null;
            update_connected_storage();
        }

        protected virtual void early_setup(StartState state)
        {
            var el = EditorLogic.fetch;
            if(el != null)
            {
                //set vessel type
                facility = el.ship.shipFacility;
                //initialize ship construct loader and vessel transfer window
                vessels_window = gameObject.AddComponent<VesselTransferWindow>();
                construct_loader = gameObject.AddComponent<ShipConstructLoader>();
                construct_loader.process_construct = process_construct;
            }
            //setup triggers
            //prevent triggers from catching raycasts
            if(!string.IsNullOrEmpty(Trigger))
            {
                var triggers = part.FindModelComponents<Collider>(Trigger);
                var layer = state == StartState.Editor ? 21 : 2; // Part Triggers : Ignore Raycasts
                triggers.ForEach(c => c.gameObject.layer = layer);
                if(vessel != null)
                    triggers.ForEach(c => Triggers.Add(SpatialSensor.AddToCollider(c, vessel, 1, on_trigger)));
            }
            //setup hangar name
            if(HangarName == "_none_") HangarName = part.Title();
            //initialize resources
            update_resources();
            //initialize Animator
            hangar_gates = part.GetAnimator<MultiAnimator>(AnimatorID);
            if(hangar_gates == null)
            {
                Events["Open"].active = false;
                Events["Close"].active = false;
            }
            if(EnergyConsumption > 0)
                socket = part.CreateSocket();
            //setup magnetic damper
            hangar_damper = ATMagneticDamper.GetDamper(part, DamperID);
            //get docking ports that are inside hangar sapace
            var docks = part.Modules.GetModules<ModuleDockingNode>();
            foreach(var d in CheckDockingPorts.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                docks_checklist.AddRange(docks.Where(m => m.referenceAttachNode == d));
            //get all passages in the vessel
            passage_checklist = part.AllModulesOfType<HangarPassage>();
            //vessel spawner
            vessel_spawner.Init(part);
        }

        protected virtual void start_coroutines()
        {
            StartCoroutine(delayed_update_connected_storage());
        }

        /// <summary>
        /// Sets up internal properties that depend on Storage
        /// and may change with resizing.
        /// Overrides should always check if Storage is not null.
        /// </summary>
        /// <param name="reset">If set to <c>true</c> reset state befor setup.</param>
        public virtual void Setup(bool reset = false)
        {
            PartMetric = new Metric(part);
            if(highlighted_content != null)
                update_content_hull_mesh();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            early_setup(state);
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
            if(ModuleConfig == null)
                ModuleConfig = node;
            if(node.HasValue("hangarState"))
                hangar_state = (HangarState)Enum.Parse(typeof(HangarState), node.GetValue("hangarState"));
        }
        #endregion

        #region Updates
        public virtual void FixedUpdate()
        {
            if(HighLogic.LoadedSceneIsFlight)
            {
                //change vessel velocity if requested
                if(apply_force)
                {
                    if(!momentumDelta.IsZero())
                    {
                        part.Rigidbody.AddForce(momentumDelta, ForceMode.Impulse);
                        momentumDelta = Vector3.zero;
                    }
                    apply_force = false;
                }
                //consume energy if hangar is operational
                if(socket != null && hangar_state == HangarState.Active)
                {
                    socket.RequestTransfer(EnergyConsumption * TimeWarp.fixedDeltaTime);
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
        protected virtual bool hangar_is_ready(Vessel vsl)
        {
            //always check relative velocity and acceleration
            Vector3 rv = vessel.GetObtVelocity() - vsl.GetObtVelocity();
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

        /// <summary>
        /// Chech if a Vessel can be stored in flight in using this hangar.
        /// </summary>
        /// <param name="vsl">Vessel.</param>
        protected virtual bool can_store_vessel(Vessel vsl)
        {
            //check vessel crew
            var vsl_crew = vsl.GetCrewCount();
            if(NoCrewTransfers && vsl_crew > 0)
            {
                Utils.Message("Crew cannot enter through this hangar. Leave your ship before docking.");
                return false;
            }
            if(vsl_crew > vessel.GetCrewCapacity() - vessel.GetCrewCount())
            {
                Utils.Message("Not enough space for the crew of a docking vessel");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Check if a PackedVessel can be stored in flight or in editor using this hangar.
        /// Use this to implement different logic for in-flight and in-editor vessel storing.
        /// </summary>
        /// <param name="vsl">Vessel.</param>
        /// <param name="in_flight">If set to <c>true</c>, the PackedVessel is stored in flight.</param>
        protected virtual bool can_store_packed_vessel(PackedVessel vsl, bool in_flight) => true;

        /// <summary>
        /// Try to store a PackedVessel in flight or in editor using this hangar.
        /// Use this to implement different logic for in-flight and in-editor vessel storing.
        /// </summary>
        /// <param name="vsl">Vessel.</param>
        /// <param name="in_flight">If set to <c>true</c>, the PackedVessel is stored in flight.</param>
        protected virtual bool try_store_packed_vessel(PackedVessel vsl, bool in_flight)
        {
            if(!can_store_packed_vessel(vsl, in_flight))
                return false;
            return in_flight
                ? Storage.TryStoreVessel(vsl, false, false)
                : Storage.TryStoreVesselInEditor(vsl);
        }

        static int snap_angle(float a)
        {
            var a90 = Mathf.RoundToInt(a / 90);
            if(a90 > 0)
                a90 = a90 % 4;
            else
                a90 = 4 - (-a90 % 4);
            if(a90 == 4)
                a90 = 0;
            return a90 * 90;
        }

        static Vector3 snap_vector3(Vector3 vec) =>
        new Vector3(snap_angle(vec.x), snap_angle(vec.y), snap_angle(vec.z));

        /// <summary>
        /// Try to store a Vessel in flight.
        /// </summary>
        /// <returns>The corresponding StoredVessel or null.</returns>
        /// <param name="vsl">Vessel.</param>
        StoredVessel try_store_vessel(Vessel vsl)
        {
            if(can_store_vessel(vsl))
            {
                //check vessel metrics
                var sv = new StoredVessel(vsl)
                {
                    SpawnRotation = snap_vector3(spawn_space_manager.GetSpawnRotation(vsl.vesselTransform).eulerAngles)
                };
                if(try_store_packed_vessel(sv, true))
                {
                    SetHighlightedContent(null);
                    return sv;
                }
                HighlightContentTemporary(sv, 5, ContentState.DoesntFit);
            }
            return null;
        }

        /// <summary>
        /// Process a vessel that triggered the hangar.
        /// </summary>
        /// <param name="vsl">Vessel</param>
        void process_vessel(Vessel vsl)
        {
            //if the vessel is new, check momentary states
            if(!hangar_is_ready(vsl)) return;
            //if the state is OK, try to store the vessel
            var stored_vessel = try_store_vessel(vsl);
            //vessel does not fit into storage
            if(stored_vessel == null) return;
            //deactivate the hangar
            Deactivate();
            //calculate velocity change to conserve momentum
            momentumDelta = (vsl.orbit.vel - vessel.orbit.vel).xzy * stored_vessel.mass;
            apply_force = true;
            //get vessel crew on board
            stored_vessel.ExtractProtoVesselCrew(vessel, part);
            //update display values
            update_total_values();
            //switch to the hangar if needed
            if(FlightGlobals.ActiveVessel == vsl)
            {
                FlightCameraOverride.AnchorForSeconds(FlightCameraOverride.Mode.Hold, vessel.transform, 1);
                FlightGlobals.ForceSetActiveVessel(vessel);
                FlightInputHandler.SetNeutralControls();
            }
            //respawn crew portraits
            if(stored_vessel.crew.Count > 0)
                CrewTransferBatch.respawnCrew(vsl, vessel);
            //destroy vessel
            vsl.Die();
            Utils.Message("\"{0}\" has been docked inside the hangar", stored_vessel.name);
        }

        /// <summary>
        /// Check if the hangar's internal space is occupied in a way that prevents vessel spawning.
        /// </summary>
        protected virtual bool hangar_is_occupied() =>
        !(Triggers.TrueForAll(t => t.Empty)
            && docks_checklist.TrueForAll(d => d.vesselInfo == null)
            && spawn_space_manager.SpawnSpaceEmpty);

        protected virtual void on_trigger(Part p)
        {
            if(p != null && p.vessel != null && !p.vessel.isEVA
               && hangar_state == HangarState.Active)
                process_vessel(p.vessel);
        }
        #endregion

        #region Restore
        #region Positioning
        protected virtual IEnumerable<YieldInstruction> before_vessel_launch(PackedVessel vsl) { yield break; }

        protected virtual void on_vessel_positioned(Vessel vsl)
        {

        }

        protected virtual void on_vessel_loaded(Vessel vsl) { }

        protected virtual void on_vessel_off_rails(Vessel vsl)
        {
            spawning_vessel_on_rails = false;
            if(hangar_damper != null)
            {
                if(LaunchWithPunch && !LaunchVelocity.IsZero())
                    hangar_damper.EnableDamper(false);
                else
                {
                    hangar_damper.EnableDamper(true);
                    hangar_damper.AttractorEnabled = false;
                }
            }
        }

        protected virtual void process_on_vessel_launched_data(BaseEventDetails data) { }

        protected virtual void on_vessel_launched(Vessel vsl)
        {
            if(spawning_vessel != null)
                CrewTransferBatch.moveCrew(vessel, vsl, spawning_vessel.crew);
            var data = new BaseEventDetails(BaseEventDetails.Sender.STAGING);
            data.Set<PartModule>("hangar", this);
            process_on_vessel_launched_data(data);
            vsl.Parts.ForEach(p => p.SendEvent("onLaunchedFromHangar", data));
            if(hangar_damper != null
               && hangar_damper.DamperEnabled
               && !hangar_damper.EnableControls)
                StartCoroutine(CallbackUtil
                    .DelayedCallback(5f, hangar_damper.EnableDamper, false));
        }

        protected virtual void on_part_die(Part p)
        {
            if(p == part && vessel_spawner.LaunchInProgress)
                GameEvents.onShowUI.Fire();
        }

        public void SetSpawnRotation(PackedVessel vsl, Vector3 spawn_rotation)
        {
            if(HighLogic.LoadedSceneIsFlight && PayloadFixedInFlight)
                return;
            var old_rotation = vsl.SpawnRotation;
            vsl.SpawnRotation = spawn_rotation;
            var fits = spawn_space_manager.MetricFits(vsl.metric, vsl.GetSpawnRotation());
            if(HighLogic.LoadedSceneIsFlight)
            {
                if(!fits)
                {
                    vsl.SpawnRotation = old_rotation;
                    Utils.Message("Cannot rotate the vessel that way inside the hangar");
                }
                HighlightContentTemporary(vsl, 5);
            }
            else if(vsl is PackedConstruct pc)
            {
                if(fits)
                {
                    if(Storage.RemoveUnfit(pc))
                        Storage.TryStoreVessel(pc, false, false);
                }
                else if(Storage.RemoveVessel(pc))
                    Storage.AddUnfit(pc);
                SetHighlightedContent(pc);
            }
        }

        public void StepChangeSpawnRotation(PackedVessel vsl, int idx, bool clockwise)
        {
            var rotation = vsl.GetSpawnRotation() ?? Quaternion.identity;
            var angle = clockwise ? 90 : -90;
            var axis = Vector3.zero;
            axis[idx] = 1;
            SetSpawnRotation(vsl, snap_vector3((Quaternion.AngleAxis(angle, axis) * rotation).eulerAngles));
        }

        protected virtual Transform get_spawn_transform(PackedVessel pv, out Vector3 spawn_offset) =>
        spawn_space_manager.GetSpawnTransform(pv.metric, out spawn_offset, pv.GetSpawnRotation());

        IEnumerator launch_vessel(PackedVessel vsl)
        {
            if(!HighLogic.LoadedSceneIsFlight) yield break;
            while(!FlightGlobals.ready) yield return null;
            FlightCameraOverride.AnchorForSeconds(FlightCameraOverride.Mode.Hold, part.transform, 1);
            spawning_vessel_on_rails = true;
            spawning_vessel = vsl;
            if(hangar_damper != null)
                hangar_damper.EnableDamper(false);
            vessel_spawner.BeginLaunch();
#if !DEBUG
            GameEvents.onHideUI.Fire();
#endif
            yield return null;
            foreach(var yi in before_vessel_launch(vsl))
                yield return yi;
            yield return new WaitForFixedUpdate();
            TransferResources(vsl);
            var dV = Vector3.zero;
            Vector3 spawn_offset;
            var spawn_transfrom = get_spawn_transform(vsl, out spawn_offset);
            spawn_offset -= vsl.metric.center;
            if(LaunchWithPunch)
                dV = LaunchVelocity.Local2LocalDir(part.partTransform, spawn_transfrom);
            if(vsl is StoredVessel sv)
            {
                //this is for compatibility with the old crew transfer framework
                //to prevent crew duplication
                sv.RemoveProtoVesselCrew();
                vessel_spawner.SpawnProtoVessel(sv.proto_vessel,
                    spawn_transfrom,
                    spawn_offset,
                    dV,
                    null,
                    on_vessel_positioned,
                    on_vessel_loaded,
                    on_vessel_off_rails,
                    on_vessel_launched);
                yield return vessel_spawner.WaitForLaunch;
            }
            else if(vsl is PackedConstruct pc)
            {
                pc.LoadConstruct();
                if(pc.construct == null)
                {
                    Utils.Log("Unable to load ShipConstruct {}. " +
                              "This usually means that some parts are missing " +
                              "or some modules failed to initialize.", pc.name);
                    Utils.Message("Something went wrong. Ship cannot be launched.");
                    GameEvents.onShowUI.Fire();
                    vessel_spawner.AbortLaunch();
                    spawning_vessel = null;
                    yield break;
                }
                pc.construct.Parts[0].localRoot.transform.rotation = Quaternion.identity;
                vessel_spawner.SpawnShipConstruct(pc.construct,
                    spawn_transfrom,
                    spawn_offset,
                    dV,
                    on_vessel_positioned,
                    on_vessel_loaded,
                    on_vessel_off_rails,
                    on_vessel_launched);
                yield return vessel_spawner.WaitForLaunch;
            }
            GameEvents.onShowUI.Fire();
            spawning_vessel = null;
        }
        #endregion

        #region Resources
        public void PrepareResourceList(PackedVessel sv)
        {
            if(ResourceTransferList.Count > 0) return;
            ResourceTransferList.NewTransfer(HangarResources, sv.resources);
        }

        public void UpdateResourceList()
        {
            update_resources();
            ResourceTransferList.UpdateHostInfo(HangarResources);
        }

        public void TransferResources(PackedVessel sv)
        {
            if(ResourceTransferList.Count == 0) return;
            double dM, dC;
            ResourceTransferList.TransferResources(HangarResources, sv.resources, out dM, out dC);
            sv.mass += (float)dM; sv.cost += (float)dC;
            Storage.UpdateParams();
            update_total_values();
        }
        #endregion

        protected virtual bool can_restore(PackedVessel v)
        {
            //if hangar is not ready
            if(vessel_spawner.LaunchInProgress)
            {
                Utils.Message("Launch is in progress");
                return false;
            }
            if(hangar_gates != null && hangar_gates.State != AnimatorState.Opened)
            {
                Utils.Message("Open hangar gates first");
                return false;
            }
            if(hangar_is_occupied())
            {
                Utils.Message("Cannot launch a vessel when something is inside the docking space");
                return false;
            }
            if(!spawn_space_manager.MetricFits(v.metric, v.GetSpawnRotation()))
            {
                Utils.Message("Cannot launch in this orientation");
                return false;
            }
            return true;
        }

        public bool TryRestoreVessel(PackedVessel stored_vessel)
        {
            if(HighLogic.LoadedSceneIsFlight && can_restore(stored_vessel))
            {
                Utils.SaveGame(stored_vessel.name + "-before_launch", false);
                if(Storage.RemoveVessel(stored_vessel))
                {
                    Deactivate();
                    StartCoroutine(launch_vessel(stored_vessel));
                    return true;
                }
                this.Log("WARNING: restored vessel is not found in the Stored Vessels: {0}\n" +
                             "This should never happen!", stored_vessel.id);
            }
            return false;
        }
        #endregion

        #region Events&Actions
        //events
        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Open gates", active = true)]
        public void Open()
        {
            if(hangar_gates == null) return;
            hangar_gates.Open();
            Events["Open"].active = false;
            Events["Close"].active = true;
            Activate();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Close gates", active = false)]
        public void Close()
        {
            if(hangar_gates == null) return;
            hangar_gates.Close();
            Events["Open"].active = true;
            Events["Close"].active = false;
            Deactivate();
        }

        public void Activate()
        {
            hangar_state = HangarState.Active;
            if(hangar_damper != null && hangar_damper.HasAttractor)
            {
                hangar_damper.EnableDamper(true);
                hangar_damper.AttractorEnabled = true;
            }
        }

        public void Deactivate()
        {
            hangar_state = HangarState.Inactive;
        }

        public void Toggle()
        {
            if(hangar_state == HangarState.Active)
                Deactivate();
            else
                Activate();
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

        public virtual float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            if(spawning_vessel != null && spawning_vessel_on_rails)
                return spawning_vessel.mass;
            return 0;
        }

        public virtual ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.CONSTANTLY;
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
