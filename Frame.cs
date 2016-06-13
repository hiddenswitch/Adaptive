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
	public class Frame
	{
		public int frameIndex;
		public Dictionary<PeerId, CommandList> commands = new Dictionary<PeerId, CommandList> (3);
	}
	
}