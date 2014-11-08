using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class HangarStorageDynamic : HangarStorage
	{
		const float tank_area_per_volume = 6.73f; // m^2/m^3 (for aspect ratio 3:1:1)

		[KSPField(isPersistant = true)] public float TotalVolume;
		[KSPField(isPersistant = true)] public Vector3 StorageSize;
		[KSPField] public float WidthToLengthRatio = 0.5f;
		[KSPField] public float UpdateVolumeThreshold = 0.1f; //m^3
		float max_side;

		public ConfigNode ModuleSave;
		SwitchableTankManager tank_manager;

		protected override void early_setup(StartState state)
		{
			base.early_setup(state);
			Fields["hangar_v"].guiActive = true;
			Fields["hangar_d"].guiActive = true;
			//init tank manager
			tank_manager = new SwitchableTankManager(part);
			if(ModuleSave.HasNode(SwitchableTankManager.NODE_NAME))
				tank_manager.Load(ModuleSave.GetNode(SwitchableTankManager.NODE_NAME));
			max_side = Mathf.Pow(TotalVolume, 1f/3);
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			AddVolume(1000);//debug
		}

		protected override void update_metrics()
		{
			PartMetric = new Metric(part);
			HangarMetric = new Metric(StorageSize);
		}

		public void UpdateMetric()
		{
			HangarMetric = new Metric(StorageSize);
			hangar_v = Utils.formatVolume(HangarMetric.volume);
			hangar_d = Utils.formatDimensions(HangarMetric.size);
			_used_volume = Utils.formatPercent(UsedVolumeFrac);
		}

		public void AddVolume(float volume) 
		{
			if(volume < 0 || tank_manager.TanksCount > 0) return;
			TotalVolume += volume;
			if(TotalVolume-Volume > UpdateVolumeThreshold)
			{
				max_side = Mathf.Pow(TotalVolume, 1f/3);
				StorageSize = new Vector3(max_side,max_side,max_side);
				UpdateMetric();
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

		public void RescaleTanks(float relative_scale)
		{ if(tank_manager != null) tank_manager.RescaleTanks(relative_scale); }

		#region GUI
		enum TankWindows { EditTanks } //maybe we'll need more in the future
		readonly Multiplexer<TankWindows> selected_window = new Multiplexer<TankWindows>();

		//debug: for Release remove guiActiveEditor
		[KSPEvent (guiActiveEditor = true, guiName = "Edit Tanks", active = true)]
		public void EditTanks()
		{ 
			if(VesselsDocked > 0)
			{
				ScreenMessager.showMessage("There are some ships docked inside this hangar.\n" +
					"All works on resource tanks are prohibited for safety reasons.");
				selected_window[TankWindows.EditTanks] = false;
				return;
			}
			selected_window.Toggle(TankWindows.EditTanks);
		}

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
			UpdateMetric();
		}

		float add_tank(string tank_type, float volume)
		{
			var max = GUILayout.Button("Max");
			if(max || volume > Volume) volume = Volume;
			if(volume <= 0) GUILayout.Label("Add", Styles.grey);
			else if(GUILayout.Button("Add", Styles.green_button) &&
					tank_manager.AddTank(tank_type, volume))
				change_size(-volume);
			return volume;
		}

		void remove_tank(HangarSwitchableTank tank)
		{
			var volume = tank.Volume;
			if(!tank_manager.RemoveTank(tank)) return;
			change_size(volume);
		}

		public void OnGUI() 
		{ 
			if(Event.current.type != EventType.Layout) return;
			if(!selected_window.Any()) return;
			if(VesselsDocked > 0) return;
			if(tank_manager == null) return;
			Styles.Init();
			if(selected_window[TankWindows.EditTanks])
			{
				tank_manager.DrawTanksWindow(GetInstanceID(), add_tank, remove_tank);
				if(tank_manager.Close) selected_window.Toggle(TankWindows.EditTanks);
			}
		}
		#endregion
	}
}

