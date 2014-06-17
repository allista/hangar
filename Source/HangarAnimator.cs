using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace AtHangar
{
	public class HangarAnimator : BaseHangarAnimator
	{
		//fields
		[KSPField(isPersistant = false)]
        public string OpenAnimation;
		
		//animation
		private List<AnimationState> openStates;
		
		//from Kethane / Plugin / Misc.cs
		public List<AnimationState> SetUpAnimation(string animationName)
        {
            List<AnimationState> states = new List<AnimationState>();
            foreach (Animation animation in part.FindModelAnimators(animationName))
            {
                AnimationState animationState = animation[animationName];
                animationState.speed = 0;
                animationState.enabled = true;
                animationState.wrapMode = WrapMode.ClampForever;
                animation.Blend(animationName);
                states.Add(animationState);
            }
            return states;
        }
		

        public override void OnStart(StartState state)
        {
			base.OnStart(state);
            openStates = SetUpAnimation(OpenAnimation);
            if (GatesState == HangarGates.Opened)
            {
                foreach(var openState in openStates)
					openState.normalizedTime = 1;
            }
        }

        override public void Open()
        {
            if(GatesState != HangarGates.Closed) { return; }
            GatesState = HangarGates.Opening;
        }

        override public void Close()
        {
            if (GatesState != HangarGates.Opened) { return; }
            GatesState = HangarGates.Closing;
        }

        public void Update()
        {
            if (GatesState == HangarGates.Opening && openStates.TrueForAll(s => s.normalizedTime >= 1))
                GatesState = HangarGates.Opened;
            else if (GatesState == HangarGates.Closing && openStates.TrueForAll(s => s.normalizedTime <= 0))
            	GatesState = HangarGates.Closed;
   
			foreach (var state in openStates)
            {
                var time = Mathf.Clamp01(state.normalizedTime);
                state.normalizedTime = time;
                var speed = HighLogic.LoadedSceneIsEditor ? 1 - 10 * (time - 1) * time : 1;
                state.speed = (GatesState == HangarGates.Opening || GatesState == HangarGates.Opened) ? speed : -speed;
            }
        }
	}
}

