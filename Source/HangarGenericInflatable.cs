using System;
using System.Collections.Generic;
using System.Linq;
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
		class AnimatedNode
		{
			readonly AttachNode node;
			readonly Transform nT;
			readonly Transform pT;
			readonly Part part;

			public AnimatedNode(AttachNode node, Transform node_transform, Part part)
			{ 
				this.node     = node; 
				this.part     = part;
				nT            = node_transform; 
				pT            = part.partTransform; 
			}

			//move parts proportionally to the masses
			void UpdatePartsPos()
			{
				Part attached_part = node.attachedPart;
				if(!attached_part) return;
				AttachNode ap_node = attached_part.findAttachNodeByPart(part);
				if(ap_node == null) return;
				var dp =
					part.transform.TransformPoint(node.position) -
					attached_part.transform.TransformPoint(ap_node.position);
				Part root = part.RootPart();
				float total_mass = root.MassWithChildren();
				float this_mass, attached_mass;
				Utils.Log("dp: {0}", dp);
				if(attached_part == part.parent) 
				{
					this_mass = part.MassWithChildren();
					attached_mass = total_mass - this_mass;
					root.transform.position += dp*(this_mass/total_mass);
					part.transform.position -= dp;
					Utils.Log("moving root part: {0}, {1}", dp*(this_mass/total_mass), -dp);
				} 
				else 
				{
					attached_mass = attached_part.MassWithChildren();
					this_mass = total_mass - attached_mass;
					root.transform.position -= dp*(attached_mass/total_mass);
					attached_part.transform.position += dp;
					Utils.Log("moving attached part: {0}, {1}", dp, -dp*(attached_mass/total_mass));
				}
				Utils.Log("total mass {0}; this_mass {1}; attached_mass {2}", total_mass, this_mass, attached_mass); 
			}

			public void UpdateNode()
			{
				Utils.Log("0: pos {0}; ori {1}", node.position, node.orientation);
				node.position    = pT.InverseTransformPoint(nT.position);
				node.orientation = pT.InverseTransformDirection(nT.up);
				Utils.Log("1: pos {0}; ori {1}", node.position, node.orientation);
				UpdatePartsPos();
			}

			public void UpdateJoint()
			{
				Part attached_part = node.attachedPart;
				if(attached_part == null) return;
				Joint joint = part.GetComponents<Joint>().FirstOrDefault(j => j.connectedBody == attached_part.Rigidbody);
				bool update_anchor = true;
				if(joint == null) 
				{
					joint = attached_part.GetComponents<Joint>().FirstOrDefault(j => j.connectedBody == part.Rigidbody);
					update_anchor = false;
				}
				if(joint == null) return;
				if(update_anchor) joint.anchor = node.position;
				else joint.connectedAnchor = node.position;
			}
		}

		[KSPField(isPersistant = false)]
		public string ControlledModules;

		[KSPField(isPersistant = false)]
		public string AnimatedNodes;

		[KSPField(isPersistant = false)]
		public bool PackedByDefault = true;

		readonly List<IControllableModule> controlled_modules = new List<IControllableModule>();
		readonly List<AnimatedNode> animated_nodes = new List<AnimatedNode>();

		const int skip_fixed_frames = 5;
		ModuleGUIState gui_state;
		bool just_loaded = false;
		

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			if(state == StartState.None) return;
			//get controlled modules
			foreach(string module_name in ControlledModules.Split(' '))
			{
				if(!part.Modules.Contains(module_name))
				{
					Utils.Log("HangarGenericInflatable.OnStart: {0} does not contain {1} module.", part.name, module_name);
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
						else Utils.Log("HangarGenericInflatable.OnStart: {0} is not a ControllableModule. Skipping it.", pm.moduleName);
					}
				}
				controlled_modules.AddRange(modules);
			}
			//get animated nodes
			foreach(string node_name in AnimatedNodes.Split(' '))
			{
				Transform node_transform = part.FindModelTransform(node_name);
				if(node_transform == null) 
					Utils.Log("HangarGenericInflatable.OnStart: no transform '{0}' in {1}", node_name, part.name);
				AttachNode node = part.findAttachNode(node_name);
				if(node == null) node = part.srfAttachNode.id == node_name? part.srfAttachNode : null;
				if(node == null)
					Utils.Log("HangarGenericInflatable.OnStart: no node '{0}' in {1}", node_name, part.name);
				var a_node = new AnimatedNode(node, node_transform, part);
				animated_nodes.Add(a_node);
			}
			//forbid node attachment for the inflatable
			part.attachRules.allowSrfAttach = false;
			//update GUI and set the flag
			ToggleEvents();
			gui_state = this.DeactivateGUI();
			just_loaded = true;
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			if(!node.HasValue("SavedState"))
				State = PackedByDefault? AnimatorState.Closed : AnimatorState.Opened;
		}

		void UpdateNodes() 
		{ 
			animated_nodes.ForEach(n => n.UpdateNode()); 
			if(FlightGlobals.fetch != null && 
			   FlightGlobals.ActiveVessel != null)
				animated_nodes.ForEach(n => n.UpdateJoint()); 
		}

		IEnumerator<YieldInstruction> DelayedUpdateNodes()
		{
			for(int i = 0; i < skip_fixed_frames; i++)
			{
				yield return new WaitForFixedUpdate();
				UpdateNodes();
			}
		}

		public void FixedUpdate()
		{
			if(State == AnimatorState.Opening  || 
				State == AnimatorState.Closing ||
				just_loaded) 
			{ 
				if(just_loaded)
				{
					if(gui_state == null) 
						gui_state = this.SaveGUIState();
					this.ActivateGUI(gui_state);
					just_loaded = false;
					StartCoroutine(DelayedUpdateNodes());
				}
				else UpdateNodes();
			}
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
			UpdateNodes();
		}
		#endregion

		#region Events
		void ToggleEvents()
		{
			bool state = State == AnimatorState.Closed || State == AnimatorState.Closing;
			Events["Inflate"].active = state;
			Events["Deflate"].active = !state;
		}

		[KSPEvent (guiActiveEditor = true, guiActive = true, guiName = "Inflate", active = true)]
		public void Inflate() 
		{ 
			if(State != AnimatorState.Closed) return;
			if(!CanEnableModules()) return;
			StartCoroutine(DelayedEnableModules(true));
			Open(); ToggleEvents();
		}

		[KSPEvent (guiActiveEditor = true, guiActive = true, guiName = "Deflate", active = false)]
		public void Deflate()	
		{ 
			if(State != AnimatorState.Opened) return;
			if(!CanDisableModules()) return;
			EnableModules(false);
			Close(); ToggleEvents();
		}
		#endregion
	}
}