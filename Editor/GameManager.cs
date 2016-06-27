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
	
}
