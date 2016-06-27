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
	/// Represents a single queued command
	/// </summary>
	public class CommandWithArguments
	{
		public byte CommandId;
		public CommandArguments Arguments;
	}
}