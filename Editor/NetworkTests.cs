﻿using UnityEngine;
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

			network1.AddPeer ("2", 4);
			Assert.IsTrue (network1.AllHaveState);
			Assert.IsTrue (network2.AllHaveState);
			Assert.AreEqual (network1.LatestState.count, 52);
		}

		[Test]
		public void TransferredInputOnce ()
		{
			var clock = new ManualClock ();
			var transport1 = new TestTransport ("1");
			var transport2 = new TestTransport ("2");
			var network1 = new HiddenSwitch.Multiplayer.Network<GameState, GameInput> (clock: clock, transport: transport1, startState: null);
			var network2 = new HiddenSwitch.Multiplayer.Network<GameState, GameInput> (clock: clock, transport: transport2, startState: new GameState () { count = 52 });

			network1.AddPeer ("2", 4);
			network1.QueueInput (new GameInput () { direction = Vector3.up });
			network2.QueueInput (new GameInput () { direction = Vector3.back });
			clock.Step ();
			var input1to2 = network2.GetSimulationFrame (0).Inputs [network1.MyPeerId] as GameInput;
			Assert.IsNotNull (input1to2);
			Assert.AreEqual (Vector3.up, input1to2.direction);
			var input2to1 = network1.GetSimulationFrame (0).Inputs [network2.MyPeerId] as GameInput;
			Assert.IsNotNull (input2to1);
			Assert.AreEqual (Vector3.back, input2to1.direction);
		}
	}
}
