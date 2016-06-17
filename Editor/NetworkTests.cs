using UnityEngine;
using System.Collections.Generic;
using NUnit.Framework;
using HiddenSwitch.Multiplayer;
using System;

namespace HiddenSwitch.Multiplayer.Tests
{
	[TestFixture]
	public class NetworkTests
	{
		[Test]
		public void AllHaveStateAfterConnection ()
		{
			var clock = new ManualClock ();
			var transport1 = new TestTransport ("1");
			var transport2 = new TestTransport ("2");
			var network1 = new HiddenSwitch.Multiplayer.Network<GameState> (clock: clock, transport: transport1, startState: null);
			var network2 = new HiddenSwitch.Multiplayer.Network<GameState> (clock: clock, transport: transport2, startState: new GameState () { count = 52 });

			var otherConnectionId = network1.AddPeer ("2", 4);
			Assert.IsTrue (network1.AllHaveState);
			Assert.IsTrue (network2.AllHaveState);
			Assert.AreEqual (network1.LatestState.count, 52);
		}
	}
}
