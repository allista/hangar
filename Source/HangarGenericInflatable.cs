using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AtHangar
{
	public interface IControllableModule
	{
		bool CanEnable();
		bool CanDisable();
		void Enable(bool enable);
	}

	public class ModuleGUIState
	{
		readonly public List<string> EditorFields    = new List<string>();
		readonly public List<string> GUIFields       = new List<string>();
		readonly public List<string> InactiveEvents  = new List<string>();
		readonly public List<string> InactiveActions = new List<string>();
	}

	public static class PartModuleExtension
	{
		public static ModuleGUIState SaveGUIState(this PartModule pm)
		{
			ModuleGUIState state = new ModuleGUIState();
			foreach(BaseField f in pm.Fields)
			{
				if(f.guiActive) state.GUIFields.Add(f.name);
				if(f.guiActiveEditor) state.EditorFields.Add(f.name);
			}
			foreach(BaseEvent e in pm.Events)
				if(!e.active) state.InactiveEvents.Add(e.name);
			foreach(BaseAction a in pm.Actions)
				if(!a.active) state.InactiveActions.Add(a.name);
			return state;
		}

		public static ModuleGUIState DeactivateGUI(this PartModule pm)
		{
			ModuleGUIState state = new ModuleGUIState();
			foreach(BaseField f in pm.Fields)
			{
				if(f.guiActive) state.GUIFields.Add(f.name);
				if(f.guiActiveEditor) state.EditorFields.Add(f.name);
				f.guiActive = f.guiActiveEditor = false;
			}
			foreach(BaseEvent e in pm.Events)
			{
				if(!e.active) state.InactiveEvents.Add(e.name);
				e.active = false;
			}
			foreach(BaseAction a in pm.Actions)
			{
				if(!a.active) state.InactiveActions.Add(a.name);
				a.active = false;
			}
			return state;
		}

		public static void ActivateGUI(this PartModule pm, ModuleGUIState state = null)
		{
			foreach(BaseField f in pm.Fields)
			{
				if(state.GUIFields.Contains(f.name)) f.guiActive = true;
				if(state.EditorFields.Contains(f.name)) f.guiActiveEditor = true;
			}
			foreach(BaseEvent e in pm.Events)
			{
				if(state.InactiveEvents.Contains(e.name)) continue;
				e.active = true;
			}
			foreach(BaseAction a in pm.Actions)
			{
				if(state.InactiveActions.Contains(a.name)) continue;
				a.active = true;
			}
		}
	}

	public class HangarGenericInflatable : HangarAnimator
	{
		[KSPField(isPersistant = false)]
		public string ControlledModules;

		[KSPField(isPersistant = false)]
		public bool PackedByDefault;

		readonly List<IControllableModule> controlled_modules = new List<IControllableModule>();


		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			//get controlled modules
			foreach(string module_name in ControlledModules.Split(' '))
			{
				if(!part.Modules.Contains(module_name))
				{
					Utils.Log("HangarGenericInflatable.OnStart: {0} does not contain {1} module", part.name, module_name);
					continue;
				}
				List<IControllableModule> modules = new List<IControllableModule>();
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
						else Utils.Log("HangarGenericInflatable.OnStart: {0} is not a ControllableModule", pm.moduleName);
					}
				}
				controlled_modules.AddRange(modules);
			}
			//force attach roules for inflatable part. No surface attach!
			part.attachRules.allowSrfAttach = false;
			part.attachRules.srfAttach = false;
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			if(node.HasValue("SavedState")) return;
			State = PackedByDefault? AnimatorState.Closed : AnimatorState.Opened;
		}

		#region Modules Control
		bool CanEnableModules() { return controlled_modules.All(m => m.CanEnable()); }
		bool CanDisableModules() { return controlled_modules.All(m => m.CanDisable()); }

		void EnableModules(bool enable) { controlled_modules.ForEach(m => m.Enable(enable)); }

		IEnumerator<YieldInstruction> DelayedEnableModules(bool enable)
		{
			AnimatorState target_state = enable ? AnimatorState.Opened : AnimatorState.Closed;
			while(State != target_state) yield return new WaitForFixedUpdate();
			yield return new WaitForSeconds(0.5f);
			EnableModules(enable);
		}
		#endregion

		#region Events
		[KSPEvent (guiActiveEditor = true, guiActive = true, guiName = "Inflate", active = true)]
		public void Inflate() 
		{ 
			if(State != AnimatorState.Closed) return;
			if(!CanEnableModules()) return;
			Events["Inflate"].active = false;
			Events["Deflate"].active = true;
			StartCoroutine(DelayedEnableModules(true));
			Open();
		}

		[KSPEvent (guiActiveEditor = true, guiActive = true, guiName = "Deflate", active = false)]
		public void Deflate()	
		{ 
			if(State != AnimatorState.Opened) return;
			if(!CanDisableModules()) return;
			Events["Inflate"].active = true;
			Events["Deflate"].active = false;
			EnableModules(false);
			Close();
		}
		#endregion
	}
}

