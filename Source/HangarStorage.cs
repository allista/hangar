//   HangarStorage.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using AT_Utils;

namespace AtHangar
{
	public class HangarStorage : HangarPassage, IPartCostModifier, IPartMassModifier, IControllableModule
	{
		#region callbacks
		public delegate void StoredVesselHandler(StoredVessel sv);
		public delegate void PackedConstructHandler(PackedConstruct pc);
		public delegate bool PackedVesselConstraint(PackedVessel pv);

		public PackedVesselConstraint FitConstraint = delegate { return true; };

		public StoredVesselHandler OnVesselStored = delegate {};
		public PackedConstructHandler OnConstructStored = delegate {};
		public StoredVesselHandler OnVesselRemoved = delegate {};
		public PackedConstructHandler OnConstructRemoved = delegate {};
		public Action OnStorageEmpty = delegate {};

		void OnPackedVesselRemoved(PackedVessel pv)
		{
			var sv = pv as StoredVessel;
			if(sv != null) { OnVesselRemoved(sv); return; }
			var pc = pv as PackedConstruct;
			if(pc != null) { OnConstructRemoved(pc); return; }
		}
		#endregion

		#region Internals
		//hangar space
		[KSPField] public string  HangarSpace = string.Empty;
		[KSPField] public string  SpawnTransform = string.Empty;
		[KSPField] public bool    AutoPositionVessel;
		[KSPField] public Vector3 SpawnOffset = Vector3.zero;
		public VesselSpawnManager SpawnManager { get; protected set; }
		public bool HasSpaceMesh { get { return SpawnManager.Space != null; } }
		public Metric PartMetric { get; protected set; }

		//vessels storage
		readonly static string SCIENCE_DATA = typeof(ScienceData).Name;
		readonly List<ConfigNode> stored_vessels_science = new List<ConfigNode>();
		readonly protected VesselsPack<StoredVessel> stored_vessels = new VesselsPack<StoredVessel>(Globals.Instance.EnableVesselPacking);
		readonly protected VesselsPack<PackedConstruct> packed_constructs = new VesselsPack<PackedConstruct>(Globals.Instance.EnableVesselPacking);
		readonly protected List<PackedConstruct> unfit_constructs = new List<PackedConstruct>();
		public Vector3 Size { get { return SpawnManager.SpaceMetric.size; } }
		public float Volume { get { return SpawnManager.SpaceMetric.volume; } }
		public int   ConstructsCount { get { return packed_constructs.Count; } }
		public int   VesselsCount { get { return stored_vessels.Count; } }
		public int   TotalVesselsDocked { get { return packed_constructs.Count+stored_vessels.Count; } }
		public float VesselsMass { get { return packed_constructs.VesselsMass+stored_vessels.VesselsMass; } }
		public float VesselsCost { get { return packed_constructs.VesselsCost+stored_vessels.VesselsCost; } }
		public float UsedVolume  { get { return packed_constructs.UsedVolume+stored_vessels.UsedVolume; } }
		public float FreeVolume	    { get { return Volume-UsedVolume; } }
		public float UsedVolumeFrac { get { return UsedVolume/Volume; } }
		//coordination
		readonly List<HangarStorage> storage_checklist = new List<HangarStorage>();
		#endregion

		#region GUI
		[KSPField (guiName = "Volume", guiActiveEditor=true)] public string hangar_v;
		[KSPField (guiName = "Size",   guiActiveEditor=true)] public string hangar_d;
		//GUI Active
		[KSPField (guiName = "Vessels",     guiActive=true, guiActiveEditor=true)] public string _stored_vessels;
		[KSPField (guiName = "Stored Mass", guiActive=true, guiActiveEditor=true)] public string _stored_mass;
		[KSPField (guiName = "Stored Cost", guiActive=true, guiActiveEditor=true)] public string _stored_cost;
		[KSPField (guiName = "Used Volume", guiActive=true, guiActiveEditor=true)] public string _used_volume;
		#endregion

		#region Setup
		public override string GetInfo() 
		{ 
			SpawnManager = new VesselSpawnManager(part);
			SpawnManager.Load(ModuleConfig);
			update_metrics();
			var info = base.GetInfo();
			if(SpawnManager.SpaceMetric.volume > 0)
			{
				info += string.Format("Available Volume: {0}\n", Utils.formatVolume(SpawnManager.SpaceMetric.volume));
				info += string.Format("Dimensions: {0}\n", Utils.formatDimensions(SpawnManager.SpaceMetric.size));
			}
			return info;
		}

