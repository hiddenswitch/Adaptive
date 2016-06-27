using UnityEngine;
using System.Collections.Generic;
using NUnit.Framework;
using HiddenSwitch.Multiplayer;
using System;

namespace HiddenSwitch.Multiplayer.Tests
{
	[TestFixture]
	public class PlayoutDelayedClockTests
	{
		[Test]
		public void DelayedCorrectly ()
		{
			for (var i = 0; i < 60; i++) {
				var testClock = new TestClock (autostart: true, framesPerSecond: 60);
				var clock = new PlayoutDelayedClock (simulationRate: 60, playoutDelayFrameCount: i, timeClock: testClock);
				clock.PeerCount = 1;
				var frame = 0;
				for (; frame < 1000; frame++) {
					clock.IncrementReadyForFrame (frame);
					testClock.Timer.ElapseTicks ((long)(10e10 * 1.0 / 60.0));
				}
				Assert.AreEqual (frame, 1000);
				Assert.AreEqual (frame - i, clock.ElapsedFrameCount);
			}
		}

		[Test]
		public void DelayedCorrectlyWithTwoPeers ()
		{
			for (var i = 0; i < 60; i++) {
				var testClock = new TestClock (autostart: true, framesPerSecond: 60);
				var clock = new PlayoutDelayedClock (simulationRate: 60, playoutDelayFrameCount: i, timeClock: testClock);
				clock.PeerCount = 2;
				var frame = 0;
				for (; frame < 1000; frame++) {
					clock.IncrementReadyForFrame (frame);
					clock.IncrementReadyForFrame (frame);
					testClock.Timer.ElapseTicks ((long)(10e10 * 1.0 / 60.0));
				}
				Assert.AreEqual (frame - i, clock.ElapsedFrameCount);
			}
		}

		[Test]
		public void DelayedCorrectlySuperLatePeer ()
		{
			for (var i = 0; i < 60; i++) {
				var testClock = new TestClock (autostart: true, framesPerSecond: 60);
				var clock = new PlayoutDelayedClock (simulationRate: 60, playoutDelayFrameCount: i, timeClock: testClock);
				clock.PeerCount = 2;
				var frame = 0;
				for (; frame < 1000; frame++) {
					clock.IncrementReadyForFrame (frame);
					testClock.Timer.ElapseTicks ((long)(10e10 * 1.0 / 60.0));
				}

				Assert.AreEqual (0, clock.ElapsedFrameCount);

				for (frame = 0; frame < 1000; frame++) {
					clock.IncrementReadyForFrame (frame);
					Assert.AreEqual (frame, clock.HighestExecutableFrame);
					testClock.Timer.ElapseTicks ((long)(10e10 * 1.0 / 60.0));
				}

				Assert.AreEqual (frame - i, clock.ElapsedFrameCount, "Incrementing the peer count a second time did not result in a correctly resumed PlayoutDelayedClock");
			}
		}
	}
}

