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
	/// A command list
	/// </summary>
	public class SerializedCommandList : IDisposable
	{
		/// <summary>
		/// Which frame index (point in time) does this frame instance correspond to?
		/// </summary>
		public int frameIndex;
		public long dataStartIndex;
		public ushort dataLength;
		public BinaryReader data;
		public byte[] commandIds;
		public int peerId;

		~SerializedCommandList ()
		{
			data.Close ();
		}

		public void Dispose ()
		{
			data.Close ();
		}
	}
	
}