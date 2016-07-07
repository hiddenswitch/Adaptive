using UnityEngine;
using System.Collections.Generic;
using NUnit.Framework;
using HiddenSwitch.Multiplayer;
using System;
using System.Linq;

namespace HiddenSwitch.Multiplayer.Tests
{

	public class TestClock : TimeClock
	{
		public string Name { get; set; }

		public bool Logging { get; set; }

		public int ExecutionOrder;

		public static void ElapseTimers (long ticks)
		{
			for (var i = 0L; i < ticks; i += (long)(10e10 / 60.0)) {
				foreach (var clockRef in m_timers.OrderBy(t => ((TestClock)(t.Target)).ExecutionOrder)) {
					var clock = (TestClock)clockRef.Target;
					if (clock.FramesPerSecond != 60) {
						throw new InvalidOperationException ("Test clock only works with 60 fps clocks");
					}
					if (clock.Logging) {
						System.Console.WriteLine ("Timer {0} ticked", clock.Name);
					}
					clock.Timer.ElapseTicks ((long)(10e10 / 60.0));
				}
			}
		}

		protected static HashSet<WeakReference> m_timers = new HashSet<WeakReference> ();
		public DeterministicTimer Timer;
		protected bool m_running;

		public override long ElapsedTicks {
			get {
				return Timer.ElapsedTicks;
			}
		}

		public TestClock (bool autostart = true, int framesPerSecond = 30, bool endOfFrame = false, int startFrame = 0, string name = null)
		{
			Logging = false;
			Name = name;
			FramesPerSecond = framesPerSecond;
			EndOfFrame = endOfFrame;
			StartFrame = startFrame;

			Timer = new DeterministicTimer ();
			m_timers.Add (new WeakReference (this));
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
					var ticks = (long)(10e10 * (1.0 / FramesPerSecond));
					Timer.StartTimer (OnHelperTick, ticks);
				} else {
					Timer.StopTimer ();
				}
			}
		}

		public override void Stop ()
		{
			Running = false;
		}

		public override void Start ()
		{
			Running = true;
		}

		void OnSystemTimerElapsed (object sender, System.Timers.ElapsedEventArgs e)
		{
			OnHelperTick ();
		}

		public static void ClearOldTimers ()
		{
			m_timers.Clear ();
		}
	}

}
