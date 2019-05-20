//   HangarGenericInflatable.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
    public interface IControllableModule
    {
        bool CanEnable();
        bool CanDisable();
        void Enable(bool enable);
    }

    public class ControllableModuleBase : SerializableFiledsPartModule, IControllableModule
    {
        virtual public bool CanEnable() { return true; }
        virtual public bool CanDisable() { return true; }

        virtual public void Enable(bool enable)
        { enabled = isEnabled = enable; }
    }

    public class GasCompressor : ConfigNodeObject
    {
        [Persistent] public float ConversionRate = -1;
        [Persistent] public float ConsumptionRate = -1;
        public float OutputFraction { get; private set; }
        public bool Valid { get { return ConversionRate > 0 && ConsumptionRate >= 0; } }

        ResourcePump socket;
        Part part;

        public void Init(Part p)
        { part = p; socket = p.CreateSocket(); }

        public float CompressGas()
        {
            if(part.vessel == null || part.vessel.mainBody == null) return 0;
            if(!part.vessel.mainBody.atmosphere) return 0;
            var pressure = part.vessel.mainBody.GetPressure(part.vessel.altitude);
            if(pressure < 1e-6) return 0;
            pressure /= Math.Max(part.vessel.mainBody.atmospherePressureSeaLevel, 101.325);
            socket.RequestTransfer(ConsumptionRate * TimeWarp.fixedDeltaTime);
            if(!socket.TransferResource()) return 0;
            OutputFraction = socket.Ratio;
            return (float)(pressure * ConversionRate * socket.Result);
        }
    }

    public class HangarGenericInflatable : MultiGeometryAnimator
    {
        //configuration
        [KSPField(isPersistant = false)] public string ControlledModules;
        [KSPField(isPersistant = false)] public string AnimatedNodes;
        [KSPField(isPersistant = false)] public bool PackedByDefault = true;
        [KSPField(isPersistant = false)] public float InflatableVolume;
        [KSPField(isPersistant = true)] public float CompressedGas = -1f;
        [KSPField(isPersistant = false)] public bool Recompressable = false;
        [KSPField(isPersistant = false)] public string CompressorSound = "Hangar/Sounds/Compressor";
        [KSPField(isPersistant = false)] public string InflationSound = "Hangar/Sounds/Inflate";
        [KSPField(isPersistant = false)] public float SoundVolume = 0.2f;

        //GUI
        [KSPField(guiName = "Compressed Gas", guiActive = true)] public string CompressedGasDisplay;
        SimpleWarning warning;

        //modules and nodes
        readonly List<IControllableModule> controlled_modules = new List<IControllableModule>();
        readonly List<AnimatedNode> animated_nodes = new List<AnimatedNode>();

        //compressor
        [KSPField]
        [SerializeField]
        public GasCompressor Compressor = new GasCompressor();
        bool has_compressed_gas { get { return CompressedGas >= InflatableVolume; } }
        public FXGroup fxSndCompressor;
        bool play_compressor;

        //metric and scale
        Part prefab = null;
        Metric prefab_metric;
        Metric part_metric;
        protected float volume_scale = 1;

        //state
        const int skip_fixed_frames = 5;

        #region Info
        public override string GetInfo()
        {
            if(!Compressor.Valid) return "";
            string info = "Compressor:\n";
            info += string.Format("Pump Rate: {0}/sec\n",
                Utils.formatVolume(Compressor.ConversionRate * Compressor.ConsumptionRate));
            info += string.Format("Energy Consumption: {0}/sec\n", Compressor.ConsumptionRate);
            return info;
        }
        #endregion

        #region Startup
        protected override void onPause()
        {
            base.onPause();
            if(fxSndCompressor.audio == null) return;
            if(fxSndCompressor.audio.isPlaying) fxSndCompressor.audio.Pause();
        }

        protected override void onUnpause()
        {
            base.onUnpause();
            if(fxSndCompressor.audio == null) return;
            if(play_compressor) fxSndCompressor.audio.Play();
        }

        public override void OnAwake()
        {
            base.OnAwake();
            warning = gameObject.AddComponent<SimpleWarning>();
            warning.yesCallback = deflate;
            GameEvents.onGamePause.Add(onPause);
            GameEvents.onGameUnpause.Add(onUnpause);
            GameEvents.onEditorShipModified.Add(UpdateGUI);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            Destroy(warning);
            GameEvents.onGamePause.Remove(onPause);
            GameEvents.onGameUnpause.Remove(onUnpause);
            GameEvents.onEditorShipModified.Remove(UpdateGUI);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if(state == StartState.None) return;
            //init compressor
            if(Compressor.Valid)
            {
                Compressor.Init(part);
                if(CompressorSound != string.Empty)
                {
                    Utils.createFXSound(part, fxSndCompressor, CompressorSound, true);
                    fxSndCompressor.audio.volume = GameSettings.SHIP_VOLUME * SoundVolume;
                }
            }
            //get controlled modules
            if(!string.IsNullOrEmpty(ControlledModules))
            {
                foreach(string module_name in Utils.ParseLine(ControlledModules, Utils.Delimiters))
                {
                    if(module_name == "") continue;
                    if(!part.Modules.Contains(module_name))
                    {
                        this.Log("OnStart: {} does not contain {} module.", part.name, module_name);
                        continue;
                    }
                    var modules = new List<IControllableModule>();
                    foreach(PartModule pm in part.Modules)
                    {
                        if(pm.moduleName == module_name)
                        {
                            var controllableModule = pm as IControllableModule;
                            if(controllableModule != null)
                            {
                                modules.Add(controllableModule);
                                if(State != AnimatorState.Opened)
                                    controllableModule.Enable(false);
                            }
                            else this.Log("OnStart: {} is not a ControllableModule. Skipping it.", pm.moduleName);
                        }
                    }
                    controlled_modules.AddRange(modules);
                }
            }
            //get animated nodes
            foreach(string node_name in Utils.ParseLine(AnimatedNodes, Utils.Delimiters))
            {
                if(node_name == "") continue;
                Transform node_transform = part.FindModelTransform(node_name);
                if(node_transform == null)
                {
                    this.Log("OnStart: no transform '{}' in {}", node_name, part.name);
                    continue;
                }
                AttachNode node = part.FindAttachNode(node_name);
                if(node == null) node = part.srfAttachNode.id == node_name ? part.srfAttachNode : null;
                if(node == null)
                {
                    this.Log("OnStart: no node '{}' in {}", node_name, part.name);
                    continue;
                }
                var a_node = new AnimatedNode(node, node_transform, part);
                animated_nodes.Add(a_node);
            }
            //calculate prefab metric
            prefab = part.partInfo.partPrefab;
            prefab_metric = new Metric(prefab);
            //get compressed gas for the first time
            if(CompressedGas < 0 &&
               (state == StartState.Editor
                || vessel != null && vessel.staticPressurekPa > 1e-6))
                CompressedGas = InflatableVolume;
            //prevent accidental looping of animation
            Loop = false;
            //update part, GUI and set the flag
            UpdatePart();
            ToggleEvents();
            StartCoroutine(SlowUpdate());
            StartCoroutine(FirstTimeUpdateNodes());
            isEnabled = false;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if(!node.HasValue("State"))
                State = PackedByDefault ? AnimatorState.Closed : AnimatorState.Opened;
        }
        #endregion

        #region Updates
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if(State == AnimatorState.Opening ||
                State == AnimatorState.Closing)
            {
                UpdatePart();
                part.BreakConnectedCompoundParts();
            }
            if(Compressor.Valid && !has_compressed_gas)
            {
                CompressedGas += Compressor.CompressGas();
                if(has_compressed_gas)
                { play_compressor = false; ToggleEvents(); }
                else play_compressor = Compressor.OutputFraction > 0;
            }
        }

