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
	/// A helper interface for network data receivers.
	/// </summary>
	internal interface INetworkReceiver
	{
		void Receive (int recHostId,
		              int connectionId,
		              int channelId,
		              byte[] recBuffer,
		              int bufferSize,
		              int dataSize,
		              byte error,
		              NetworkEventType recData);
	}
	
}