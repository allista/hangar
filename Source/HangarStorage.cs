using System;
using System.Collections.Generic;

namespace AtHangar
{
	public class HangarStorage : HangarPassage, IPartCostModifier, IControllableModule
	{
		const float usefull_volume_ratio = 0.888f; //only 70% of the volume (0.7^(1/3)) may be used by docking vessels

		[KSPField(isPersistant = false)] public string HangarSpace;

		#region Internals
		//metrics
		public Metric PartMetric { get; private set; }
		public Metric HangarMetric { get; private set; }

		//physical properties
		[KSPField (isPersistant = true)]  
		public float base_mass   = -1f;
		public float VesselsMass = -1f;
		public float VesselsCost = -1f;
		public float UsedVolume  = -1f;
		public float FreeVolume
		{ get { if(HangarMetric.Empty) return 0; return HangarMetric.volume-UsedVolume; } }
		public float UsedVolumeFrac 
		{ get { if(HangarMetric.Empty) return 0; return UsedVolume/HangarMetric.volume; } }

		//vessels storage
		readonly protected VesselsPack<StoredVessel> stored_vessels = new VesselsPack<StoredVessel>();
		readonly protected VesselsPack<PackedConstruct> packed_constructs = new VesselsPack<PackedConstruct>();
		public int VesselsDocked { get { return stored_vessels.Count; } }
		#endregion

		#region GUI
		[KSPField (guiName = "Volume", guiActiveEditor=true)] public string hangar_v;
		[KSPField (guiName = "Size",   guiActiveEditor=true)] public string hangar_d;
		[KSPField (guiName = "Stored Mass", guiActive=true, guiActiveEditor=true)] public string stored_mass;
		[KSPField (guiName = "Stored Cost", guiActive=true, guiActiveEditor=true)] public string stored_cost;
		#endregion

		#region Storage
		protected bool try_store_vessel(PackedConstruct pc)
		{
			if(!packed_constructs.TryAdd(pc))
			{
				ScreenMessager.showMessage("There's no room for \"{0}\"", pc.name);
				return false;
			}
			return true;
		}

		protected bool try_store_vessel(StoredVessel sv)
		{
			if(!stored_vessels.TryAdd(sv))
			{
				ScreenMessager.showMessage("There's no room for \"{0}\"", sv.name);
				return false;
			}
			return true;
		}

		public override bool CanHold(PackedVessel vsl)
		{
			var pc = vsl as PackedConstruct;
			if(pc != null) return packed_constructs.CanAdd(pc);
			var sv = vsl as StoredVessel;
			if(sv != null) return stored_vessels.CanAdd(sv);
			return false;
		}
		#endregion

		#region Setup
		protected void process_stored_vessels(Action<PackedVessel> action)
		{
			stored_vessels.Values.ForEach(v => action(v));
			packed_constructs.Values.ForEach(v => action(v));
		}

		public override void Setup(bool reset = false)
		{
			this.Log("HangarStorage.Setup");//debug
			//initialize part and hangar metric
			PartMetric = new Metric(part);
			HangarMetric = HangarSpace != string.Empty ? new Metric(part, HangarSpace) : null;
			//if hangar metric is not provided, derive it from part metric
			if(HangarMetric == null || HangarMetric.Empty)
				HangarMetric = PartMetric*usefull_volume_ratio;
			//setup vessels packs
			stored_vessels.space = HangarMetric;
			packed_constructs.space = HangarMetric;
			//display recalculated values
			hangar_v = Utils.formatVolume(HangarMetric.volume);
			hangar_d = Utils.formatDimensions(HangarMetric.size);
			//now recalculate used volume
			if(reset)
			{   //if resetting, try to repack vessels on resize
				var constructs = packed_constructs.Values;
				packed_constructs.Clear();
				constructs.ForEach(pc => try_store_vessel(pc));
				//no need to change_part_params as set_params is called later
			}
			//calculate UsedVolume
			UsedVolume = 0;
			process_stored_vessels(v => UsedVolume += v.volume);
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
			//reset values if needed
			if(base_mass < 0 || reset) 
				base_mass = part.mass;
			if(VesselsMass < 0 || reset)
			{
				VesselsMass = 0;
				process_stored_vessels(v => VesselsMass += v.mass);
				stored_mass = Utils.formatMass(VesselsMass);
			}
			if(VesselsCost < 0 || reset)
			{
				VesselsCost = 0;
				process_stored_vessels(v => VesselsCost += v.cost);
				stored_cost = VesselsCost.ToString();
			}
			//set part mass
			part.mass = base_mass+VesselsMass;
			//update Editor counters and all others that listen
			on_set_part_params();
		}

		protected void change_part_params(Metric delta, float k = 1f)
		{
			VesselsMass += k*delta.mass;
			VesselsCost += k*delta.cost;
			UsedVolume  += k*delta.volume;
			if(UsedVolume  < 0) UsedVolume = 0;
			if(VesselsMass < 0) VesselsMass = 0;
			if(VesselsCost < 0) VesselsCost = 0;
			stored_mass = Utils.formatMass(VesselsMass);
			stored_cost = VesselsCost.ToString();
			set_part_params();
		}

		public float GetModuleCost() { return VesselsCost; }
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