		void build_storage_checklist()
		{
			if(!HighLogic.LoadedSceneIsFlight) return;
			storage_checklist.Clear();
			foreach(Part p in vessel.parts)
			{
				if(p == part) 
				{
					foreach(var s in p.Modules.OfType<HangarStorage>())
					{ 
						if(s == this) return;
						storage_checklist.Add(s);
					} return;
				}
				storage_checklist.AddRange(p.Modules.OfType<HangarStorage>());
			}
		}

		bool other_storages_ready { get { return storage_checklist.All(s => s.Ready); } }

		protected override void early_setup(StartState state)
		{
			base.early_setup(state);
			SpawnManager = new VesselSpawnManager(part);
			SpawnManager.Load(ModuleConfig);
			build_storage_checklist();
		}

		protected override void start_coroutines()
		{ StartCoroutine(convert_constructs_to_vessels()); }

		protected virtual void update_metrics()
		{
			PartMetric = new Metric(part);
			SpawnManager.UpdateMetric();
		}

		bool VesselFits(PackedVessel pv)
		{
            if(pv == null || !SpawnManager.MetricFits(pv.metric)) return false;
			var constraints = FitConstraint.GetInvocationList();
			for(int i = 0, len = constraints.Length; i < len; i++)
			{
				var cons = constraints[i] as PackedVesselConstraint;
				if(cons != null && !cons(pv)) return false;
			}
			return true;
		}

		void try_repack_construct(PackedConstruct pc)
		{ 
			if(!VesselFits(pc) || !packed_constructs.TryAdd(pc))
			{
				unfit_constructs.Add(pc);
				OnConstructRemoved(pc);
			}
		}

		void try_pack_unfit_construct(PackedConstruct pc)
		{ 
			if(VesselFits(pc) && packed_constructs.TryAdd(pc))
			{
				unfit_constructs.Remove(pc);
				OnConstructStored(pc);
			}
		}

		public override void Setup(bool reset = false)
		{
			base.Setup(reset);
			//initialize part and hangar metric
			update_metrics();
			//setup vessels packs
			stored_vessels.space = SpawnManager.SpaceMetric;
			packed_constructs.space = SpawnManager.SpaceMetric;
			//display recalculated values
			hangar_v = Utils.formatVolume(SpawnManager.SpaceMetric.volume);
			hangar_d = Utils.formatDimensions(SpawnManager.SpaceMetric.size);
			if(reset)
			{   //if resetting, try to repack vessels on resize
				var constructs = packed_constructs.Values;
				packed_constructs.Clear();
				constructs.ForEach(try_repack_construct);
				if(constructs.Count > packed_constructs.Count)
				{
					var dN = constructs.Count-packed_constructs.Count;
					Utils.Message("The storage became too small. {0} vessels {1} removed", 
						dN, dN > 1? "were" : "was");
				}
				else if(unfit_constructs.Count > 0)
				{
					constructs = unfit_constructs.ToList();
					constructs.ForEach(try_pack_unfit_construct);
				}
			}
			//then set other part parameters
			set_part_params(reset);
		}

		virtual protected void on_set_part_params()
		{
			var el = EditorLogic.fetch;
			if(el != null) GameEvents.onEditorShipModified.Fire(el.ship);
//			else if(part.vessel != null) GameEvents.onVesselWasModified.Fire(part.vessel);
		}

		virtual protected void set_part_params(bool reset = false) 
		{
			_stored_vessels = TotalVesselsDocked.ToString();
			_stored_mass    = Utils.formatMass(VesselsMass);
			_stored_cost    = VesselsCost.ToString();
			_used_volume    = Volume > 0? UsedVolumeFrac.ToString("P1") : "N/A";
			on_set_part_params();
			part.UpdatePartMenu();
		}

		public override void OnAwake()
		{ GameEvents.OnVesselRecoveryRequested.Add(onVesselRecoveryRequested); }

		public void OnDestroy()
		{ GameEvents.OnVesselRecoveryRequested.Remove(onVesselRecoveryRequested); }
		#endregion

		#region Content Management
		public List<StoredVessel>    GetVessels() { return stored_vessels.Values; }
		public List<PackedConstruct> GetConstructs() { return packed_constructs.Values; }
		public List<PackedVessel> GetVesselsBase() { return new List<PackedVessel>(stored_vessels); }
		public List<PackedVessel> GetConstructsBase() { return new List<PackedVessel>(packed_constructs); }
		public List<PackedVessel> GetAllVesselsBase() 
		{ 
			var vessels = new List<PackedVessel>(stored_vessels.Count+packed_constructs.Count);
			vessels.AddRange(packed_constructs);
			vessels.AddRange(stored_vessels);
			return vessels;
		}

