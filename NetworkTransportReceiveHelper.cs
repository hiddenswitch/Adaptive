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
	internal class NetworkTransportReceiveHelper : MonoBehaviour
	{
		internal INetworkReceiver adaptiveDelegate { get; set; }

		void Update ()
		{
			int recHostId; 
			int connectionId; 
			int channelId; 
			byte[] recBuffer = new byte[1024]; 
			int bufferSize = 1024;
			int dataSize;
			byte error;
			NetworkEventType recData = NetworkTransport.Receive (out recHostId, out connectionId, out channelId, recBuffer, bufferSize, out dataSize, out error);
			adaptiveDelegate.Receive (recHostId, connectionId, channelId, recBuffer, bufferSize, dataSize, error, recData);
		}

		void Awake ()
		{
			DontDestroyOnLoad (this.gameObject);
		}
	}
	
}