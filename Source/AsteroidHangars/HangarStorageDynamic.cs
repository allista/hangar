using System.Linq;
using UnityEngine;

namespace AtHangar
{
	public class HangarStorageDynamic : HangarStorage, ITankManager
	{
		[KSPField(isPersistant = true)] public float TotalVolume;
		[KSPField(isPersistant = true)] public Vector3 StorageSize;
		[KSPField(isPersistant = true)] float TanksMass;
		[KSPField] public float  WidthToLengthRatio    = 0.5f;
		[KSPField] public float  UpdateVolumeThreshold = 0.1f; //m^3
		[KSPField] public bool   HasTankManager;
		[KSPField] public string BuildTanksFrom  = "Metal";
		[KSPField] public float  ResourcePerArea = 0.6f; // 200U/m^3, 1m^2*3mm

		public ConfigNode ModuleSave;
		SwitchableTankManager tank_manager;
		ResourcePump metal_pump;
		float max_side;

		public SwitchableTankManager GetTankManager() { return tank_manager; }

		public override float GetModuleCost(float default_cost)
		{
			var cost = base.GetModuleCost(default_cost);
			var res = PartResourceLibrary.Instance.GetDefinition(BuildTanksFrom);
			if(res != null) cost += TanksMass/res.density*res.unitCost;
			return cost;
		}

		protected override void early_setup(StartState state)
		{
			base.early_setup(state);
			Fields["hangar_v"].guiActive = true;
			Fields["hangar_d"].guiActive = true;
			max_side = Mathf.Pow(TotalVolume, 1f/3);
			//init tank manager
			if(HasTankManager)
			{
				tank_manager = new SwitchableTankManager(this);
				if(ModuleSave == null) 
				{ this.Log("ModuleSave is null. THIS SHOULD NEVER HAPPEN!"); return; }
				if(ModuleSave.HasNode(SwitchableTankManager.NODE_NAME))
					tank_manager.Load(ModuleSave.GetNode(SwitchableTankManager.NODE_NAME));
				Events["EditTanks"].active = true;
				if(BuildTanksFrom != string.Empty) 
				{
					metal_pump = new ResourcePump(part, BuildTanksFrom);
					if(!metal_pump.Valid) metal_pump = null;
					else if(TanksMass <= 0) 
						TanksMass = tank_manager.TanksVolumes
							.Aggregate(0f, (m, v) => m+metal_for_tank(v)*metal_pump.Resource.density);
				}
			}
		}

		protected override void update_metrics()
		{
			PartMetric = new Metric(part);
			HangarMetric = new Metric(StorageSize);
		}

		protected override void set_part_mass()
		{ part.mass = base_mass + VesselsMass + TanksMass;	}

		public bool AddVolume(float volume) 
		{
			if(volume < 0 || tank_manager != null && tank_manager.TanksCount > 0) return false;
			TotalVolume += volume;
			if(TotalVolume-Volume > UpdateVolumeThreshold)
			{
				max_side = Mathf.Pow(TotalVolume, 1f/3);
				StorageSize = new Vector3(max_side,max_side,max_side);
				Setup();
			}
			return true;
		}

		public bool CanAddVolume
		{ 
			get
			{
				return VesselsDocked == 0 && 
				tank_manager != null && 
				tank_manager.TanksCount == 0;
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			ModuleSave = node;
		}

		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
			if(tank_manager != null)
				tank_manager.Save(node.AddNode(SwitchableTankManager.NODE_NAME));
		}

		//workaround for ConfigNode non-serialization
		public byte[] _module_save;
		public override void OnBeforeSerialize()
		{
			base.OnBeforeSerialize();
			if(tank_manager != null)
			{
				ModuleSave = new ConfigNode();
				Save(ModuleSave);
			}
			_module_save = ConfigNodeWrapper.SaveConfigNode(ModuleSave);
		}
		public override void OnAfterDeserialize() 
		{ 
			base.OnAfterDeserialize();
			ModuleSave = ConfigNodeWrapper.RestoreConfigNode(_module_save); 
		}

		#region Tanks
		public void RescaleTanks(float relative_scale)
		{ if(tank_manager != null) tank_manager.RescaleTanks(relative_scale); }