		public int UnfitCount { get { return unfit_constructs.Count; } }
		public List<PackedConstruct> UnfitConstucts { get { return unfit_constructs.ToList(); } }
		public void RemoveUnfit(PackedConstruct pc) { unfit_constructs.Remove(pc); }
		public void AddUnfit(PackedConstruct pc) { unfit_constructs.Add(pc); }

		public void UpdateParams()
		{
            packed_constructs.UpdateParams();
			stored_vessels.UpdateParams();
			set_part_params();
		}

		public void ClearConstructs()
		{
			unfit_constructs.Clear();
			packed_constructs.Clear();
			OnStorageEmpty();
			set_part_params();
		}
		#endregion

		#region Logistics
		public override bool CanHold(PackedVessel vsl)
		{
			if(!VesselFits(vsl)) return false;
			var pc = vsl as PackedConstruct;
			if(pc != null) return packed_constructs.CanAdd(pc);
			var sv = vsl as StoredVessel;
			if(sv != null) return stored_vessels.CanAdd(sv);
			return false;
		}

		public bool TryTransferTo(PackedVessel vsl, HangarStorage other)
		{
			if(!CanTransferTo(vsl, other)) 
			{
				Utils.Message("Unable to move \"{0}\" from \"{1}\" to \"{2}\"",
					vsl.name, this.Title(), other.Title());
				return false;
			}
			if(!RemoveVessel(vsl))
			{ 
				this.Log("TryTransferTo: trying to remove a PackedVessel that is not present."); 
				return false; 
			}
			OnPackedVesselRemoved(vsl);
			other.StoreVessel(vsl);
			return true;
		}
		#endregion

		#region Storage
		public void StoreVessel(PackedVessel v)
		{ 
			var pc = v as PackedConstruct;
			if(pc != null) 
			{
				packed_constructs.ForceAdd(pc);
				OnConstructStored(pc);
				set_part_params();
				return;
			}
			var sv = v as StoredVessel;
			if(sv != null) 
			{
				stored_vessels.ForceAdd(sv);
				OnVesselStored(sv);
				set_part_params();
				return;
			}
			this.Log("Unknown PackedVessel type: {}", v);
		}

		public bool RemoveVessel(PackedVessel v)
		{ 
			var success = false;
			var pc = v as PackedConstruct;
			if(pc != null) 
			{
				if(packed_constructs.Remove(pc))
				{
					OnConstructRemoved(pc);
					success = true;
				}
			}
			var sv = v as StoredVessel;
			if(sv != null) 
			{
				if(stored_vessels.Remove(sv))
				{
					OnVesselRemoved(sv);
					success = true;
				}
			}
			if(success)
			{
				if(packed_constructs.Count == 0 && stored_vessels.Count == 0)
					OnStorageEmpty();
				set_part_params();
				return true;
			}
			this.Log("Unknown PackedVessel type: {}", v);
			return false;
		}

		public virtual bool TryAddUnfit(PackedVessel v)
		{
			if(HighLogic.LoadedSceneIsEditor) return false;
			var pc = v as PackedConstruct;
			if(pc == null) return false;
			unfit_constructs.Add(pc); 
			return true;
		}

		public virtual bool TryStoreVessel(PackedVessel v)
		{
			bool stored = false;
			var pc = v as PackedConstruct;
			var sv = v as StoredVessel;
			if(VesselFits(v))
			{
				if(pc != null) 
				{
					stored = packed_constructs.TryAdd(pc);
					if(stored)
					{
						OnConstructStored(pc);
						set_part_params();
					}
				}
				else if(sv != null) 
				{
					stored = stored_vessels.TryAdd(sv);
					if(stored)
					{
						OnVesselStored(sv);
						set_part_params();
					}
				}
				else { this.Log("Unknown PackedVessel type: {}", v); return false; }
				if(!stored) Utils.Message("There's no room for \"{0}\"", v.name);
			}
			else Utils.Message(5, "Insufficient vessel clearance for safe docking\n" +
			                   "\"{0}\" cannot be stored", v.name);
			if(pc != null && !stored && HighLogic.LoadedSceneIsEditor)
			{ unfit_constructs.Add(pc); return true; }
			return stored;
		}

        public bool Contains(PackedConstruct item) => packed_constructs.Contains(item);
        public bool Contains(StoredVessel item) => stored_vessels.Contains(item);
        public bool Contains(PackedVessel item) => packed_constructs.Contains(item) || stored_vessels.Contains(item);

