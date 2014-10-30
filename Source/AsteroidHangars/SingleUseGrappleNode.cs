using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace AtHangar
{
	public class SingleUseGrappleNode : ModuleGrappleNode
	{
		[KSPField(isPersistant = true, guiName = "Fixed", guiActive = true)] 
		public bool Fixed;

		readonly SimpleDialog warning = new SimpleDialog();
		bool try_fix;

		[KSPField] public string AnimatorID = "_none_";
		BaseHangarAnimator animator;

		public override void OnStart(StartState st)
		{
			base.OnStart(st);
			//initialize Animator
			part.force_activate();
			animator = part.GetAnimator(AnimatorID);
			animator = part.Modules.OfType<BaseHangarAnimator>().FirstOrDefault(m => m.AnimatorID == AnimatorID);
			if(Fixed && animator as HangarAnimator == null) animator.Open();
			//initialize Fixed state
			StartCoroutine(delayed_disable_decoupling());
		}

		#region Fixing
		bool is_docked
		{ 
			get 
			{ 
				if(vessel == null) return false; 
				return vessel[dockedPartUId] != null;
			} 
		}

		void reinforce_grapple_joint()
		{
			var attached_part = vessel[dockedPartUId];
			if(attached_part == null) return;
			if(part.parent == attached_part && part.attachJoint != null) 
				part.attachJoint.SetUnbreakable(true);
			else if(attached_part.parent == part && attached_part.attachJoint != null) 
				attached_part.attachJoint.SetUnbreakable(true);
			else this.Log("Unable to find attachJoint when grappled. This should never heppen.");
		}

		void disable_decoupling()
		{
			LockPivot();
			reinforce_grapple_joint();
			Events["SetLoose"].active = false;
			Events["Decouple"].active = false;
			Actions["DecoupleAction"].active = false;
			Events["Release"].active = false;
			Events["ReleaseSameVessel"].active = false;
			Actions["ReleaseAction"].active = false;
			Fixed = true;
			disable_fixing();
		}

		void disable_fixing()
		{
			Events["FixHatch"].active = false;
			Actions["FixHatchAction"].active = false;
		}

		IEnumerator<YieldInstruction> delayed_disable_decoupling()
		{
			if(Fixed)
			{
				yield return new WaitForSeconds(1f);
				disable_decoupling();
				yield break;
			}
			if(animator.State != AnimatorState.Opening) 
			{ 
				this.Log("WARNING: trying to disable decoupling while not playing the animation."); 
				yield break; 
			}
			while(animator.State != AnimatorState.Opened) 
				yield return new WaitForSeconds(0.5f);
			disable_decoupling();
			ScreenMessager.showMessage("The grapple was fixed permanently");
		}
		#endregion

		#region Events & Actions
		#if DEBUG
		[KSPEvent (guiActive = true, guiName = "Try Fix Hatch", active = true)]
		public void TryFixHatch()
		{ animator.Toggle(); }
		#endif

		[KSPEvent (guiActive = true, guiName = "Fix Hatch Permanently", active = true)]
		public void FixHatch()
		{ 
			if(!is_docked) 
			{ ScreenMessager.showMessage("Nothing to fix to"); return; }
			if(animator.State != AnimatorState.Closed)
			{ ScreenMessager.showMessage("Already working..."); return; }
			try_fix = true;
		}

		[KSPAction("Fix Hatch Permanently")]
		public void FixHatchAction(KSPActionParam param) { FixHatch(); }
		#endregion

		public void OnGUI() 
		{ 
			if(Event.current.type != EventType.Layout) return;
			Styles.Init();
			while(try_fix)
			{
				warning.Show("This will fix the hatch permanently. " +
					"You will not be able to decouple it ever again. " +
					"Are you sure you want to continue?");
				if(warning.Result == SimpleDialog.Answer.None) break;
				if(warning.Result == SimpleDialog.Answer.Yes) 
				{
					animator.Open();
					StartCoroutine(delayed_disable_decoupling());
				}
				try_fix = false;
				break;
			}
		}
	}
}

