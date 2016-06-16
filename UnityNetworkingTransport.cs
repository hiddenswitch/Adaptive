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

	public class UnityNetworkingTransport : ITransport, INetworkReceiverHelper
	{
		protected GameObject m_NetworkReceiverGameObject;
		protected int m_ReliableChannelId;
		protected int m_UnreliableChannelId;
		protected int m_HostId;

		public int Port { get; set; }

		public byte LastError { get; private set; }

		public UnityNetworkingTransport (int players = 2, int port = 6002)
		{
			Port = port;

			// Create an object that handles network receive events from the transport layer
			if (m_NetworkReceiverGameObject == null) {
				m_NetworkReceiverGameObject = new GameObject ("Network Receiver");
				var receiver = m_NetworkReceiverGameObject.AddComponent<NetworkTransportReceiveHelper> ();
				receiver.transportDelegate = this;
			}

			// Initialize the network transport code
			var config = new GlobalConfig ();
			NetworkTransport.Init (config);

			// Create the reliable (state transfer) and unreliable (command transfer) layers
			var networkTransportConfig = new ConnectionConfig ();
			m_ReliableChannelId = networkTransportConfig.AddChannel (QosType.AllCostDelivery);
			m_UnreliableChannelId = networkTransportConfig.AddChannel (QosType.UnreliableFragmented);

			// Set up a networking topology. For now, only two player is supported.
			var topology = new HostTopology (networkTransportConfig, 2);

			// Start a host
			m_HostId = NetworkTransport.AddHost (topology, Port);
		}

		public event TransportReceiveHandler Received;

		public void Send (int destinationId, int channelId, byte[] buffer, int startIndex, int length, out byte error)
		{
			if (startIndex != 0) {
				throw new NotSupportedException ("UnityNetworkTransport cannot send from a buffer starting at a non-zero index.");
			}

			NetworkTransport.Send (m_HostId, destinationId, channelId, buffer, length, out error);
			LastError = error;
		}

		public ConnectionId Connect (string hostName)
		{
			byte error;
			var connectionId = NetworkTransport.Connect (m_HostId, hostName, Port, 0, out error);
			LastError = error;
			return connectionId;
		}

		void INetworkReceiverHelper.Receive (int recHostId, int connectionId, int channelId, byte[] recBuffer, int bufferSize, int dataSize, byte error, NetworkEventType recData)
		{
			if (Received != null) {
				LastError = error;
				Received (connectionId, channelId, recData, recBuffer, 0, dataSize, error);
			}
		}

		public int ReliableChannelId {
			get {
				return m_ReliableChannelId;
			}
		}

		public int UnreliableChannelId {
			get {
				return m_UnreliableChannelId;
			}
		}
	}
}