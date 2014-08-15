using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class HangarAnimator : BaseHangarAnimator
	{
		//fields
		[KSPField(isPersistant = false)]
        public string AnimationName;

		[KSPField(isPersistant = false)]
		public float ForwardSpeed = 1f;

		[KSPField(isPersistant = false)]
		public float ReverseSpeed = 1f;
		
		//animation
		List<AnimationState> animation_states = new List<AnimationState>();
		
		//from Kethane / Plugin / Misc.cs
		void setup_animation()
		{
			Animation[] animations = part.FindModelAnimators(AnimationName);
			if(animations == null)
			{
				Utils.Log("HangarAnimator.setup_animation: there's no '{0}' animation in {1}", 
						  AnimationName, part.name);
				return;
			}
			animation_states = new List<AnimationState>();
			foreach(Animation anim in animations)
			{
				AnimationState animationState = anim[AnimationName];
				animationState.speed = 0;
				animationState.enabled = true;
				animationState.wrapMode = WrapMode.ClampForever;
				anim.Blend(AnimationName);
				animation_states.Add(animationState);
			}
		}

        public override void OnStart(StartState state)
        {
			base.OnStart(state);
			setup_animation();
			if(State == AnimatorState.Opened) seek(1);
        }

		protected void seek(float norm_time = 0f)
		{
			foreach(var state in animation_states)
				state.normalizedTime = Mathf.Clamp01(norm_time);
		}

        override public void Open()
        {
            if(State != AnimatorState.Closed) return;
            State = AnimatorState.Opening;
        }

        override public void Close()
        {
            if(State != AnimatorState.Opened) return;
            State = AnimatorState.Closing;
        }

		public virtual void Update()
        {
            if (State == AnimatorState.Opening && animation_states.TrueForAll(s => s.normalizedTime >= 1))
                State = AnimatorState.Opened;
            else if (State == AnimatorState.Closing && animation_states.TrueForAll(s => s.normalizedTime <= 0))
            	State = AnimatorState.Closed;
   
			foreach (var state in animation_states)
            {
                var time = Mathf.Clamp01(state.normalizedTime);
                state.normalizedTime = time;
				var speed = (State == AnimatorState.Opening || State == AnimatorState.Opened) ? ForwardSpeed : -ReverseSpeed;
				if(HighLogic.LoadedSceneIsEditor) speed *= 1 - 10 * (time - 1) * time;
				state.speed = speed;
            }
        }
	}
}

