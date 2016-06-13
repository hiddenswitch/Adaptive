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
	}
}

