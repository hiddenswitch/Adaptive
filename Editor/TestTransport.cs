using UnityEngine;
using System.Collections.Generic;
using NUnit.Framework;
using HiddenSwitch.Multiplayer;
using System;

namespace HiddenSwitch.Multiplayer.Tests
{

	public class TestTransport : ITransport
	{
		public static Dictionary<int, TestTransport> transports = new Dictionary<int, TestTransport> ();
		public static Dictionary<string, TestTransport> transportsByHostname = new Dictionary<string, TestTransport> ();
		public int ThisConnectionId;
		public string Hostname;
		public static System.Random random = new System.Random ();
		public List<int> Connections = new List<int> ();
		public bool Logging = false;
		protected bool m_FailNextSend = false;

		public int Port {
			get {
				return 2;
			}
		}

		public TestTransport (string hostname)
		{
			ThisConnectionId = random.Next ();
			Hostname = hostname;
			transports [ThisConnectionId] = this;
			transportsByHostname [hostname] = this;
		}

		#region ITransport implementation

		public event TransportReceiveHandler Received;

		public void FailNextSend ()
		{
			m_FailNextSend = true;
		}

		public void Send (int destinationId, int channelId, byte[] buffer, int startIndex, int length, out byte error)
		{
			if (Logging) {
				System.Console.WriteLine ("S {0}: {1} length {2}", Hostname, buffer.Length > 0 ? buffer [0] : -1, length);
			}
			if (!transports.ContainsKey (destinationId)) {
				error = 1;
				return;
			}

			if (channelId != this.ReliableChannelId
			    && channelId != this.UnreliableChannelId) {
				error = 1;
				return;
			}

			if (buffer == null) {
				throw new Exception ();
			}

			if (length < 0) {
				throw new Exception ();
			}

			if (m_FailNextSend) {
				error = 0;
				m_FailNextSend = false;
				return;
			}

			var otherTransport = transports [destinationId];
			if (!otherTransport.Connections.Contains (ThisConnectionId)) {
				otherTransport.ReliableChannelId = 100;
				otherTransport.UnreliableChannelId = -200;
				otherTransport.Connections.Add (ThisConnectionId);
				otherTransport.RaiseReceived (ThisConnectionId, ReliableChannelId, UnityEngine.Networking.NetworkEventType.ConnectEvent, new byte[] { }, 0, 0, out error);
			}
			otherTransport.RaiseReceived (ThisConnectionId, ReliableChannelId, UnityEngine.Networking.NetworkEventType.DataEvent, buffer, startIndex, length, out error);
		}

		public void RaiseReceived (int connectionId, int channelId, UnityEngine.Networking.NetworkEventType eventType, byte[] buffer, int startIndex, int length, out byte error)
		{
			error = 0;
			if (Logging) {
				System.Console.WriteLine ("--> R {0}: {1} length {2}", Hostname, buffer.Length > 0 ? buffer [0] : -1, length);
			}
			if (Received != null) {
				Received (connectionId, channelId, eventType, buffer, startIndex, length, error);
			}
		}

		public void Connect (string hostname, int port)
		{
			ReliableChannelId = 100;
			UnreliableChannelId = -200;

			var otherTransport = transportsByHostname [hostname];
			byte error;
			Connections.Add (otherTransport.ThisConnectionId);
			RaiseReceived (otherTransport.ThisConnectionId, ReliableChannelId, UnityEngine.Networking.NetworkEventType.ConnectEvent, new byte[] { }, 0, 0, out error);
		}

		public int ReliableChannelId {
			get;
			protected set;
		}

		public int UnreliableChannelId {
			get;
			protected set;
		}

		#endregion
	}
}
