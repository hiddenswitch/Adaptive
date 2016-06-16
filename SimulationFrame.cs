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

	public sealed class SimulationFrame
	{
		public FrameIndex frameIndex;
		public IList<CommandWithArgumentsAndPeer> Commands = new List<CommandWithArgumentsAndPeer> ();
	}
	
}