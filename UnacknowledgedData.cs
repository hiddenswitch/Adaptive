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
	/// A list of unacknowledged data
	/// </summary>
	public class UnacknowledgedData
	{
		public Input input;
		public IList<CommandWithArguments> queuedCommands;
	}
	
}