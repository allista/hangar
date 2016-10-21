//   HangarStorageDynamic.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Linq;
using UnityEngine;
using AT_Utils;

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
		[KSPField] public string BuildTanksFrom  = "Metals";
		[KSPField] public float  ResourcePerArea = 0.6f; // 200U/m^3, 1m^2*3mm

		public ConfigNode ModuleSave;
		SwitchableTankManager tank_manager;
		ResourcePump metal_pump;
		float max_side;

		public SwitchableTankManager GetTankManager() { return tank_manager; }

		#region IPart*Modifiers
		public override float GetModuleCost(float defaultCost, ModifierStagingSituation situation)
		{
			var cost = base.GetModuleCost(defaultCost, situation);
			if(metal_pump != null)
				cost += TanksMass/metal_pump.Resource.density*metal_pump.Resource.unitCost;
			return cost;
		}

		public override float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
		{ 
			var add_mass = tank_manager == null? 0 :
				tank_manager.Tanks.Aggregate(0f, (m, t) => m+metal_for_tank(t.TankType, t.Volume)*metal_pump.Resource.density);
			return base.GetModuleMass(defaultMass, sit) + TanksMass - add_mass; 
		}
		#endregion

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
						TanksMass = tank_manager.Tanks
							.Aggregate(0f, (m, t) => 
							           m+(metal_for_hull(t.Volume)+metal_for_tank(t.TankType, t.Volume))*
							           metal_pump.Resource.density);
				}
			}
		}

		protected override void update_metrics()
		{
			PartMetric = new Metric(part);
			HangarMetric = new Metric(StorageSize);
		}

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
				return TotalVesselsDocked == 0 && 
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
		float metal_for_hull(float volume)
		{ return Mathf.Sign(volume)*10*Mathf.Pow(Mathf.Abs(volume)/2, 2f/3)*ResourcePerArea; }

		float metal_for_tank(string tank_name, float volume)
		{ 
			var type = SwitchableTankType.GetTankType(tank_name);
			return type != null? type.AddMass(volume) / metal_pump.Resource.density : 0;
		}

		bool convert_metal(float metal)
		{
			metal_pump.RequestTransfer(metal);
			if(metal_pump.TransferResource())
			{
				if(metal > 0)
				{
					if(metal_pump.PartialTransfer) 
					{
						metal_pump.Revert();
						metal_pump.Clear();
						return false;
					}
					TanksMass += metal_pump.Result*metal_pump.Resource.density;
				}
				else
				{
					if(metal_pump.PartialTransfer)
						Utils.Message("Not enough storage for {0}. The excess was disposed of.", BuildTanksFrom);
					TanksMass += metal*metal_pump.Resource.density;
				}
				if(TanksMass < 0) TanksMass = 0;
			}
			return true;
		}

		float _add_tank_last_volume, _add_tank_metal;
		float add_tank(string tank_name, float volume, bool percent)
		{
			if(percent) volume = Volume*volume/100;
			if(metal_pump != null)
			{
				if(!volume.Equals(_add_tank_last_volume))
					_add_tank_metal = metal_for_hull(volume) + metal_for_tank(tank_name, volume);
				GUILayout.Label(Utils.formatUnits(_add_tank_metal), GUILayout.Width(50));
			}
			_add_tank_last_volume = volume;
			var max = GUILayout.Button("Max");
			if(max || volume > Volume) volume = Volume;
			if(volume <= 0) GUILayout.Label("Add", Styles.grey);
			else if(GUILayout.Button("Add", Styles.green_button))
			{
				if(metal_pump == null || convert_metal(_add_tank_metal))
				{
					change_size(-volume);
					tank_manager.AddVolume(tank_name, volume); //liters
				}
				else if(metal_pump != null)
					Utils.Message("Not enough {0} to build {1} tank. Need {2}.", 
					              BuildTanksFrom, Utils.formatVolume(volume), _add_tank_metal);
			}
			return percent? (Volume.Equals(0)? 0 : volume/Volume*100) : volume;
		}

		void remove_tank(ModuleSwitchableTank tank)
		{
			var volume = tank.Volume;
			if(!tank_manager.RemoveTank(tank)) return;
			if(metal_pump != null && !convert_metal(-metal_for_hull(volume)-metal_for_tank(tank.TankType, volume))) return;
			change_size(volume);
		}
		#endregion

		#region GUI
		enum TankWindows { None, EditTanks } //maybe we'll need more in the future
		readonly Multiplexer<TankWindows> selected_window = new Multiplexer<TankWindows>();

		[KSPEvent(guiActive = true, guiName = "Edit Tanks", active = false)]
		public void EditTanks()
		{ 
			if(TotalVesselsDocked > 0)
			{
				Utils.Message("There are some ships docked inside this hangar.\n" +
				              "All works on resource tanks are prohibited for safety reasons.");
				selected_window[TankWindows.EditTanks] = false;
			}
			else selected_window.Toggle(TankWindows.EditTanks);
			if(selected_window[TankWindows.EditTanks]) 
				tank_manager.UnlockEditor(); 
		}

		public void OnGUI() 
		{ 
			if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
			if(!selected_window) return;
			if(tank_manager == null) return;
			if(TotalVesselsDocked > 0) 
			{ 
				selected_window[TankWindows.EditTanks] = false;
				tank_manager.UnlockEditor(); 
				return; 
			}
			Styles.Init();
			if(selected_window[TankWindows.EditTanks])
			{
				var title = string.Format("Available Volume: {0}", Utils.formatVolume(Volume));
				tank_manager.DrawTanksManagerWindow(GetInstanceID(), title, add_tank, remove_tank);
				if(tank_manager.Closed) selected_window[TankWindows.EditTanks] = false;
			}
		}
		#endregion
	}
}

