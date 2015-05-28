using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSPAPIExtensions;

namespace AtHangar
{
	public class HangarAnimator : BaseHangarAnimator
	{
		//fields
		[KSPField(isPersistant = false)] public string OpenEventGUIName;
		[KSPField(isPersistant = false)] public string CloseEventGUIName;
		[KSPField(isPersistant = false)] public string ActionGUIName;
		[KSPField(isPersistant = false)] public string StopTimeGUIName;

		[KSPField(isPersistant = false)] public string AnimationNames;
		[KSPField(isPersistant = false)] public float  ForwardSpeed = 1f;
		[KSPField(isPersistant = false)] public float  ReverseSpeed = 1f;
		[KSPField(isPersistant = false)] public bool   Loop;
		[KSPField(isPersistant = false)] public bool   Reverse;
		[KSPField(isPersistant = false)] public float  EnergyConsumption = 0f;
		[KSPField(isPersistant = false)] public float  DragMultiplier = 1f;
		[KSPField(isPersistant = true)]  public float  progress = 0f;

		[KSPField(isPersistant=true, guiActiveEditor=false, guiActive = false, guiName="Stop At", guiFormat="P1")]
		[UI_FloatEdit(scene=UI_Scene.All, minValue=0f, maxValue=1f, incrementLarge=0.1f, incrementSmall=0.01f, incrementSlide=0.001f)]
		public float StopTime = 1.0f;

		protected ResourcePump socket;
		protected float last_progress    = 0f;
		protected float speed_multiplier = 1f;
		
		//animation
		List<AnimationState> animation_states = new List<AnimationState>();

		void setup_animation()
		{
			foreach(var aname in AnimationNames.Split(new []{' '}, StringSplitOptions.RemoveEmptyEntries))
			{
				Animation[] animations = part.FindModelAnimators(aname);
				if(animations == null || animations.Length == 0)
				{
					this.Log("setup_animation: there's no '{0}' animation in {1}", 
							  aname, part.name);
					continue;
				}
				foreach(Animation anim in animations)
				{
					if(anim[aname] == null) continue;
					AnimationState animationState = anim[aname];
					animationState.speed = 0;
					animationState.enabled = true;
					animationState.wrapMode = WrapMode.ClampForever;
					anim.Blend(aname);
					animation_states.Add(animationState);
				}
			}
			Duration = animation_states.Aggregate(0f, (d, s) => Math.Max(d, s.length));
		}

        public override void OnStart(StartState state)
        {
			base.OnStart(state);
			if(State == AnimatorState.Opened) progress = 1f;
			setup_animation();
			seek(progress);
			if(EnergyConsumption > 0) 
				socket = part.CreateSocket();
			//GUI
			Events["OpenEvent"].guiName     = OpenEventGUIName;
			Events["CloseEvent"].guiName    = CloseEventGUIName;
			Actions["ToggleAction"].guiName = ActionGUIName;
			Fields["StopTime"].guiName      = StopTimeGUIName;
			update_events();
        }

		protected void seek(float _progress = 0f)
		{
			var norm_time = Mathf.Clamp01(_progress*StopTime);
			if(Reverse) norm_time = 1-norm_time;
			animation_states.ForEach(s => s.normalizedTime = norm_time);
			progress = _progress;
		}

        override public void Open()
		{ State = AnimatorState.Opening; }

        override public void Close()
		{ State = AnimatorState.Closing; }

		public virtual void Update()
        {
			if(!Playing) return;
			//calculate animation speed
			float speed = (State == AnimatorState.Opening || State == AnimatorState.Opened)? 
				ForwardSpeed : -ReverseSpeed;
			if(Reverse) speed *= -1;
			if(HighLogic.LoadedSceneIsEditor) 
				speed *= 1 - 10 * (progress - 1) * progress;
			else speed *= speed_multiplier;
			//set animation speed, compute total progress
			float _progress = 1;
			foreach(var state in animation_states)
            {
				float time = Mathf.Clamp01(state.normalizedTime);
                state.normalizedTime = time;
				_progress = Math.Min(_progress, time);
				state.speed = speed;
            }
			last_progress = progress;
			if(Reverse) _progress = 1-_progress;
			progress = Mathf.Clamp01(_progress/StopTime);
			//check progress
			if(State == AnimatorState.Opening && progress >= 1)
			{ if(Loop) seek(0); else State = AnimatorState.Opened; }
			else if(State == AnimatorState.Closing && progress <= 0) 
				State = AnimatorState.Closed;
			//stop the animation if not playing anymore
			if(!Playing) animation_states.ForEach(s => s.speed = 0);
        }

		protected virtual void consume_energy()
		{
			if(State != AnimatorState.Closing && State != AnimatorState.Opening) return;
			socket.RequestTransfer(EnergyConsumption*TimeWarp.fixedDeltaTime);
			if(!socket.TransferResource()) return;
			speed_multiplier = socket.Ratio;
			if(speed_multiplier < 0.01f) 
				speed_multiplier = 0;
		}

		public virtual void FixedUpdate()
		{
			//consume energy if playing
			if(HighLogic.LoadedSceneIsFlight && socket != null)	consume_energy();
			//change Drag according to the animation progress
			if(DragMultiplier != 1 && last_progress != progress)
			{
				float mult = 1 + (DragMultiplier-1)*progress;
				part.maximum_drag = part.partInfo.partPrefab.maximum_drag * mult;
				part.minimum_drag = part.partInfo.partPrefab.minimum_drag * mult;
				part.angularDrag  = part.partInfo.partPrefab.angularDrag  * mult;
			}
		}

		#region Events & Actions
		protected void update_events()
		{
			switch(State)
			{
			case AnimatorState.Closed:
			case AnimatorState.Closing:
				Events["OpenEvent"].active = OpenEventGUIName != string.Empty;
				Events["CloseEvent"].active = false;
				break;
			case AnimatorState.Opened:
			case AnimatorState.Opening:
				Events["OpenEvent"].active = false;
				Events["CloseEvent"].active = CloseEventGUIName != string.Empty;
				break;
			}
			Actions["ToggleAction"].active = ActionGUIName != string.Empty;
			HangarGUI.EnableField(Fields["StopTime"], StopTimeGUIName != string.Empty);
		}

		[KSPEvent (guiActiveEditor = true, guiActive = true, guiName = "Open", active = false)]
		public void OpenEvent() 
		{ 
			Open(); 
			update_events();
		}

		[KSPEvent (guiActiveEditor = true, guiActive = true, guiName = "Close", active = false)]
		public void CloseEvent()
		{ 
			Close(); 
			update_events();
		}

		[KSPAction("Toggle")]
		public void ToggleAction(KSPActionParam param) 
		{ 
			Toggle();
			update_events();
		}
		#endregion
	}
}

