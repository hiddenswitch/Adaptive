using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using PeerId = System.Int32;
using ConnectionId = System.Int32;
using FrameIndex = System.Int32;

namespace HiddenSwitch.Multiplayer
{

	public class Input : HiddenSwitch.Multiplayer.IBinarySerializable, ICloneable
	{
		public virtual void Serialize (BinaryWriter writeTo)
		{
		}

		public virtual void Deserialize (BinaryReader readFrom)
		{
		}

		public override bool Equals (object obj)
		{
			return true;
		}

		public virtual int GetHashCode (Input obj)
		{
			return 0;
		}

		#region ICloneable implementation

		public virtual object Clone ()
		{
			return new Input ();
		}

		#endregion

		public Input ()
		{
		}
	}
}