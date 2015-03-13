using System;
using UnityEngine;

namespace AtHangar
{
	public class HangarTankManager : PartModule, ITankManager, ISerializationCallbackReceiver
	{
		#region Config
		/// <summary>
		/// The total maximum volume of a tank.
		/// </summary>
		[KSPField] public float Volume;

		/// <summary>
		/// If true, tanks may be added and removed.
		/// </summary>
		[KSPField] public bool AddRemoveEnabled = true;

		/// <summary>
		/// If true, type of tanks may be changed.
		/// </summary>
		[KSPField] public bool TypeChangeEnabled = true;

		public ConfigNode ModuleSave;
		#endregion

		#region Tanks
		SwitchableTankManager tank_manager;
		public SwitchableTankManager GetTankManager() { return tank_manager; }

		public override string GetInfo()
		{ 
			var info = string.Format("Max. Volume: {0}\n", Utils.formatVolume(Volume)); 
			if(TypeChangeEnabled) info += SwitchableTankType.TypesInfo;
			if(ModuleSave.HasNode(SwitchableTankManager.TANK_NODE))
			{
				info += "Preconfigured Tanks:\n";
				ModuleSave.GetNodes(SwitchableTankManager.TANK_NODE)
					.ForEach(n => info += SwitchableTankInfo.Info(n));
			}
			return info;
		}

		void init_tank_manager()
		{
			if(tank_manager != null) return;
			tank_manager = new SwitchableTankManager(this);
			tank_manager.EnablePartControls = !HighLogic.LoadedSceneIsEditor;
			tank_manager.Load(ModuleSave);
			var used_volume = tank_manager.TotalVolume;
			if(used_volume > Volume) 
			{
				this.Log("WARNING: Volume limit is less than the total volume " +
					"of preconfigured tanks: {0} < {1}", Volume, used_volume);
				Volume = used_volume;
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			ModuleSave = node;
			if(HighLogic.LoadedSceneIsEditor || 
			   HighLogic.LoadedSceneIsFlight) 
				init_tank_manager();
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			init_tank_manager();
		}

		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
			if(tank_manager != null)
				tank_manager.Save(node);
		}

		public void RescaleTanks(float relative_scale)
		{ if(tank_manager != null) tank_manager.RescaleTanks(relative_scale); }

		public void OnBeforeSerialize()
		{
			if(tank_manager == null) return;
			ModuleSave = new ConfigNode();
			Save(ModuleSave);
		}
		public void OnAfterDeserialize() {}
		#endregion

		#region GUI
		enum TankWindows { EditTanks } //maybe we'll need more in the future
		readonly Multiplexer<TankWindows> selected_window = new Multiplexer<TankWindows>();

		[KSPEvent (guiActiveEditor = true, guiName = "Edit Tanks", active = true)]
		public void EditTanks()
		{ 
			selected_window.Toggle(TankWindows.EditTanks);
			if(selected_window[TankWindows.EditTanks]) 
				tank_manager.UnlockEditor(); 
		}

		float add_tank(string tank_type, float volume)
		{
			var max  = GUILayout.Button("Max");
			var half = GUILayout.Button("1/2");
			var max_volume = (Volume - tank_manager.TotalVolume);
			if(max || volume > max_volume) volume = max_volume;
			else if(half) volume = max_volume/2;
			if(volume <= 0) GUILayout.Label("Add", Styles.grey);
			else if(GUILayout.Button("Add", Styles.green_button))
				tank_manager.AddTank(tank_type, volume);
			return volume;
		}
		void remove_tank(HangarSwitchableTank tank) 
		{ tank_manager.RemoveTank(tank); }

		public void OnGUI() 
		{ 
			if(Event.current.type != EventType.Layout) return;
			if(!HighLogic.LoadedSceneIsEditor) return;
			if(!selected_window.Any()) return;
			if(tank_manager == null) return;
			Styles.Init();
			if(selected_window[TankWindows.EditTanks])
			{
				var title = string.Format("Available Volume: {0} of {1}", 
				                          Utils.formatVolume(Volume - tank_manager.TotalVolume), 
				                          Utils.formatVolume(Volume));
				tank_manager.DrawTanksWindow(GetInstanceID(), title, add_tank, remove_tank);
				if(tank_manager.Closed) selected_window[TankWindows.EditTanks] = false;
			}
		}
		#endregion
	}
}



