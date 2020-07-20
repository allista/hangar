//   HangarStorageDynamic.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using AT_Utils;
using CC.UI;

namespace AtHangar
{
    public class HangarStorageDynamic : HangarStorage, ITankManagerHost, ITankManagerCapabilities
    {
        [KSPField(isPersistant = true)] public float TotalVolume;
        [KSPField(isPersistant = true)] public Vector3 StorageSize;
        [KSPField(isPersistant = true)] private float TanksMass;
        [KSPField] public float WidthToLengthRatio = 0.5f;
        [KSPField] public float UpdateVolumeThreshold = 0.1f; //m^3
        [KSPField] public bool HasTankManager;
        [KSPField] public string BuildTanksFrom = "Metals";
        [KSPField] public float ResourcePerArea = 0.6f; // 200U/m^3, 1m^2*3mm

        [SerializeField] public ConfigNode ModuleSave;

        private SwitchableTankManager tank_manager;
        private ResourcePump metal_pump;
        private float max_side;

        public SwitchableTankManager GetTankManager() => tank_manager;

        #region IPart*Modifiers
        public override float GetModuleCost(float defaultCost, ModifierStagingSituation situation)
        {
            var cost = base.GetModuleCost(defaultCost, situation);
            if(metal_pump != null)
                cost += TanksMass / metal_pump.Resource.density * metal_pump.Resource.unitCost;
            return cost;
        }

        public override float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            var add_mass = tank_manager?.Tanks.Aggregate(0f,
                               (m, t) => m + metal_for_tank(t.TankType, t.Volume) * metal_pump.Resource.density)
                           ?? 0;
            return base.GetModuleMass(defaultMass, sit) + TanksMass - add_mass;
        }
        #endregion

        #region ITankManagerCapabilities
        public bool AddRemoveEnabled => VesselsCount == 0;
        public bool ConfirmRemove => !HighLogic.LoadedSceneIsEditor;
        public bool TypeChangeEnabled => HighLogic.LoadedSceneIsEditor;
        public bool VolumeChangeEnabled => HighLogic.LoadedSceneIsEditor;
        public bool FillEnabled => HighLogic.LoadedSceneIsEditor;
        public bool EmptyEnabled => HighLogic.LoadedSceneIsEditor;
        #endregion

        protected override void early_setup(StartState state)
        {
            SpawnSpaceSensor = false;
            base.early_setup(state);
            Fields[nameof(hangar_v)].guiActive = true;
            Fields[nameof(hangar_d)].guiActive = true;
            max_side = Mathf.Pow(TotalVolume, 1f / 3);
            //init tank manager
            if(!HasTankManager)
                return;
            tank_manager = new SwitchableTankManager(this);
            if(ModuleSave == null)
            {
                this.Log("ModuleSave is null. THIS SHOULD NEVER HAPPEN!");
                return;
            }
            var node = ModuleSave.GetNode(SwitchableTankManager.NODE_NAME)
                       ?? new ConfigNode(SwitchableTankManager.NODE_NAME);
            tank_manager.Load(node);
            tank_manager.Volume = TotalVolume;
            Events[nameof(EditTanks)].active = TotalVolume > 0;
            if(BuildTanksFrom != string.Empty)
            {
                metal_pump = new ResourcePump(part, BuildTanksFrom);
                if(!metal_pump.Valid)
                    metal_pump = null;
                else if(TanksMass <= 0)
                    TanksMass = tank_manager.Tanks
                        .Aggregate(0f,
                            (m, t) =>
                                m
                                + (metal_for_hull(t.Volume) + metal_for_tank(t.TankType, t.Volume))
                                * metal_pump.Resource.density);
            }
            tank_manager.onValidateNewTank += onValidateNewTank;
            tank_manager.onTankFailedToAdd += onTankFailedToAdd;
            tank_manager.onTankRemoved += onTankRemoved;
        }

        [SuppressMessage("ReSharper", "DelegateSubtraction")]
        public override void OnDestroy()
        {
            base.OnDestroy();
            if(tank_manager == null)
                return;
            tank_manager.onValidateNewTank -= onValidateNewTank;
            tank_manager.onTankFailedToAdd -= onTankFailedToAdd;
            tank_manager.onTankRemoved -= onTankRemoved;
            tank_manager.UI?.Close();
        }

        protected override void update_metrics()
        {
            PartMetric = new Metric(part);
            SpawnManager.SpaceMetric = new Metric(StorageSize);
        }

