using System;

namespace HiddenSwitch.Multiplayer
{
	/// <summary>
	/// An interface to a clock source.
	/// </summary>
	public interface IClock
	{
		/// <summary>
		/// An event raised whenever this clock ticks. The argument is the current frame.
		/// </summary>
		event Action<int> Tick;
		event Action<int> LateTick;

		/// <summary>
		/// Gets the number of frames that have elapsed so far.
		/// </summary>
		/// <value>The current frame.</value>
		int ElapsedFrameCount { get; }

		/// <summary>
		/// Gets the value of the first frame of this clock. Helps synchronize clocks.
		/// </summary>
		/// <value>The start frame.</value>
		int StartFrame { get; set; }
	}
}

