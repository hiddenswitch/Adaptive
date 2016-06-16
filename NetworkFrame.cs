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
	/// Frame data
	/// </summary>
	public class NetworkFrame
	{
		public int frameIndex;
		public Dictionary<PeerId, SerializedCommandList> commands = new Dictionary<PeerId, SerializedCommandList> (2);
	}
	
}