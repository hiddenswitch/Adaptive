using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace HiddenSwitch.Multiplayer
{

	/// <summary>
	/// A default time or frame based clock.
	/// </summary>
	public class TimeClock : IClock
	{
		public event Action<int> Tick;

		public int ElapsedFrameCount { get; private set; }

		public int FramesPerSecond { get; private set; }

		public bool EndOfFrame { get; private set; }

		internal TimeClockHelper m_timeClockHelper;

		public TimeClock (bool autostart = true, int framesPerSecond = 30, bool endOfFrame = false)
		{
			FramesPerSecond = framesPerSecond;
			EndOfFrame = endOfFrame;

			var timer = new GameObject ("Timer Helper");
			m_timeClockHelper = timer.AddComponent<TimeClockHelper> ();
			m_timeClockHelper.framesPerSecond = framesPerSecond;
			m_timeClockHelper.endOfFrame = endOfFrame;

			if (autostart) {
				m_timeClockHelper.running = true;
			}
		}

		internal void HelperTick ()
		{
			ElapsedFrameCount++;
			if (Tick != null) {
				Tick (ElapsedFrameCount - 1);
			}
		}

		/// <summary>
		/// Stops the timer.
		/// </summary>
		public void Stop ()
		{
			m_timeClockHelper.running = false;
		}

		/// <summary>
		/// Starts the timer.
		/// </summary>
		public void Start ()
		{
			m_timeClockHelper.running = true;
		}

		/// <summary>
		/// Is the timer running?
		/// </summary>
		/// <value><c>true</c> if running; otherwise, <c>false</c>.</value>
		public bool Running {
			get {
				return m_timeClockHelper.running;
			}
			set {
				m_timeClockHelper.running = value;
			}
		}
	}
	
}