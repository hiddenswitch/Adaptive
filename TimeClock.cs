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

		protected int m_elapsedFrameCount;

		public virtual int ElapsedFrameCount { get { return m_elapsedFrameCount + StartFrame; } }

		public virtual int FramesPerSecond { get; protected set; }

		public virtual bool EndOfFrame { get; protected set; }

		public virtual int StartFrame { get; protected set; }

		internal TimeClockHelper m_timeClockHelper;

		public TimeClock (bool autostart = true, int framesPerSecond = 30, bool endOfFrame = false, int startFrame = 0)
		{
			FramesPerSecond = framesPerSecond;
			EndOfFrame = endOfFrame;
			StartFrame = startFrame;
			var timer = new GameObject ("Timer Helper");
			m_timeClockHelper = timer.AddComponent<TimeClockHelper> ();
			m_timeClockHelper.framesPerSecond = framesPerSecond;
			m_timeClockHelper.endOfFrame = endOfFrame;

			if (autostart) {
				m_timeClockHelper.running = true;
			}
		}

		internal virtual void HelperTick ()
		{
			if (Tick != null) {
				Tick (ElapsedFrameCount);
			}
			m_elapsedFrameCount++;
		}

		protected virtual void OnHelperTick ()
		{
			HelperTick ();
		}

		/// <summary>
		/// Stops the timer.
		/// </summary>
		public virtual void Stop ()
		{
			m_timeClockHelper.running = false;
		}

		/// <summary>
		/// Starts the timer.
		/// </summary>
		public virtual void Start ()
		{
			m_timeClockHelper.running = true;
		}

		/// <summary>
		/// Is the timer running?
		/// </summary>
		/// <value><c>true</c> if running; otherwise, <c>false</c>.</value>
		public virtual bool Running {
			get {
				return m_timeClockHelper.running;
			}
			set {
				m_timeClockHelper.running = value;
			}
		}
	}
	
}