		void change_size(float volume)
		{
			var V = Mathf.Clamp(Volume+volume, 0, TotalVolume);
			var a = Mathf.Pow(WidthToLengthRatio*V, 1f/3);
			var b = V/(a*a);
			if(volume < 0 && b > StorageSize.y)
			{ b = StorageSize.y; a = Mathf.Sqrt(V/b); }
			else if(volume > 0 && b > max_side)
			{ b = max_side; a = Mathf.Sqrt(V/b); }
			StorageSize = new Vector3(a, b, a);
			Setup();
		}

		//area is calculated for a box with sides [a, a, 2a], where a*a*2a = volume
		float metal_for_tank(float volume)
		{ return Mathf.Sign(volume)*10*Mathf.Pow(Mathf.Abs(volume)/2, 2f/3)*ResourcePerArea; }

		bool convert_metal(float volume)
		{
			var metal = metal_for_tank(volume);
			metal_pump.RequestTransfer(metal);
			if(metal_pump.TransferResource())
			{
				if(metal > 0)
				{
					if(metal_pump.PartialTransfer) 
					{
						ScreenMessager.showMessage("Not enough {0} to build {1} tank. Need {2}.", 
							BuildTanksFrom, Utils.formatVolume(volume), metal);
						metal_pump.Revert();
						metal_pump.Clear();
						return false;
					}
					TanksMass += metal_pump.Result*metal_pump.Resource.density;
				}
				else
				{
					if(metal_pump.PartialTransfer)
						ScreenMessager.showMessage("Not enough storage for {0}. The excess was disposed of.", 
							BuildTanksFrom);
					TanksMass += metal*metal_pump.Resource.density;
				}
				if(TanksMass < 0) TanksMass = 0;
			}
			return true;
		}

		float _add_tank_last_volume, _add_tank_metal;
		float add_tank(string tank_type, float volume)
		{
			if(!volume.Equals(_add_tank_last_volume))
				_add_tank_metal = metal_for_tank(volume);
			_add_tank_last_volume = volume;
			GUILayout.Label(Utils.formatUnits(_add_tank_metal), GUILayout.Width(50));
			var max = GUILayout.Button("Max");
			if(max || volume > Volume) volume = Volume;
			if(volume <= 0) GUILayout.Label("Add", Styles.grey);
			else if(GUILayout.Button("Add", Styles.green_button))
			{
				if(metal_pump != null && !convert_metal(volume)) return volume;
				tank_manager.AddTank(tank_type, volume); //liters
				change_size(-volume);
			}
			return volume;
		}

		void remove_tank(HangarSwitchableTank tank)
		{
			var volume = tank.Volume;
			if(!tank_manager.RemoveTank(tank)) return;
			if(metal_pump != null && !convert_metal(-volume)) return;
			change_size(volume);
		}
		#endregion

		#region GUI
		enum TankWindows { EditTanks } //maybe we'll need more in the future
		readonly Multiplexer<TankWindows> selected_window = new Multiplexer<TankWindows>();

		[KSPEvent(guiActive = true, guiName = "Edit Tanks", active = false)]
		public void EditTanks()
		{ 
			if(VesselsDocked > 0)
			{
				ScreenMessager.showMessage("There are some ships docked inside this hangar.\n" +
					"All works on resource tanks are prohibited for safety reasons.");
				selected_window[TankWindows.EditTanks] = false;
			}
			else selected_window.Toggle(TankWindows.EditTanks);
			if(selected_window[TankWindows.EditTanks]) 
				tank_manager.UnlockEditor(); 
		}

		public void OnGUI() 
		{ 
			if(Event.current.type != EventType.Layout) return;
			if(!selected_window.Any()) return;
			if(tank_manager == null) return;
			if(VesselsDocked > 0) 
			{ 
				selected_window[TankWindows.EditTanks] = false;
				tank_manager.UnlockEditor(); 
				return; 
			}
			Styles.Init();
			if(selected_window[TankWindows.EditTanks])
			{
				var title = string.Format("Available Volume: {0}", Utils.formatVolume(Volume));
				tank_manager.DrawTanksWindow(GetInstanceID(), title, add_tank, remove_tank);
				if(tank_manager.Closed) selected_window[TankWindows.EditTanks] = false;
			}
		}
		#endregion
	}
}

