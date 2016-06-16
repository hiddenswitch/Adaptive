using UnityEngine;
using System.Collections;
using NUnit.Framework;
using HiddenSwitch.Multiplayer;

namespace HiddenSwitch.Multiplayer.Tests
{
	[TestFixture]
	public class SimulationTests
	{
		[Test]
		public void ExecutesCorrectly ()
		{
			var manualClock = new ManualClock ();
			var comparisonState = new GameState ();
			var simulation = new Simulation<GameState> (manualClock);
			simulation.AddCommandHandler<IncrementArguments> (TestStateCommands.Increment, delegate(GameState mutableState, IncrementArguments arguments, int peerId, int frameIndex) {
				mutableState.Increment (arguments);
			});

			var qty = 20;
			for (var i = 0; i < qty; i++) {
				if (i % 2 == 0) {
					comparisonState.Increment (new IncrementArguments () { amount = 1 });
					simulation.CallCommand<IncrementArguments> (TestStateCommands.Increment, new IncrementArguments () { amount = 1 }, 0);
				}
				manualClock.Step ();
			}

			Assert.AreEqual (comparisonState.count, simulation.State.count);
		}
	}
}
