﻿using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class HangarStorage : HangarPassage, IPartCostModifier, IControllableModule
	{
		const float usefull_size_ratio = 0.9f;

		[KSPField(isPersistant = false)] public string HangarSpace;

		#region Internals
		//metrics
		[KSPField (isPersistant = true)] public float base_mass   = -1f;
		public Metric PartMetric { get; private set; }
		public Metric HangarMetric { get; private set; }

		//vessels storage
		readonly protected VesselsPack<StoredVessel> stored_vessels = new VesselsPack<StoredVessel>();
		readonly protected VesselsPack<PackedConstruct> packed_constructs = new VesselsPack<PackedConstruct>();
		public int VesselsDocked { get { return packed_constructs.Count+stored_vessels.Count; } }
		public float VesselsMass { get { return packed_constructs.VesselsMass+stored_vessels.VesselsMass; } }
		public float VesselsCost { get { return packed_constructs.VesselsCost+stored_vessels.VesselsCost; } }
		public float UsedVolume  { get { return packed_constructs.UsedVolume+stored_vessels.UsedVolume; } }
		public float FreeVolume
		{ get { if(HangarMetric.Empty) return 0; return HangarMetric.volume-UsedVolume; } }
		public float UsedVolumeFrac 
		{ get { if(HangarMetric.Empty) return 0; return UsedVolume/HangarMetric.volume; } }
		//coordination
		readonly List<HangarStorage> storage_cecklist = new List<HangarStorage>();
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
		void build_storage_checklist()
		{
			if(!HighLogic.LoadedSceneIsFlight) return;
			storage_cecklist.Clear();
			foreach(Part p in vessel.parts)
			{
				if(p == part) break;
				storage_cecklist.AddRange(p.Modules.OfType<HangarStorage>());
			}
		}

		bool other_storages_ready
		{
			get
			{
				if(storage_cecklist.Count == 0) return true;
				bool ready = true;
				foreach(var h in storage_cecklist)
				{ ready &= h.Ready; if(!ready) break; }
				return ready;
			}
		}

		protected override void early_setup(StartState state)
		{
			base.early_setup(state);
			build_storage_checklist();
		}

		protected override void start_coroutines()
		{ StartCoroutine(convert_constructs_to_vessels()); }

		public override void Setup(bool reset = false)
		{
			base.Setup(reset);
			//initialize part and hangar metric
			PartMetric = new Metric(part);
			HangarMetric = HangarSpace != string.Empty ? new Metric(part, HangarSpace) : null;
			//if hangar metric is not provided, derive it from part metric
			if(HangarMetric == null || HangarMetric.Empty)
				HangarMetric = PartMetric*usefull_size_ratio;
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
				constructs.ForEach(pc => try_store_vessel(pc));
			}
			//then set other part parameters
			set_part_params(reset);
		}

		virtual protected void on_set_part_params()
		{
			var el = EditorLogic.fetch;
			if(el != null) GameEvents.onEditorShipModified.Fire(el.ship);
		}

		virtual protected void set_part_params(bool reset = false) 
		{
			if(base_mass < 0 || reset) base_mass = part.mass;
			_stored_vessels = VesselsDocked.ToString();
			_stored_mass    = Utils.formatMass(VesselsMass);
			_stored_cost    = VesselsCost.ToString();
			_used_volume    = Utils.formatPercent(UsedVolumeFrac);
			part.mass = base_mass+VesselsMass;
			on_set_part_params();
		}

		public float GetModuleCost() { return VesselsCost; }
		#endregion

		#region For HangarWindow
		public List<StoredVessel> GetVessels() { return stored_vessels.Values; }
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
		#endregion

		#region Logistics
		public override bool CanHold(PackedVessel vsl)
		{
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
				ScreenMessager.showMessage("Unable to move \"{0}\" from {1} to {2}",
					vsl.name, name, other.name);
				return false;
			}
			if(!RemoveVessel(vsl))
			{ 
				this.Log("TryTransferTo: trying to remove a PackedVessel that is not present."); 
				return false; 
			}
			other.StoreVessel(vsl);
			return true;
		}
		#endregion

		#region Storage
		public void StoreVessel(PackedVessel v)
		{ 
			var pc = v as PackedConstruct;
			var sv = v as StoredVessel;
			if(pc != null) packed_constructs.ForceAdd(pc); 
			else if(sv != null) stored_vessels.ForceAdd(sv);
			else this.Log("Unknown PackedVessel type: {0}", v);
			set_part_params(); 
		}

		public bool RemoveVessel(PackedVessel v)
		{ 
			var pc = v as PackedConstruct;
			var sv = v as StoredVessel;
			bool result = false;
			if(pc != null) result = packed_constructs.Remove(pc); 
			else if(sv != null) result = stored_vessels.Remove(sv);
			else this.Log("Unknown PackedVessel type: {0}", v);
			set_part_params(); 
			return result;
		}

		protected virtual bool try_store_vessel(PackedVessel v)
		{
			bool stored = false;
			var pc = v as PackedConstruct;
			var sv = v as StoredVessel;
			if(pc != null) stored = packed_constructs.TryAdd(pc);
			else if(sv != null) stored = stored_vessels.TryAdd(sv);
			else this.Log("Unknown PackedVessel type: {0}", v);
			if(!stored)
			{
				ScreenMessager.showMessage("There's no room for \"{0}\"", v.name);
				return false;
			}
			set_part_params();
			return true;
		}

		IEnumerator<YieldInstruction> convert_constructs_to_vessels()
		{
			if(!HighLogic.LoadedSceneIsFlight || packed_constructs.Count == 0) 
			{ Ready = true;	yield break; }
			//wait for storage.vessel to be loaded
			var self = new VesselWaiter(vessel);
			while(!self.loaded) yield return null;
			while(!enabled) yield return null;
			//wait for other storages to be ready
			while(!other_storages_ready) yield return null;
			//create vessels from constructs and store them
			foreach(PackedConstruct pc in packed_constructs.Values)
			{
				RemoveVessel(pc);
				if(!pc.LoadConstruct()) 
				{
					Utils.Log("PackedConstruct: unable to load ShipConstruct {0}. " +
						"This usually means that some parts are missing " +
						"or some modules failed to initialize.", pc.name);
					ScreenMessager.showMessage("Unable to load {0}", pc.name);
					continue;
				}
				ShipConstruction.PutShipToGround(pc.construct, part.transform);
				ShipConstruction.AssembleForLaunch(pc.construct, 
					vessel.vesselName, pc.flag, 
					FlightDriver.FlightStateCache,
					new VesselCrewManifest());
				var vsl = new VesselWaiter(FlightGlobals.Vessels[FlightGlobals.Vessels.Count - 1]);
				FlightGlobals.ForceSetActiveVessel(vsl.vessel);
				Staging.beginFlight();
				//wait for vsl to be launched
				while(!vsl.loaded) yield return null;
				//store vessel
				StoreVessel(new StoredVessel(vsl.vessel));
				//switch to storage vessel before storing
				FlightGlobals.ForceSetActiveVessel(vessel);
				//destroy vessel
				vsl.vessel.Die();
				//wait a 0.1 sec, otherwise the vessel may not be destroyed properly
				yield return new WaitForSeconds(0.1f); 
			}
			//save game afterwards
			FlightGlobals.ForceSetActiveVessel(vessel);
			while(!self.loaded) yield return null;
			yield return new WaitForSeconds(0.5f);
			GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
			Ready = true;
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
		}

		public override void OnLoad(ConfigNode node)
		{ 
			base.OnLoad(node);
			//restore stored vessels
			if(node.HasNode("STORED_VESSELS"))
				stored_vessels.Load(node.GetNode("STORED_VESSELS"));
			if(node.HasNode("PACKED_CONSTRUCTS"))
				packed_constructs.Load(node.GetNode("PACKED_CONSTRUCTS"));
		}
		#endregion

		#region ControllableModule
		protected ModuleGUIState gui_state;
		virtual public bool CanEnable() { return true; }
		virtual public bool CanDisable() 
		{ 
			if(stored_vessels.Count > 0 || packed_constructs.Count > 0)
			{
				ScreenMessager.showMessage("Cannot deflate: there are vessels inside");
				return false;
			}
			return true;
		}

		virtual public void Enable(bool enable) 
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