        public bool AddVolume(float volume)
        {
            if(volume < 0 || tank_manager == null || tank_manager.TanksCount > 0)
                return false;
            TotalVolume += volume;
            tank_manager.Volume = TotalVolume;
            Events[nameof(EditTanks)].active = TotalVolume > 0;
            if(!(TotalVolume - Volume > UpdateVolumeThreshold))
                return true;
            max_side = Mathf.Pow(TotalVolume, 1f / 3);
            StorageSize = new Vector3(max_side, max_side, max_side);
            Setup();
            return true;
        }

        public bool CanAddVolume => VesselsCount == 0 && tank_manager != null && tank_manager.TanksCount == 0;

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            tank_manager?.SaveInto(node);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            ModuleSave = node;
        }

        public override void OnBeforeSerialize()
        {
            if(tank_manager != null)
            {
                var node = new ConfigNode();
                Save(node);
                ModuleSave = node;
            }
            base.OnBeforeSerialize();
        }

        #region Tanks
        private void change_size(float volume)
        {
            var V = Mathf.Clamp(Volume + volume, 0, TotalVolume);
            if(V.Equals(0))
                StorageSize = Vector3.zero;
            else
            {
                var a = Mathf.Pow(WidthToLengthRatio * V, 1f / 3);
                var b = V / (a * a);
                if(volume < 0 && b > StorageSize.y)
                {
                    b = StorageSize.y;
                    a = Mathf.Sqrt(V / b);
                }
                else if(volume > 0 && b > max_side)
                {
                    b = max_side;
                    a = Mathf.Sqrt(V / b);
                }
                StorageSize = new Vector3(a, b, a);
            }
            Setup();
        }

        //area is calculated for a box with sides [a, a, 2a], where a*a*2a = volume
        private float metal_for_hull(float volume) =>
            Mathf.Sign(volume) * 10 * Mathf.Pow(Mathf.Abs(volume) / 2, 2f / 3) * ResourcePerArea;

        private float metal_for_tank(string tank_name, float volume)
        {
            var type = SwitchableTankType.GetTankType(tank_name);
            return type != null ? type.AddMass(volume) / metal_pump.Resource.density : 0;
        }

        private float metal_for_tank_and_hull(string tank_name, float volume) =>
            metal_for_hull(volume) + metal_for_tank(tank_name, volume);

        private bool convert_metal(float metal)
        {
            metal_pump.RequestTransfer(metal);
            if(!metal_pump.TransferResource())
                return true;
            if(metal > 0)
            {
                if(metal_pump.PartialTransfer)
                {
                    metal_pump.Revert();
                    metal_pump.Clear();
                    return false;
                }
                TanksMass += metal_pump.Result * metal_pump.Resource.density;
            }
            else
            {
                if(metal_pump.PartialTransfer && metal_pump.Ratio < 0.999f)
                    Utils.Message("Not enough storage for {0}. The excess was disposed of.", BuildTanksFrom);
                TanksMass += metal * metal_pump.Resource.density;
            }
            if(TanksMass < 0)
                TanksMass = 0;
            return true;
        }

        private string onValidateNewTank(string tankType, float volume)
        {
            if(VesselsCount > 0)
                return "There are some ships docked inside this hangar.\n"
                       + "All works on resource tanks are prohibited for safety reasons.";
            if(metal_pump == null)
                return null;
            var neededMetal = metal_for_tank_and_hull(tankType, volume);
            if(!convert_metal(neededMetal))
                return
                    $"Not enough {BuildTanksFrom} to build {Utils.formatVolume(volume)} tank.\nNeed {Utils.formatBigValue(neededMetal, "u")}.";
            change_size(-volume);
            return null;
        }

        private void onTankFailedToAdd(string tankType, float volume)
        {
            if(metal_pump != null)
                convert_metal(-metal_for_tank_and_hull(tankType, volume));
            change_size(volume);
        }

        private void onTankRemoved(ModuleSwitchableTank tank)
        {
            if(metal_pump != null)
                convert_metal(-metal_for_tank_and_hull(tank.TankType, tank.Volume));
            change_size(tank.Volume);
        }
        #endregion

        #region GUI
        [KSPEvent(guiActive = true, guiName = "Edit Tanks", active = false)]
        public void EditTanks()
        {
            tank_manager.UI.Toggle(this);
        }

        private void LateUpdate()
        {
            if(tank_manager == null || !tank_manager.UI.IsShown)
                return;
            tank_manager.UI.OnLateUpdate();
        }
        #endregion
    }
}
