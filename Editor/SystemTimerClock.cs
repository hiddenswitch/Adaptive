using UnityEngine;
using System.Collections.Generic;
using NUnit.Framework;
using HiddenSwitch.Multiplayer;
using System;

namespace HiddenSwitch.Multiplayer.Tests
{

	public class SystemTimerClock : TimeClock
	{
		protected System.Timers.Timer m_timer;
		protected bool m_running;

		public SystemTimerClock (bool autostart = true, int framesPerSecond = 30, bool endOfFrame = false, int startFrame = 0)
		{
			FramesPerSecond = framesPerSecond;
			EndOfFrame = endOfFrame;
			StartFrame = startFrame;

			m_timer = new System.Timers.Timer (1.0 / FramesPerSecond);
			m_timer.AutoReset = true;
			m_timer.Elapsed += OnSystemTimerElapsed;

			if (autostart) {
				Running = true;
			}
		}

		public override bool Running {
			get {
				return m_running;
			}
			set {
				m_running = value;
				if (m_running) {
					m_timer.Start ();
				} else {
					m_timer.Stop ();
				}
			}
		}

		void OnSystemTimerElapsed (object sender, System.Timers.ElapsedEventArgs e)
		{
			OnHelperTick ();
		}
	}

}
