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
	/// A list of message types that can be sent over the network
	/// </summary>
	internal static class MessageType
	{
		/// <summary>
		/// This message is an acknowledgement of commands.
		/// </summary>
		internal const byte AcknowledgeCommands = 1;
		/// <summary>
		/// This message is a complete game state.
		/// </summary>
		internal const byte State = 2;
		/// <summary>
		/// This message contains commands.
		/// </summary>
		internal const byte Commands = 3;
		/// <summary>
		/// This message contains no commands.
		/// </summary>
		internal const byte EmptyCommands = 4;
		/// <summary>
		/// This message contains information about the peer
		/// </summary>
		internal const byte PeerInfo = 5;
		/// <summary>
		/// This message acknowledges the receipt of state.
		/// </summary>
		internal const byte AcknowledgeState = 6;
	}
}