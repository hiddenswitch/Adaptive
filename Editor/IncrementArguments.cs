using UnityEngine;
using System.Collections;
using NUnit.Framework;
using HiddenSwitch.Multiplayer;

namespace HiddenSwitch.Multiplayer.Tests
{

	public class IncrementArguments : CommandArguments {
		public int amount;

		public override void Deserialize (System.IO.BinaryReader readFrom)
		{
			amount = readFrom.ReadInt32 ();
		}

		public override void Serialize (System.IO.BinaryWriter writeTo)
		{
			writeTo.Write (amount);
		}
	}
	
}
