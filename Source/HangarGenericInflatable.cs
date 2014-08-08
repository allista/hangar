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

