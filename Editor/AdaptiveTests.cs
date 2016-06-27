using UnityEngine;
using System.Collections.Generic;
using NUnit.Framework;
using HiddenSwitch.Multiplayer;
using System;

namespace HiddenSwitch.Multiplayer.Tests
{
	public class GameManager : IAdaptiveDelegate<GameStateWithInputs, GameInput>
	{
		#region IAdaptiveDelegate implementation

		public Vector3 testInput;

		public GameInput GetCurrentInput ()
		{
			return new GameInput () {
				direction = testInput
			};
		}

		public GameStateWithInputs GetStartState ()
		{
			return new GameStateWithInputs ();
		}

		#endregion
		
	}

	[TestFixture]
	public class AdaptiveTests
	{
		public bool Logging = false;

		[Test]
		public void BasicIntegration ()
		{
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
				var time = (long)(10e10 + bufferSize / 60.0 * 10e10);
				TestClock.ElapseTimers (time);
				Assert.AreEqual (60, adaptive1.Simulation.ElapsedFrameCount);
				Assert.AreEqual (GetExpectedLocation (adaptive2.SimulationClock.ElapsedFrameCount), adaptive2.Simulation.LatestState.location);
				Assert.AreEqual (GetExpectedLocation (adaptive1.SimulationClock.ElapsedFrameCount), adaptive1.Simulation.LatestState.location);
			}
		}

		Vector3 GetExpectedLocation (int elapsedFrames)
		{
			return new Vector3 (elapsedFrames, elapsedFrames, 0);
		}

		Adaptive<GameStateWithInputs, GameInput> GetTestAdaptive (string hostname, int peerId, int bufferSize)
		{
			var game = new GameManager () { testInput = Vector3.right };

			var inputClock = new TestClock (framesPerSecond: 60, autostart: false, endOfFrame: true, startFrame: 0, name: string.Format ("H{0} inputclock", hostname));
			var network = new Network<GameStateWithInputs, GameInput> (inputClock, transport: new TestTransport (hostname), peerId: peerId);

			var adaptive = new Adaptive<GameStateWithInputs, GameInput> (game, simulationDelay: bufferSize, inputClock: inputClock, simulationClock: new TestClock (framesPerSecond: 60, autostart: false, name: string.Format ("H{0} simclock", hostname)), bufferClock: new TestClock (framesPerSecond: 60), network: network);
			adaptive.Simulation.InputHandler = delegate(GameStateWithInputs mutableState, int id, GameInput input, int frameIndex) {
				if (Logging) {
					System.Console.WriteLine ("frame {3} host {0} processing peerId {1} value {2}", hostname, id, input.direction, frameIndex);
				}
				mutableState.Move (input.direction);
			};
			return adaptive;
		}
	}
}
