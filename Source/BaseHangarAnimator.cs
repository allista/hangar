using System;

namespace AtHangar
{
	public enum AnimatorState
    {
        Opened,
        Opening,
        Closed,
        Closing,
    }
		
	public class BaseHangarAnimator : PartModule
	{
		[KSPField(isPersistant = true)]
        public string SavedState;

		[KSPField(isPersistant = false)]
		public string AnimatorID = "_none_";
		
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
		
		public override void OnStart(StartState state)
        {
            if (State == AnimatorState.Opening) { State = AnimatorState.Closed; }
            else if(State == AnimatorState.Closing) { State = AnimatorState.Opened; }
        }
		
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

