using UnityEngine;
using System.Collections;
using NUnit.Framework;
using HiddenSwitch.Multiplayer;

namespace HiddenSwitch.Multiplayer.Tests
{
	public sealed class GameStateWithInputs : HiddenSwitch.Multiplayer.State
	{
		public Vector3 location;

		public void Move (Vector3 direction)
		{
			location += direction;
		}

		public override void Serialize (System.IO.BinaryWriter writeTo)
		{
			writeTo.Write (location.x);
			writeTo.Write (location.y);
			writeTo.Write (location.z);
		}

		public override void Deserialize (System.IO.BinaryReader readFrom)
		{
			location.x = readFrom.ReadSingle ();
			location.y = readFrom.ReadSingle ();
			location.z = readFrom.ReadSingle ();
		}

		public override object Clone ()
		{
			return new GameStateWithInputs () {
				location = this.location
			};
		}

		public GameStateWithInputs () : base ()
		{
		}
	}

	/// <summary>
	/// A test state
	/// </summary>
	public sealed class GameState : HiddenSwitch.Multiplayer.State
	{
		public int count;

		public void Increment (IncrementArguments incrementArguments)
		{
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
			return new GameState () {
				count = this.count
			};
		}
	}
}
