using System;

namespace HiddenSwitch.Multiplayer.Tests
{
	/// <summary>
	/// A timer to be used during unit testing. It allows to start timer operations
	/// on the same thread as the unit test.
	/// </summary>
	public class DeterministicTimer : ITimer
	{
		private Action timerAction;
		private long timerInterval;

		private bool isStarted;
		private long elapsedTime;

		/// <summary>
		/// The number of ticks that have elapsed while running.
		/// </summary>
		/// <value>The elapsed ticks.</value>
		public long ElapsedTicks {
			get;
			protected set;
		}

		#region ITimer methods

		public void StartTimer (Action action, long intervalTicks)
		{
			isStarted = true;
			timerAction = action;
			timerInterval = intervalTicks;
			elapsedTime = 0;
		}

		public bool IsStarted ()
		{
			return isStarted;
		}

		public void StopTimer ()
		{
			isStarted = false;
		}

		#endregion

		/// <summary>
		/// Tell the timer that some seconds have elapsed and let the timer execute the timer action. 
		/// </summary>
		/// <param name="seconds"></param>
		public void ElapseTicks (long ticks)
		{
			long newElapsedTime = elapsedTime + ticks;
			if (isStarted) {
				ElapsedTicks += ticks;
				long executionCountBefore = elapsedTime / timerInterval;
				long executionCountAfter = newElapsedTime / timerInterval;

				for (int i = 0; i < executionCountAfter - executionCountBefore; i++) {
					timerAction ();
				}
			}
			elapsedTime = newElapsedTime;
		}
	}
}
