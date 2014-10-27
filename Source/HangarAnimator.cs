using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AtHangar
{
	public class HangarAnimator : BaseHangarAnimator
	{
		//fields
		[KSPField(isPersistant = false)] public string AnimationName;
		[KSPField(isPersistant = false)] public float  ForwardSpeed = 1f;
		[KSPField(isPersistant = false)] public float  ReverseSpeed = 1f;
		[KSPField(isPersistant = false)] public bool   Loop = false;
		[KSPField(isPersistant = false)] public float  EnergyConsumption = 0f;
		[KSPField(isPersistant = false)] public float  DragMultiplier = 1f;
		[KSPField(isPersistant = true)]  public float  progress = 0f;

		float last_progress    = 0f;
		float speed_multiplier = 1f;
		
		//animation
		List<AnimationState> animation_states = new List<AnimationState>();

		//from Kethane / Plugin / Misc.cs
		void setup_animation()
		{
			Animation[] animations = part.FindModelAnimators(AnimationName);
			if(animations == null || animations.Length == 0)
			{
				this.Log("setup_animation: there's no '{0}' animation in {1}", 
						  AnimationName, part.name);
				return;
			}
			animation_states = new List<AnimationState>();
			foreach(Animation anim in animations)
			{
				if(anim[AnimationName] == null) continue;
				AnimationState animationState = anim[AnimationName];
				animationState.speed = 0;
				animationState.enabled = true;
				animationState.wrapMode = WrapMode.ClampForever;
				anim.Blend(AnimationName);
				animation_states.Add(animationState);
			}
			Duration = animation_states.Aggregate(0f, (d, s) => Math.Max(d, s.length));
		}

        public override void OnStart(StartState state)
        {
			base.OnStart(state);
			if(State == AnimatorState.Opened) progress = 1f;
			setup_animation();
			seek(progress);
        }

		protected void seek(float norm_time = 0f)
		{
			norm_time = Mathf.Clamp01(norm_time);
			foreach(var state in animation_states)
				state.normalizedTime = norm_time;
		}

        override public void Open()
		{ State = AnimatorState.Opening; }

        override public void Close()
		{ State = AnimatorState.Closing; }

		public virtual void Update()
        {
			if(State == AnimatorState.Opening && progress >= 1)
			{ if(Loop) seek(0); else State = AnimatorState.Opened; }
			else if(State == AnimatorState.Closing && progress <= 0) 
				State = AnimatorState.Closed;
   
			float _progress = 1;
			foreach(var state in animation_states)
            {
				float time = Mathf.Clamp01(state.normalizedTime);
                state.normalizedTime = time;
				_progress = Math.Min(_progress, time);
				float speed = (State == AnimatorState.Opening || State == AnimatorState.Opened) ? ForwardSpeed : -ReverseSpeed;
				if(HighLogic.LoadedSceneIsEditor) speed *= 1 - 10 * (time - 1) * time;
				else speed *= speed_multiplier;
				state.speed = speed;
            }
			last_progress = progress;
			progress = _progress;
        }

		public virtual void FixedUpdate()
		{
			//consume energy if doors are mooving
			if(HighLogic.LoadedSceneIsFlight)
			{
				if(EnergyConsumption > 0 && 
					(State == AnimatorState.Closing || State == AnimatorState.Opening))
				{
					float request = EnergyConsumption*TimeWarp.fixedDeltaTime;
					float consumed = part.RequestResource("ElectricCharge", request);
					speed_multiplier = consumed/request;
					if(speed_multiplier < 0.01f) speed_multiplier = 0f;
				}
			}
			//change Drag according to the animation progress
			if(DragMultiplier != 1 && last_progress != progress)
			{
				float mult = 1 + (DragMultiplier-1)*progress;
				part.maximum_drag = part.partInfo.partPrefab.maximum_drag * mult;
				part.minimum_drag = part.partInfo.partPrefab.minimum_drag * mult;
				part.angularDrag  = part.partInfo.partPrefab.angularDrag  * mult;
			}
		}
	}
}

