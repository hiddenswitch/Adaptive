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
	/// A complete game state. You should be able to render your entire scene from this state class without any external dependencies.
	/// </summary>
	public abstract class State : IBinarySerializable, ICloneable
	{
		public virtual void Serialize (BinaryWriter writeTo)
		{
			throw new NotImplementedException ();
		}

		public virtual void Deserialize (BinaryReader readFrom)
		{
			throw new NotImplementedException ();
		}

		public virtual object Clone ()
		{
			throw new NotImplementedException ();
		}
	}
	
}