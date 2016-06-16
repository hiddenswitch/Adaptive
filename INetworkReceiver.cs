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
	public delegate void TransportReceiveHandler (int connectionId, int channelId, NetworkEventType eventType, byte[] buffer, int startIndex, int length, byte error);

	/// <summary>
	/// A helper interface for network data receivers.
	/// </summary>
	internal interface INetworkReceiverHelper
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