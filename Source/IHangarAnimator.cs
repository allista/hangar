using System;

namespace AtHangar
{
	public enum HangarState
    {
        Opened,
        Opening,
        Closed,
        Closing,
    }
	
	public interface IHangarAnimator
	{
        HangarState CurrentState { get; }
        void Open();
        void Close();
		void Toggle();
    }
	
	public class DummyHangarAnimator : IHangarAnimator
	{
        public HangarState CurrentState { get; private set; }
        public void Open() { CurrentState = HangarState.Opened; }
        public void Close() { CurrentState = HangarState.Closed; }
		public void Toggle()
		{
			if (CurrentState == HangarState.Closed || CurrentState == HangarState.Closing)
				Open ();
			else Close ();
		}

        public DummyHangarAnimator() { CurrentState = HangarState.Closed; }
	}
}