		IEnumerator<YieldInstruction> convert_constructs_to_vessels()
		{
			if(!HighLogic.LoadedSceneIsFlight || packed_constructs.Count == 0) 
			{ Ready = true;	yield break; }
			//wait for storage.vessel to be loaded
			while(!vessel.loaded || !vessel.PartsStarted()) yield return WaitWithPhysics.ForNextUpdate();
			while(!enabled) yield return WaitWithPhysics.ForNextUpdate();
			//wait for other storages to be ready
			while(!other_storages_ready) yield return WaitWithPhysics.ForNextUpdate();
			//fix the FlightCamera to prevent it from jumping to and from converted vessels
            FlightCameraOverride.AnchorForSeconds(FlightCameraOverride.Mode.Hold, vessel.transform, 1);
			//create vessels from constructs and store them
			foreach(PackedConstruct pc in packed_constructs.Values)
			{
				FlightCameraOverride.UpdateDurationSeconds(1);
				packed_constructs.Remove(pc);
				if(!pc.LoadConstruct()) 
				{
					Utils.Log("PackedConstruct: unable to load ShipConstruct {}. " +
							  "This usually means that some parts are missing " +
							  "or some modules failed to initialize.", pc.name);
					Utils.Message("Unable to load {}", pc.name);
					continue;
				}
				ShipConstruction.PutShipToGround(pc.construct, part.transform);
				ShipConstruction.AssembleForLaunch(pc.construct, 
                                                   vessel.landedAt, vessel.displaylandedAt, pc.flag, 
                                                   FlightDriver.FlightStateCache,
                                                   new VesselCrewManifest());
				StageManager.BeginFlight();
				var vsl = FlightGlobals.Vessels[FlightGlobals.Vessels.Count - 1];
				FlightGlobals.ForceSetActiveVessel(vsl);
				//wait for vsl to be launched
				while(!vsl.isActiveVessel || !vsl.PartsStarted()) 
				{
					FlightCameraOverride.UpdateDurationSeconds(1);
					yield return WaitWithPhysics.ForNextUpdate();
				}
				//store vessel
				stored_vessels.ForceAdd(new StoredVessel(vsl));
				//switch to storage vessel before storing
				FlightGlobals.ForceSetActiveVessel(vessel);
				//destroy vessel
				vsl.Die();
				//wait a 0.1 sec, otherwise the vessel may not be destroyed properly
				yield return WaitWithPhysics.ForSeconds(0.1f);
			}
			//switch back to this.vessel and signal to other waiting storages
			FlightGlobals.ForceSetActiveVessel(vessel);
			while(!vessel.isActiveVessel || !vessel.PartsStarted()) 
			{
				FlightCameraOverride.UpdateDurationSeconds(1);
				yield return WaitWithPhysics.ForNextUpdate();
			}
			Ready = true;
			//save game afterwards
			FlightCameraOverride.UpdateDurationSeconds(1);
			yield return WaitWithPhysics.ForSeconds(0.5f);
			FlightDriver.PostInitState = new GameBackup(HighLogic.CurrentGame.Updated());
			GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
		}

		void onVesselRecoveryRequested(Vessel v)
		{
			if(v != vessel) return;
			stored_vessels_science.Clear();
			foreach(var sv in stored_vessels.Values)
				foreach(var p in sv.proto_vessel.protoPartSnapshots)
					foreach(var pm in p.modules)
					{
						var s = pm.moduleValues.GetNode(SCIENCE_DATA);
						if(s != null) stored_vessels_science.Add(s);
					}
		}
		#endregion

		#region Save-Load
		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
			//save stored vessels
			if(stored_vessels.Count > 0)
				stored_vessels.Save(node.AddNode("STORED_VESSELS"));
			if(packed_constructs.Count > 0)
				packed_constructs.Save(node.AddNode("PACKED_CONSTRUCTS"));
			//save science data of stored ships
			if(stored_vessels_science.Count > 0)
				stored_vessels_science.ForEach(n => node.AddNode(n));
		}

		public override void OnLoad(ConfigNode node)
		{ 
			base.OnLoad(node);
			//restore stored vessels
			if(node.HasNode("STORED_VESSELS"))
				stored_vessels.Load(node.GetNode("STORED_VESSELS"));
			if(node.HasNode("PACKED_CONSTRUCTS"))
				packed_constructs.Load(node.GetNode("PACKED_CONSTRUCTS"));
			//restore science data
			stored_vessels_science.Clear();
			foreach(var n in node.GetNodes(SCIENCE_DATA))
				stored_vessels_science.Add(n);

		}
		#endregion

		#region ControllableModule
		public override bool CanDisable() 
		{ 
			if(stored_vessels.Count > 0 || packed_constructs.Count > 0)
			{
				Utils.Message("Cannot disable storage: there are vessels inside");
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

		#region IPart*Modifiers
		public virtual float GetModuleCost(float defaultCost, ModifierStagingSituation situation) { return VesselsCost; }
		public virtual ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

		public virtual float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return VesselsMass; }
		public virtual ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
		#endregion
	}
}

