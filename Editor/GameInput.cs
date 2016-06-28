using UnityEngine;
using System.Collections;
using NUnit.Framework;
using HiddenSwitch.Multiplayer;

namespace HiddenSwitch.Multiplayer.Tests
{
	public sealed class GameInput : Input
	{
		public Vector3 direction;

		public override void Serialize (System.IO.BinaryWriter writeTo)
		{
			writeTo.Write (direction.x);
			writeTo.Write (direction.y);
			writeTo.Write (direction.z);
		}

		public override void Deserialize (System.IO.BinaryReader readFrom)
		{
			direction.x = readFrom.ReadSingle ();
			direction.y = readFrom.ReadSingle ();
			direction.z = readFrom.ReadSingle ();
		}

		public override bool Equals (object obj)
		{
			return direction == ((GameInput)obj).direction;
		}

		public override int GetHashCode (Input obj)
		{
			return direction.GetHashCode ();
		}

		public override object Clone ()
		{
			return new GameInput () { direction = this.direction };
		}
	}
	
}
