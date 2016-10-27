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
		#region AutoRotation
		static readonly Quaternion xyrot = Quaternion.Euler(0, 0, 90);
		static readonly Quaternion xzrot = Quaternion.Euler(0, 90, 0);
		static readonly Quaternion yzrot = Quaternion.Euler(90, 0, 0);
		static readonly Quaternion[,] swaps = 
		{
			{Quaternion.identity, 	xyrot, 					xzrot}, 
			{xyrot.Inverse(), 		Quaternion.identity, 	yzrot}, 
			{xzrot.Inverse(), 		yzrot.Inverse(), 		Quaternion.identity}
		};
		#endregion

		#region callbacks
		public delegate void StoredVesselHandler(StoredVessel sv);
		public delegate void PackedConstructHandler(PackedConstruct pc);

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
		//metrics
		public Metric PartMetric { get; protected set; }
		public Metric HangarMetric { get; protected set; }

		//hangar space
		[KSPField] public string HangarSpace = string.Empty;
		[KSPField] public string SpawnTransform;
		[KSPField] public float  UsefulSizeRatio = 0.9f; //in case no HangarSpace is provided and the part metric is used
		[KSPField] public bool   AutoPositionVessel;
		[KSPField] public Vector3 SpawnOffset = Vector3.zero;
		public virtual bool ComputeHull { get { return Nodes.Count > 0 || hangar_space != null; } }
		public MeshFilter hangar_space { get; protected set; }
		public Transform  spawn_transform { get; protected set; }

		//vessels storage
		readonly static string SCIENCE_DATA = typeof(ScienceData).Name;
		readonly List<ConfigNode> stored_vessels_science = new List<ConfigNode>();
		readonly protected VesselsPack<StoredVessel> stored_vessels = new VesselsPack<StoredVessel>(Globals.Instance.EnableVesselPacking);
		readonly protected VesselsPack<PackedConstruct> packed_constructs = new VesselsPack<PackedConstruct>(Globals.Instance.EnableVesselPacking);
		readonly protected List<PackedConstruct> unfit_constructs = new List<PackedConstruct>();
		public Vector3 Size { get { return HangarMetric.size; } }
		public float Volume { get { return HangarMetric.volume; } }
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
			update_metrics();
			var info = base.GetInfo();
			if(HangarMetric.volume > 0)
			{
				info += string.Format("Available Volume: {0}\n", Utils.formatVolume(HangarMetric.volume));
				info += string.Format("Dimensions: {0}\n", Utils.formatDimensions(HangarMetric.size));
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
			if(AutoPositionVessel) 
				SpawnOffset = Vector3.zero;
			if(HangarSpace != string.Empty)
				hangar_space = part.FindModelComponent<MeshFilter>(HangarSpace);
			if(SpawnTransform != string.Empty)
				spawn_transform = part.FindModelTransform(SpawnTransform);
			if(spawn_transform == null)
			{
				var launch_empty = new GameObject();
				var parent = hangar_space != null? hangar_space.transform : part.transform;
				launch_empty.transform.SetParent(parent);
				spawn_transform = launch_empty.transform;
			}
			if(hangar_space != null)
			{
				//check if the hangar space has its normals flipped iside; if not, flip them
				var flipped = false;
				var mesh   = hangar_space.sharedMesh;
				var tris   = mesh.triangles;
				var verts  = mesh.vertices;
				var center = mesh.bounds.center;
				for(int i = 0, len = tris.Length/3; i < len; i++)
				{
					var j = i*3;
					var p = new Plane(verts[tris[j]], verts[tris[j+1]], verts[tris[j+2]]);
					var outside = !p.GetSide(center);
					if(outside)
					{
						var t = tris[j];
						tris[j] = tris[j+2];
						tris[j+2] = t;
						flipped = true;
					}
				}
				if(flipped)
				{
					this.Log("The '{}' mesh is not flipped. Hangar space normals should be pointed INSIDE.", HangarSpace);
					mesh.triangles = tris;
					mesh.RecalculateNormals();
				}
			}
			build_storage_checklist();
		}

		protected override void start_coroutines()
		{ StartCoroutine(convert_constructs_to_vessels()); }

		protected virtual void update_metrics()
		{
			PartMetric = new Metric(part);
			HangarMetric = new Metric(part, HangarSpace);
			//if hangar metric is not provided, derive it from part metric
			if(HangarMetric.Empty) HangarMetric = PartMetric*UsefulSizeRatio;
		}

		static List<KeyValuePair<float, int>> sort_vector(Vector3 v)
		{
			var s = new List<KeyValuePair<float, int>>(3);
			s.Add(new KeyValuePair<float, int>(v[0], 0));
			s.Add(new KeyValuePair<float, int>(v[1], 1));
			s.Add(new KeyValuePair<float, int>(v[2], 2));
			s.Sort((x, y) => x.Key.CompareTo(y.Key));
			return s;
		}

		public Vector3 GetSpawnOffset(PackedVessel v)
		{ return SpawnOffset.IsZero() ? SpawnOffset : Vector3.Scale(v.metric.extents, SpawnOffset); }

		public Transform GetSpawnTransform(PackedVessel v = null)
		{
			if(AutoPositionVessel && v != null) 
			{
				var s_size = sort_vector(HangarMetric.size);
				var v_size = sort_vector(v.size);
				var r1 = swaps[s_size[0].Value, v_size[0].Value];
				var i2 = s_size[0].Value == v_size[1].Value? 2 : 1;
				var r2 = swaps[s_size[i2].Value, v_size[i2].Value];
				spawn_transform.localPosition = Vector3.zero;
				spawn_transform.localRotation = Quaternion.identity;
				spawn_transform.rotation = part.transform.rotation * r2 * r1;
			}
			return spawn_transform;
		}

		public bool VesselFits(PackedVessel v)
		{
			var	position = GetSpawnTransform(v);
			var offset = GetSpawnOffset(v);
			return ComputeHull ? 
				v.metric.FitsAligned(position, hangar_space.transform, hangar_space.sharedMesh, offset) :
				v.metric.FitsAligned(position, part.partTransform, HangarMetric, offset);
		}

		void try_repack_construct(PackedConstruct pc)
		{ 
			if(VesselFits(pc) && packed_constructs.TryAdd(pc))
				OnConstructStored(pc);
			else unfit_constructs.Add(pc);
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
			stored_vessels.space = HangarMetric;
			packed_constructs.space = HangarMetric;
			//display recalculated values
			hangar_v = Utils.formatVolume(HangarMetric.volume);
			hangar_d = Utils.formatDimensions(HangarMetric.size);
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
			_used_volume    = UsedVolumeFrac.ToString("P1");
			on_set_part_params();
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

		public void UpdateParams()
		{
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

		IEnumerator<YieldInstruction> convert_constructs_to_vessels()
		{
			if(!HighLogic.LoadedSceneIsFlight || packed_constructs.Count == 0) 
			{ Ready = true;	yield break; }
			//wait for storage.vessel to be loaded
			while(!vessel.PartsStarted()) yield return WaitWithPhysics.ForNextUpdate();
			while(!enabled) yield return WaitWithPhysics.ForNextUpdate();
			//wait for other storages to be ready
			while(!other_storages_ready) yield return WaitWithPhysics.ForNextUpdate();
			//create vessels from constructs and store them
			foreach(PackedConstruct pc in packed_constructs.Values)
			{
				RemoveVessel(pc);
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
					vessel.landedAt, pc.flag, 
					FlightDriver.FlightStateCache,
					new VesselCrewManifest());
				var vsl = FlightGlobals.Vessels[FlightGlobals.Vessels.Count - 1];
				FlightGlobals.ForceSetActiveVessel(vsl);
				StageManager.BeginFlight();
				//wait for vsl to be launched
				while(!vsl.isActiveVessel || !vsl.PartsStarted()) 
					yield return WaitWithPhysics.ForNextUpdate();
				//store vessel
				StoreVessel(new StoredVessel(vsl, ComputeHull));
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
				yield return WaitWithPhysics.ForNextUpdate();
			Ready = true;
			//save game afterwards
			yield return WaitWithPhysics.ForSeconds(0.5f);
			FlightDriver.PostInitState = new GameBackup(HighLogic.CurrentGame);
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

