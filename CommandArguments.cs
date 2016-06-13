using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using PeerId = System.Int32;
using ConnectionId = System.Int32;

namespace HiddenSwitch.Multiplayer
{

	/// <summary>
	/// A set of command arguments that correponds to logical player actions, player inputs or server-initiated actions (NPC actions).
	/// </summary>
	public abstract class CommandArguments : IBinarySerializable
	{
		public virtual void Serialize (BinaryWriter writeTo)
		{
			throw new NotImplementedException ();
		}

		public virtual void Deserialize (BinaryReader readFrom)
		{
			throw new NotImplementedException ();
		}

		public CommandArguments ()
		{
		}
	}
	
}