using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace HiddenSwitch.Multiplayer
{

	/// <summary>
	/// A script that handles data from the Unity network transport layer
	/// </summary>
	internal sealed class NetworkTransportReceiveHelper : MonoBehaviour
	{
		internal INetworkReceiverHelper transportDelegate { get; set; }

		internal byte[] workingBuffer = new byte[32 * 1024];

		void Update ()
		{
			int recHostId;
			int connectionId; 
			int channelId; 
			int dataSize;
			byte error;
			NetworkEventType recData = NetworkEventType.Nothing;
			do {
				recData = NetworkTransport.Receive (out recHostId, out connectionId, out channelId, workingBuffer, workingBuffer.Length, out dataSize, out error);
				if (recData == NetworkEventType.Nothing) {
					break;
				}
				// Don't create buffers 
				byte[] recBuffer = new byte[dataSize];

				if (dataSize > 0) {
					Buffer.BlockCopy (workingBuffer, 0, recBuffer, 0, dataSize);
				}

				transportDelegate.Receive (recHostId, connectionId, channelId, recBuffer, dataSize, dataSize, error, recData);
			} while (recData != NetworkEventType.Nothing);
		}

		void Awake ()
		{
			DontDestroyOnLoad (this.gameObject);
		}
	}
	
}