#if DEBUG
        public override void Update()
        { 
            base.Update();
            animated_nodes.ForEach(n => n.DrawAnchor());
        }
#endif

        protected virtual void UpdatePart()
        {
            animated_nodes.ForEach(n => n.UpdateNode());
            if(prefab_metric.Empty) return;
            part_metric = new Metric(part);
            volume_scale = part_metric.volume / prefab_metric.volume;
            part.crashTolerance = prefab.crashTolerance * Mathf.Pow(volume_scale, 0.333333333f);
        }

        IEnumerator<YieldInstruction> FirstTimeUpdateNodes()
        {
            yield return new WaitForFixedUpdate();
            for(int i = 0; i < skip_fixed_frames; i++)
            {
                yield return new WaitForFixedUpdate();
                UpdatePart();
            }
            if(State == AnimatorState.Opened) EnableModules(true);
            isEnabled = true;
        }

        IEnumerator<YieldInstruction> SlowUpdate()
        {
            while(true)
            {
                //update GUI
                CompressedGasDisplay = string.Format("{0:P1}", Mathf.Min(CompressedGas / InflatableVolume, 1));
                //update sounds
                if(fxSndCompressor.audio != null)
                {
                    if(play_compressor)
                    {
                        fxSndCompressor.audio.pitch = 0.5f + 0.5f * Compressor.OutputFraction;
                        if(!fxSndCompressor.audio.isPlaying)
                            fxSndCompressor.audio.Play();
                    }
                    else fxSndCompressor.audio.Stop();
                }
                //wait
                yield return new WaitForSeconds(0.5f);
            }
        }

        public void UpdateGUI(ShipConstruct ship) { UpdatePart(); }
        #endregion

        #region Modules Control
        bool CanEnableModules() { return controlled_modules.All(m => m.CanEnable()); }
        bool CanDisableModules() { return controlled_modules.All(m => m.CanDisable()); }

        void EnableModules(bool enable) { controlled_modules.ForEach(m => m.Enable(enable)); }

        IEnumerator<YieldInstruction> DelayedEnableModules(bool enable)
        {
            if(!enable) EnableModules(enable);
            AnimatorState target_state = enable ? AnimatorState.Opened : AnimatorState.Closed;
            while(State != target_state)
            {
                if(State == AnimatorState.Closing && Recompressable && !has_compressed_gas)
                    CompressedGas += Mathf.Abs(last_progress - progress) * InflatableVolume;
                yield return null;
            }
            if(State == AnimatorState.Closed && Recompressable)
                CompressedGas = InflatableVolume;
            UpdatePart();
            yield return new WaitForSeconds(0.5f);
            if(enable) EnableModules(enable);
        }
        #endregion

        #region Events and actions
        void ToggleEvents()
        {
            bool state = State == AnimatorState.Closed || State == AnimatorState.Closing;
            Events["Inflate"].active = state && has_compressed_gas;
            Events["Deflate"].active = !state;
        }

        [KSPEvent(guiActiveEditor = true, guiActive = true, guiName = "Inflate", active = true)]
        public void Inflate()
        {
            if(!has_compressed_gas) return;
            if(State != AnimatorState.Closed) return;
            if(!CanEnableModules()) return;
            if(HighLogic.LoadedSceneIsFlight) CompressedGas = 0;
            StartCoroutine(DelayedEnableModules(true));
            Open(); ToggleEvents();
        }

        [KSPEvent(guiActiveEditor = true, guiActive = true, guiName = "Deflate", active = false)]
        public void Deflate()
        {
            if(State != AnimatorState.Opened) return;
            if(!CanDisableModules()) return;
            if(warning.WindowEnabled) return;
            if(has_compressed_gas || Recompressable)
                deflate();
            else
            {
                if(!Compressor.Valid)
                    warning.Message = "This part is not equipped with a compressor. " +
                                      "You will not be able to inflate it again. " +
                                      "Are you sure you want to deflate the hangar?";
                else if(!part.vessel.mainBody.atmosphere)
                    warning.Message = "There's no atmosphere here. " +
                                      "You will not be able to inflate this part again." +
                                      "Are you sure you want to deflate it?";
                warning.Show(true);
            }
        }

        void deflate()
        {
            StartCoroutine(DelayedEnableModules(false));
            Close(); ToggleEvents();
        }

        [KSPAction("Inflate")]
        public void InflateAction(KSPActionParam param) { Inflate(); }
        #endregion
    }

    public class GenericInflatableUpdater : ModuleUpdater<HangarGenericInflatable>
    {
        protected override void on_rescale(ModulePair<HangarGenericInflatable> mp, Scale scale)
        {
            mp.module.InflatableVolume = mp.base_module.InflatableVolume * scale.absolute.volume;
            mp.module.CompressedGas *= scale.relative.volume;
            mp.module.ForwardSpeed = mp.base_module.ForwardSpeed / (scale.absolute * scale.aspect);
            mp.module.ReverseSpeed = mp.base_module.ReverseSpeed / (scale.absolute * scale.aspect);
            if(mp.module.Compressor.Valid)
                mp.module.Compressor.ConsumptionRate = mp.base_module.Compressor.ConsumptionRate * scale.absolute.volume;
        }
    }
}