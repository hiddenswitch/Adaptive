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
	
	public interface ITransport
	{
		void Send (int destinationId, int channelId, byte[] buffer, int startIndex, int length, out byte error);

		int ReliableChannelId { get; }

		int UnreliableChannelId { get; }

		ConnectionId Connect (string hostname);

		event TransportReceiveHandler Received;
	}
	
}