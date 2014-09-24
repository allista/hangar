using System;

namespace AtHangar
{
	public enum AnimatorState
    {
		Closed,
		Closing,
		Opened,
        Opening,
    }
		
	public class BaseHangarAnimator : PartModule
	{
		[KSPField(isPersistant = true)]  public string SavedState;
		[KSPField(isPersistant = false)] public string AnimatorID = "_none_";

		public float Duration { get; protected set; }
		
        public AnimatorState State 
		{
			get
            {
                try { return (AnimatorState)Enum.Parse(typeof(AnimatorState), SavedState); }
                catch
                {
                    State = AnimatorState.Closed;
                    return State;
                }
            }
            protected set { SavedState = Enum.GetName(typeof(AnimatorState), value); }
		}
		
		public override void OnStart(StartState state) { Duration = 0f; }
		
        virtual public void Open() { State = AnimatorState.Opened; }
        virtual public void Close() { State = AnimatorState.Closed; }
		
		public bool Toggle()
		{
			if(State == AnimatorState.Closed 
			   || State == AnimatorState.Closing)
			{
				Open ();
				return true;
			}
			Close ();
			return false;
		}
	}
}

