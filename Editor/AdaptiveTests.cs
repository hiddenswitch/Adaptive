using UnityEngine;
using System.Collections.Generic;
using NUnit.Framework;
using HiddenSwitch.Multiplayer;
using System;

namespace HiddenSwitch.Multiplayer.Tests
{
	[TestFixture]
	public class AdaptiveTests
	{
		public bool Logging = false;

		[Test]
		public void BasicIntegration ()
		{
			var frames = 60;
			for (var bufferSize = 0; bufferSize < 17; bufferSize++) {
				var adaptive1 = GetTestAdaptive ("1", 1, bufferSize);
				var adaptive2 = GetTestAdaptive ("2", 2, bufferSize);
				adaptive1.Host (new GameStateWithInputs ());
				Assert.IsNotNull (adaptive1.Network.LatestState);
				Assert.IsNotNull (adaptive1.Simulation.LatestState);
				Assert.IsNull (adaptive2.Network.LatestState);
				Assert.IsNull (adaptive2.Simulation.LatestState);
				var game1 = adaptive1.GameManager as GameManager;
				var game2 = adaptive2.GameManager as GameManager;
				game1.testInput = Vector3.right;
				game2.testInput = Vector3.up;
				adaptive2.Connect ("1");
				Assert.AreEqual (adaptive1.Simulation.ElapsedFrameCount, 0);
				Assert.AreEqual (adaptive2.Simulation.ElapsedFrameCount, 0);
				var time = (long)(((double)frames / 60.0) * 10e10 + bufferSize / 60.0 * 10e10);
				TestClock.ElapseTimers (time);
				Assert.AreEqual (frames, adaptive1.Simulation.ElapsedFrameCount);
				Assert.AreEqual (frames, adaptive2.Simulation.ElapsedFrameCount);
				Assert.AreEqual (GetExpectedLocation (adaptive2.SimulationClock.ElapsedFrameCount), adaptive2.Simulation.LatestState.location);
				Assert.AreEqual (GetExpectedLocation (adaptive1.SimulationClock.ElapsedFrameCount), adaptive1.Simulation.LatestState.location);
				TestClock.ClearOldTimers ();
			}
		}

		[Test]
		public void DroppedFrameTests ()
		{
			var oneSecondFrames = 60;
			var oneSecond = 10e10;
			var oneFrameTime = (long)(oneSecond / oneSecondFrames);

			for (var bufferSize = 0; bufferSize < 17; bufferSize++) {
				for (var framesDropped = 1; framesDropped < 17; framesDropped++) {
					for (var dropoutMoment = 0; dropoutMoment < oneSecondFrames; dropoutMoment++) {
						var adaptive1 = GetTestAdaptive ("1", 1, bufferSize);
						var adaptive2 = GetTestAdaptive ("2", 2, bufferSize);
						var network1 = adaptive1.Network;
						var network2 = adaptive2.Network;
						adaptive1.Host (new GameStateWithInputs ());
						Assert.IsNotNull (adaptive1.Network.LatestState);
						Assert.IsNotNull (adaptive1.Simulation.LatestState);
						Assert.IsNull (adaptive2.Network.LatestState);
						Assert.IsNull (adaptive2.Simulation.LatestState);
						var game1 = adaptive1.GameManager as GameManager;
						var game2 = adaptive2.GameManager as GameManager;
						game1.testInput = Vector3.right;
						game2.testInput = Vector3.up;
						adaptive2.Connect ("1");

						var transport2 = network2.Transport as TestTransport;
						for (var i = 0; i < oneSecondFrames; i++) {
							if (i >= dropoutMoment
							    && i < dropoutMoment + framesDropped) {
								transport2.FailNextSend ();
							}
							TestClock.ElapseTimers (oneFrameTime);
						}
						// The number of simulation steps should be exactly the same, regardless if one client dropped frames or the other
						Assert.AreEqual (adaptive1.Simulation.ElapsedFrameCount, adaptive2.Simulation.ElapsedFrameCount);
						Assert.AreEqual (GetExpectedLocation (adaptive2.SimulationClock.ElapsedFrameCount), adaptive2.Simulation.LatestState.location);
						Assert.AreEqual (GetExpectedLocation (adaptive1.SimulationClock.ElapsedFrameCount), adaptive1.Simulation.LatestState.location);
						TestClock.ClearOldTimers ();
					}
				}
			}
		}

		Vector3 GetExpectedLocation (int elapsedFrames)
		{
			return new Vector3 (elapsedFrames, elapsedFrames, 0);
		}

		Adaptive<GameStateWithInputs, GameInput> GetTestAdaptive (string hostname, int peerId, int bufferSize)
		{
			var game = new GameManager () { testInput = Vector3.right };

			var inputClock = new TestClock (framesPerSecond: 60, autostart: false, endOfFrame: true, startFrame: 0, name: string.Format ("H{0} inputclock", hostname))
			{ ExecutionOrder = 2 };
			var network = new Network<GameStateWithInputs, GameInput> (inputClock, transport: new TestTransport (hostname), peerId: peerId);

			var adaptive = new Adaptive<GameStateWithInputs, GameInput> (game, simulationDelay: bufferSize, inputClock: inputClock, simulationClock: new TestClock (framesPerSecond: 60, autostart: false, name: string.Format ("H{0} simclock", hostname)) { ExecutionOrder = 0 }, bufferClock: new TestClock (framesPerSecond: 60) { ExecutionOrder = 1 }, network: network);
			adaptive.Simulation.InputHandler = delegate(GameStateWithInputs mutableState, System.Collections.Generic.KeyValuePair<int, GameInput>[] inputs, int frameIndex) {
				foreach (var kv in inputs) {
					var input = kv.Value;
					mutableState.Move (input.direction);
				}
			};
			return adaptive;
		}
	}
}
