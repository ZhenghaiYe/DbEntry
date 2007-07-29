
#region usings

using System;
using org.hanzify.llf.util.TimingTask;

#endregion

namespace org.hanzify.llf.UnitTest.util.timingTask
{
	public class MockTimer : ITimer
	{
		public MockTimer() {}

		public event System.Timers.ElapsedEventHandler Elapsed;

		public bool Enabled
		{
			get { return false; }
			set {}
		}

		public double Interval
		{
			get { return 0; }
			set {}
		}

		public void Start() {}

		public void Stop() {}

		public void RaiseElapsed()
		{
			if ( Elapsed != null )
			{
				Elapsed(null, null);
			}
		}
	}
}
