using UnityEngine;
using System.Collections;
using NUnit.Framework;
using HiddenSwitch.Multiplayer;

namespace HiddenSwitch.Multiplayer.Tests
{

	/// <summary>
	/// A test state
	/// </summary>
	public sealed class GameState : HiddenSwitch.Multiplayer.State {
		public int count;

		public void Increment(IncrementArguments incrementArguments) {
			count += incrementArguments.amount;
		}

		public override void Serialize (System.IO.BinaryWriter writeTo)
		{
			writeTo.Write (count);
		}

		public override void Deserialize (System.IO.BinaryReader readFrom)
		{
			count = readFrom.ReadInt32 ();
		}

		public override object Clone ()
		{
			return new GameState() {
				count = this.count
			};
		}
	}
}
