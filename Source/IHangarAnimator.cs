using System;

namespace AtHangar
{
	public enum HangarGates
    {
        Opened,
        Opening,
        Closed,
        Closing,
    }
	
	public interface IHangarAnimator
	{
        HangarGates GatesState { get; }
        void Open();
        void Close();
		bool Toggle();
    }
	
	public class DummyHangarAnimator : PartModule, IHangarAnimator
	{
		[KSPField(isPersistant = true)]
        public string State;
		
        public HangarGates GatesState 
		{
			get
            {
                try { return (HangarGates)Enum.Parse(typeof(HangarGates), State); }
                catch
                {
                    GatesState = HangarGates.Closed;
                    return GatesState;
                }
            }
            private set { State = Enum.GetName(typeof(HangarGates), value); }
		}
		
		public override void OnStart(StartState state)
        {
            if (GatesState == HangarGates.Opening) { GatesState = HangarGates.Closed; }
            else if (GatesState == HangarGates.Closing) { GatesState = HangarGates.Opened; }
        }
		
        public void Open() { GatesState = HangarGates.Opened; }
        public void Close() { GatesState = HangarGates.Closed; }
		
		public bool Toggle()
		{
			if(GatesState == HangarGates.Closed 
			   || GatesState == HangarGates.Closing)
			{
				Open ();
				return true;
			}
			else 
			{
				Close ();
				return false;
			}
		}
	}
}

