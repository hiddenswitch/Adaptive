using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using HiddenSwitch.Multiplayer;

namespace HiddenSwitch.Multiplayer.Tests
{
	[TestFixture]
	public class SimulationTests
	{
		[Test]
		public void ExecutesInputs ()
		{
			var manualClock = new ManualClock ();
			var comparisonState = new GameStateWithInputs ();
			var simulation = new Simulation<GameStateWithInputs, GameInput> (manualClock, new GameStateWithInputs ());
			simulation.InputHandler = delegate(GameStateWithInputs mutableState, System.Collections.Generic.KeyValuePair<int, GameInput>[] inputs, int frameIndex) {
				foreach (var kv in inputs) {
					var input = kv.Value;
					mutableState.Move (input.direction);
				}
			};
			var myPeerId = 0;
			var moves = 20;
			var systemRandom = new System.Random ();
			for (var i = 0; i < moves; i++) {
				var direction = new Vector3 ((float)systemRandom.NextDouble (), (float)systemRandom.NextDouble (), (float)systemRandom.NextDouble ());

				comparisonState.location += direction;
				var input = new GameInput () { direction = direction };
				simulation.SetInput (input, myPeerId, simulation.ElapsedFrameCount);
				manualClock.Step ();
			}

			Assert.AreEqual (comparisonState.location, simulation.LatestState.location);
		}

		[Test]
		public void ExtendsFrames ()
		{
			var manualClock = new ManualClock ();
			var comparisonState = new GameStateWithInputs ();
			var simulation = new Simulation<GameStateWithInputs, GameInput> (manualClock, new GameStateWithInputs ());
			simulation.InputHandler = delegate(GameStateWithInputs mutableState, System.Collections.Generic.KeyValuePair<int, GameInput>[] inputs, int frameIndex) {
				foreach (var kv in inputs) {
					var input = kv.Value;
					mutableState.Move (input.direction);
				}
			};
			var moves = 20;
			var systemRandom = new System.Random ();
			for (var i = 0; i < moves; i++) {
				var direction = new Vector3 ((float)systemRandom.NextDouble (), (float)systemRandom.NextDouble (), (float)systemRandom.NextDouble ());

				comparisonState.location += direction;
				var input = new GameInput () { direction = direction };
				simulation.SetInput (input, 0, simulation.ElapsedFrameCount);

				direction = new Vector3 ((float)systemRandom.NextDouble (), (float)systemRandom.NextDouble (), (float)systemRandom.NextDouble ());

				comparisonState.location += direction;
				input = new GameInput () { direction = direction };
				simulation.SetOrExtendFrame (i, new SimulationFrame () {
					FrameIndex = simulation.ElapsedFrameCount,
					Inputs = new Dictionary<int, Input> () {
						{ 1, input }
					}
				});

				manualClock.Step ();
			}
			Assert.AreEqual (comparisonState.location, simulation.LatestState.location);
		}

		[Test]
		public void ExecutesTwoPeersInputs ()
		{
			var manualClock = new ManualClock ();
			var comparisonState = new GameStateWithInputs ();
			var simulation = new Simulation<GameStateWithInputs, GameInput> (manualClock, new GameStateWithInputs ());
			simulation.InputHandler = delegate(GameStateWithInputs mutableState, System.Collections.Generic.KeyValuePair<int, GameInput>[] inputs, int frameIndex) {
				foreach (var kv in inputs) {
					var input = kv.Value;
					mutableState.Move (input.direction);
				}
			};
			var moves = 20;
			var systemRandom = new System.Random ();
			for (var i = 0; i < moves; i++) {
				var direction = new Vector3 ((float)systemRandom.NextDouble (), (float)systemRandom.NextDouble (), (float)systemRandom.NextDouble ());

				comparisonState.location += direction;
				var input = new GameInput () { direction = direction };
				simulation.SetInput (input, 0, simulation.ElapsedFrameCount);

				direction = new Vector3 ((float)systemRandom.NextDouble (), (float)systemRandom.NextDouble (), (float)systemRandom.NextDouble ());

				comparisonState.location += direction;
				input = new GameInput () { direction = direction };
				simulation.SetInput (input, 1, simulation.ElapsedFrameCount);

				manualClock.Step ();
			}
			Assert.AreEqual (comparisonState.location, simulation.LatestState.location);

		}

		[Test]
		public void BuffersFrames ()
		{
			var manualClock = new ManualClock ();
			var comparisonState = new GameStateWithInputs ();
			var simulation = new Simulation<GameStateWithInputs, GameInput> (manualClock, new GameStateWithInputs ());
			simulation.InputHandler = delegate(GameStateWithInputs mutableState, System.Collections.Generic.KeyValuePair<int, GameInput>[] inputs, int frameIndex) {
				foreach (var kv in inputs) {
					var input = kv.Value;
					mutableState.Move (input.direction);
				}
			};
			var myPeerId = 0;
			var moves = 20;
			var systemRandom = new System.Random ();
			for (var i = 0; i < moves; i++) {
				var direction = new Vector3 ((float)systemRandom.NextDouble (), (float)systemRandom.NextDouble (), (float)systemRandom.NextDouble ());

				comparisonState.location += direction;
				var input = new GameInput () { direction = direction };
				simulation.SetInput (input, myPeerId, simulation.ElapsedFrameCount);
				manualClock.Step ();
			}

			var frameBuffer = simulation.FrameBuffer;
			var j = moves - 1;
			for (; j >= moves - frameBuffer; j--) {
				Assert.IsNotNull (simulation [j]);
			}
			Assert.IsNull (simulation [j - 1]);
		}

	}
}
