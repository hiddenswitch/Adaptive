using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using PeerId = System.Int32;
using ConnectionId = System.Int32;
using FrameIndex = System.Int32;

namespace HiddenSwitch.Multiplayer
{
	public class UdpTransport : ITransport, IDisposable
	{
		#region ITransport implementation

		public event TransportReceiveHandler Received;

		public void Send (int destinationId, int channelId, byte[] buffer, int startIndex, int length, out byte error)
		{
			var host = m_ips [destinationId];
			m_udpClient.Send (buffer, 0, host);
			error = 0;
		}

		public void Connect (string hostname, int port = -1)
		{
			if (port == -1) {
				port = Port;
			}
			m_udpClient.Send (new byte[0], 0, hostname, port);
		}

		public int ReliableChannelId {
			get {
				return 1;
			}
		}

		public int UnreliableChannelId {
			get {
				return 0;
			}
		}

		public int Port {
			get;
			protected set;
		}

		#endregion

		protected struct Frame
		{
			public byte[] Buffer;
			public int Length;
			public UnityEngine.Networking.NetworkEventType LastEventType;

		}

		protected object m_queueLock = new object ();
		protected Queue<Frame> m_queue = new Queue<Frame> ();

		public void Receive (byte[] buffer, out int length, out UnityEngine.Networking.NetworkEventType eventType)
		{
			lock (m_queueLock) {
				if (m_queue.Count == 0) {
					eventType = NetworkEventType.Nothing;
					buffer = new byte[0];
					length = 0;
				} else {
					var frame = m_queue.Dequeue ();
					length = frame.Length;
					eventType = frame.LastEventType;
					System.Buffer.BlockCopy (frame.Buffer, 0, buffer, 0, Mathf.Min (buffer.Length, frame.Length));
				}
			}
		}

		protected UdpClient m_udpClient;
		protected Thread m_udpThread;
		protected bool m_shouldDisconnect;
		protected Dictionary<IPEndPoint, int> m_connectionIds;
		protected Dictionary<int, IPEndPoint> m_ips;
		protected int m_nextConnectionId;


		public UdpTransport (bool useUnityThread = false, int queueSize = 16, int maxQueueSize = 34, int port = 12500)
		{
			Port = port;
			// Queue has to be at least 1 large
			queueSize = Mathf.Max (queueSize, 1);
			maxQueueSize = Mathf.Max (maxQueueSize, 4);
			m_nextConnectionId = 0;
			m_connectionIds = new Dictionary<IPEndPoint, int> ();
			m_ips = new Dictionary<int, IPEndPoint> ();

			if (useUnityThread) {
				
			}

			m_udpClient = new UdpClient (Port);
			m_udpThread = new System.Threading.Thread (new ThreadStart (delegate {
				while (true) {
					IPEndPoint remote = null;
					var buffer = m_udpClient.Receive (ref remote);

					if (remote == null) {
						continue;
					}

					var isFirstConnection = false;
					var connectionId = -1;
					if (!m_connectionIds.ContainsKey (remote)) {
						m_nextConnectionId++;
						connectionId = m_nextConnectionId;
						m_connectionIds [remote] = connectionId;
						m_ips [connectionId] = remote;
						isFirstConnection = true;
					}
					connectionId = m_connectionIds [remote];

					UnityEngine.Networking.NetworkEventType eventType = NetworkEventType.Nothing;

					if (isFirstConnection) {
						eventType = NetworkEventType.ConnectEvent;
					} else {
						eventType = NetworkEventType.DataEvent;
					}

					if (useUnityThread) {
						lock (m_queueLock) {
							var frame = new Frame () {
								LastEventType = eventType,
								Buffer = buffer,
								Length = buffer.Length
							};

							// \O(1) but not \Theta(1) time

							// If we have exceeded our queue size, toss K frames
							if (m_queue.Count > queueSize) {
								// Since later frames have redundant input information, we can toss input frames
								// TODO: We use Tcp for reliable messages
								// m_queue.Clear ();
								// In the meantime, inspect the frame and toss input frames if this is an input frame
								if (buffer.Length > 0
								    && buffer [0] == MessageType.Input) {
									// Kill earlier input messages
									while (m_queue.Count > 0) {
										var upNext = m_queue.Peek ();
										if (upNext.Length > 0
										    && upNext.Buffer [0] == MessageType.Input) {
											m_queue.Dequeue ();
										} else {
											// Clearly a connection or state event got in here...
											break;
										}
									}
								}

								if (buffer.Length > 0
								    && buffer [0] == MessageType.AcknowledgeInput) {
									// Kill earlier acknowledge input messages
									while (m_queue.Count > 0) {
										var upNext = m_queue.Peek ();
										if (upNext.Length > 0
										    && upNext.Buffer [0] == MessageType.AcknowledgeInput) {
											m_queue.Dequeue ();
										} else {
											// Clearly a connection or state event got in here...
											break;
										}
									}
								}
							}

							m_queue.Enqueue (frame);

							if (m_queue.Count > maxQueueSize) {
								// "Throw an exception" outside of the lock
								// TODO: Maybe this won't happen that often and memory won't leak.
							}
						}
					} else {
						if (Received != null) {
							Received (connectionId, UnreliableChannelId, eventType, buffer, 0, buffer.Length, 0);		
						}
					}

					if (m_shouldDisconnect) {
						break;
					}
				}
				m_udpClient.Close ();
			}));
		}

		#region IDisposable implementation


		public void Dispose ()
		{
			m_shouldDisconnect = true;
		}



		#endregion

		~UdpTransport ()
		{
			m_udpThread.Abort ();
			m_udpClient.Close ();
		}
	}

}