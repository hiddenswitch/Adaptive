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
	/// <summary>
	/// Send and receive messages over a network
	/// </summary>
	public class Network<TState> : IClock
		where TState : State, new()
	{
		protected System.Random m_random = new System.Random ();

		/// <summary>
		/// Get a public hostname that can be used to connect to this peer.
		/// If you are using a relay service, this still returns a valid connectable peer.
		/// </summary>
		/// <value>The hostname.</value>
		public string HostName {
			get {
				throw new NotImplementedException ();
			}
		}

		protected bool m_allHaveState;


		protected int m_myPeerId;

		public PeerId MyPeerId {
			get {
				return m_myPeerId;
			}
		}

		public ITransport Transport {
			get;
			private set;
		}

		public bool TickAllAcknowledgedFrames { get; set; }

		private IClock m_clock;

		public IClock Clock {
			get {
				return m_clock;
			}
			set {
				if (m_clock != null) {
					m_clock.Tick -= OnNetworkClockTick;
				}

				m_clock = value;
				m_clock.Tick += OnNetworkClockTick;
			}
		}

		protected FrameIndex m_stateStartFrame;
		protected Dictionary<ConnectionId, Peer> m_peers = new Dictionary<int, Peer> ();
		protected Dictionary<FrameIndex, NetworkFrame> m_frames = new Dictionary<FrameIndex, NetworkFrame> ();
		protected Dictionary<PeerId, Queue<UnacknowledgedCommands>> m_unacknowledgedCommandQueue = new Dictionary<PeerId, Queue<UnacknowledgedCommands>> ();
		protected TState m_latestState;

		/// <summary>
		/// The latest state initialized in the constructor or received over the network from the other party.
		/// </summary>
		/// <value>The state of the latest.</value>
		public TState LatestState {
			get {
				return m_latestState;
			}
		}

		protected int m_latestStateFrameIndex = 0;
		protected int m_latestAcknowledgedFrame;
		/// <summary>
		/// A reusable command send buffer.
		/// </summary>
		protected byte[] m_sendCommandBuffer = new byte[32 * 1024];
		/// <summary>
		/// A reusable state send buffer
		/// </summary>
		protected byte[] m_sendStateBuffer = new byte[32 * 1024];

		public Network (IClock clock, PeerId? peerId = null, ITransport transport = null, TState startState = null)
		{
			// Setup a peer ID for myself. Just a random value for now
			m_myPeerId = peerId.HasValue ? peerId.GetValueOrDefault () : m_random.Next ();

			// Configure a transport if one isn't specified
			Transport = transport ?? new UnityNetworkingTransport ();

			Transport.Received += HandleTransportReceive;
			m_latestState = startState;
			Clock = clock;
		}

		/// <summary>
		/// Handles messages from the network. Supports simulation
		/// </summary>
		/// <param name="connectionId">Connection identifier.</param>
		/// <param name="channelId">Channel identifier.</param>
		/// <param name="eventType">Event type.</param>
		/// <param name="buffer">Buffer.</param>
		/// <param name="startIndex">Start index.</param>
		/// <param name="length">Length.</param>
		/// <param name="error">Error.</param>
		void HandleTransportReceive (int connectionId, int channelId, NetworkEventType eventType, byte[] buffer, int startIndex, int length, byte error)
		{
			switch (eventType) {
			case NetworkEventType.Nothing:
				break;
			case NetworkEventType.ConnectEvent:
				// Handle the connection
				// Send peer info to the connector
				// Currently only makes sense for 2 player
				var peerInfoBuffer = new byte[5];
				// First byte is the peer info command
				peerInfoBuffer [0] = MessageType.PeerInfo;
				// Next four bytes are the peer ID
				var peerIdBytes = BitConverter.GetBytes (MyPeerId);
				peerIdBytes.CopyTo (peerInfoBuffer, 1);
				byte peerIdMessageError;
				Transport.Send (connectionId, Transport.ReliableChannelId, peerInfoBuffer, 0, 5, out peerIdMessageError);
				break;
			case NetworkEventType.DataEvent:
				// TODO: Handle unusually short frames
				// Check the message type byte
				var binaryReader = new BinaryReader (new MemoryStream (buffer, 0, length, false));
				var messageType = binaryReader.ReadByte ();
				PeerId peerId;
				switch (messageType) {
				case MessageType.AcknowledgeCommands:
					// Clear out my unacknowledged command lists
					peerId = m_peers [connectionId].Id.GetValueOrDefault ();
					var acknowledgedFrameIndex = binaryReader.ReadInt32 ();
					var stack = m_unacknowledgedCommandQueue [peerId];
					while (stack.Peek ().frameIndex <= acknowledgedFrameIndex) {
						stack.Dequeue ();
					}

					var currentLatestAcknowledgedFrame = m_latestAcknowledgedFrame;
					m_latestAcknowledgedFrame = acknowledgedFrameIndex;

					// Tick, now that our commands have been acknowledged
					if (Tick != null) {
						if (TickAllAcknowledgedFrames) {
							for (var i = currentLatestAcknowledgedFrame + 1; i <= m_latestAcknowledgedFrame; i++) {
								Tick (i);
							}
						} else {
							Tick (m_latestAcknowledgedFrame);
						}
					}
					break;
				case MessageType.Commands:
					peerId = m_peers [connectionId].Id.GetValueOrDefault ();
					int frameIndexToAcknowledge = Int32.MinValue;
					while (true) {
						// Add these commands to the frame
						// First int is the frame index
						var thisFrameIndex = binaryReader.ReadInt32 ();
						// Next byte is the number of commands
						var numCommands = binaryReader.ReadByte ();

						// If the number of commands is not zero, this frame has content
						if (numCommands != 0) {
							// Next bytes are the commands that are specified in this command list
							var commandIds = binaryReader.ReadBytes (numCommands);
							// Next ushort is the size of this command list. Limited to 65k
							var commandListLength = binaryReader.ReadUInt16 ();
							// Now the data reader is advanced to the command ID of the first command.
							// We're ready to save the frame.
							// Check if we have a frame already from this peer

							var framesForPeer = m_frames [peerId];
							if (!m_frames.ContainsKey (thisFrameIndex)) {
								m_frames [thisFrameIndex] = new NetworkFrame ();
								m_frames [thisFrameIndex].frameIndex = thisFrameIndex;
							}

							// Save these commands as belonging to the given peerId
							m_frames [thisFrameIndex].commands [peerId] = new SerializedCommandList () {
								// Store the index of where the data starts because we might be sharing
								// this binary reader with other command lists
								dataStartIndex = binaryReader.BaseStream.Position,
								// Store the length of the command list for this frame
								dataLength = commandListLength,
								// A pointer to the binary reader
								data = binaryReader,
								// The commands
								commandIds = commandIds,
								frameIndex = thisFrameIndex,
								peerId = peerId
							};

							// We will acknowl(int)edge the largest frame index
							frameIndexToAcknowledge = Mathf.Max (frameIndexToAcknowledge, thisFrameIndex);

							// Seek to the command list length
							binaryReader.BaseStream.Seek ((long)commandListLength, SeekOrigin.Current);
						}

						// If there is still data left, we are processing more lists of missing commands
						// Otherwise, break
						var hasNoData = binaryReader.PeekChar () == -1;
						if (hasNoData) {
							break;
						}
					}

					// Reply with an acknowledge for the commands
					AcknowledgeCommands (frameIndexToAcknowledge, connectionId);
					break;
				case MessageType.EmptyCommands:
					// We have received no commands for this frame. Just acknowledge.
					// Read the frame index we are acknowledging.
					var frameIndexOfEmpty = binaryReader.ReadInt32 ();
					// Acknowledging...
					AcknowledgeCommands (frameIndexOfEmpty, connectionId);
					break;
				case MessageType.State:
					// TODO: Load in the entire game state
					// First value is the frame of this state
					var stateFrameIndex = binaryReader.ReadInt32 ();
					// Are we receiving a null state?
					var hasState = binaryReader.ReadBoolean ();
					// If this value is greater than my current state or if I am a peer with no state,
					// use the delivered state
					if ((stateFrameIndex > m_latestStateFrameIndex
					    || m_latestState == null)
					    && hasState) {
						// Process the state
						TState state = new TState ();
						state.Deserialize (binaryReader);
						m_latestState = state;
						m_latestStateFrameIndex = stateFrameIndex;
						m_stateStartFrame = stateFrameIndex;
					}

					// Acknowledge receipt of the state
					AcknowledgeState (connectionId);

					break;
				case MessageType.AcknowledgeState:
					// Mark the peer as having received the state.
					m_peers [connectionId].HasState = true;
					// If all the peers have the latest state, we can start the execution timer
					var allHaveState = true;
					foreach (var peer in m_peers) {
						if (!peer.Value.HasState) {
							allHaveState = false;
							break;
						}
					}

					if (allHaveState) {
						AllHaveState = true;
						// TODO: Set the game as ready to start executing
					}
					break;
				case MessageType.PeerInfo:
					// Read in the peer information. This allows people to reconnect after being disconnected
					// and get treated as the same player.
					peerId = binaryReader.ReadInt32 ();
					// Do we have an existing entry in our peers table?
					ConnectionId? existingConnectionId = null;
					foreach (var kv in m_peers) {
						if (kv.Value.Id == peerId) {
							existingConnectionId = kv.Key;
							break;
						}
					}
					// Migrate all the prior information we have about this peer to the new peer data if
					// we found an existing peer ID
					if (existingConnectionId.HasValue) {
						lock (m_peers) {
							m_peers.Remove (existingConnectionId.GetValueOrDefault ());
							m_peers.Add (connectionId, new Peer (connectionId));
						}
					} else {
						m_peers [connectionId] = new Peer (connectionId);
						m_unacknowledgedCommandQueue.Add (peerId, new Queue<UnacknowledgedCommands> (30 * 16));
					}

					m_peers [connectionId].Id = peerId;

					// Now that I have peer info, send my latest copy of the state
					SendState (connectionId);
					break;
				}
				break;
			case NetworkEventType.DisconnectEvent:
				// TODO: Mark peer as disconnected
				break;
			}
		}


		/// <summary>
		/// Connect to another peer running Adaptive
		/// </summary>
		/// <returns>The peer.</returns>
		/// <param name="hostName">Host name.</param>
		/// <param name="port">Port.</param>
		public Peer AddPeer (string hostName, int port)
		{
			var connectionId = Transport.Connect (hostName);
			if (m_peers.ContainsKey (connectionId)) {
				return m_peers [connectionId];
			}

			// TODO: Handle error
			var peer = new Peer (connectionId);
			// Prepare a peer record
			m_peers.Add (connectionId, peer);
			return peer;
		}

		/// <summary>
		/// Queues a command to send over the network at the appropriate time.
		/// 
		/// It is the responsibility of the caller to not let the game queue commands wildly out into the future.
		/// </summary>
		/// <param name="commandId">Command identifier.</param>
		/// <param name="arguments">Arguments.</param>
		/// <param name="frameIndex">Frame index. Defaults to the tickrate clock's frameIndex</param>
		/// <exception cref="HiddenSwitch.Networking.LateCommandException">Thrown if you are trying to queue a command for a frame that
		/// is not the latest unacknowledged frame for a given peer.</exception>
		public void QueueCommand (byte commandId, CommandArguments arguments, int frameIndex = int.MinValue)
		{
			if (frameIndex == int.MinValue) {
				frameIndex = ElapsedFrameCount;
			}
			// If this command is coming late, throw an exception
			if (frameIndex < ElapsedFrameCount) {
				throw new LateCommandException () {
					CommandId = commandId,
					Arguments = arguments,
					FrameIndex = frameIndex
				};
			}

			// Queue commands for the other peers
			var command = new CommandWithArguments () {
				arguments = arguments,
				commandId = commandId
			};

			foreach (var peer in m_peers) {
				if (!peer.Value.Id.HasValue) {
					// TODO: Handle peers which aren't connected yet.
					continue;
				}
				var peerId = peer.Value.Id.GetValueOrDefault ();
				// Should we enqueue on this frame? First check if there is anything in the queue
				if (m_unacknowledgedCommandQueue [peerId].Count > 0) {
					// Look at the latest item on the queue
					var latestQueue = m_unacknowledgedCommandQueue [peerId].Peek ();
					// If we are queuing the current frame still, make sure this command gets serialized into this frame's commands list
					if (latestQueue.frameIndex == frameIndex) {
						latestQueue.queuedCommands.Add (command);
						// We have enqueued the command in the appropriate place, we can move onto the next peer
						continue;
					} else if (latestQueue.frameIndex > frameIndex) {
						
					}
					// Otherwise, we're queueing something newer, so we will just stick it on the queue.
				}

				m_unacknowledgedCommandQueue [peerId].Enqueue (new UnacknowledgedCommands () { 
					frameIndex = frameIndex,
					queuedCommands = new List<CommandWithArguments> (new CommandWithArguments[] { command })
				});
			}
			// The commands themselves get sent in network clock tick's event
		}

		/// <summary>
		/// Sends all the hereto unacknowledged commands to the given peer ID. Or, if there are no
		/// unacknowledged commands or just an empty command list, send the empty message.
		/// </summary>
		/// <param name="peerId">Peer identifier.</param>
		internal void SendCommands (PeerId peerId)
		{
			// TODO: When we're sending commands to multiple peers, cache this work
			var memoryStream = new MemoryStream (m_sendCommandBuffer);
			var binaryWriter = new BinaryWriter (memoryStream);

			// Send the packet
			// Find the connectionId for this peer
			var connectionId = 0;
			foreach (var peer in m_peers) {
				if (peer.Value.Id == peerId) {
					connectionId = peer.Key;
				}
			}

			byte sendError;

			// If there are no unacknowledged frames
			var unacknowledgedQueuedCommands = m_unacknowledgedCommandQueue [peerId];

			// If there are no unacknowledged commands, send empty commands with the tickrate clock's frame index
			if (unacknowledgedQueuedCommands.Count == 0) {
				binaryWriter.Write (MessageType.EmptyCommands);
				binaryWriter.Write (Clock.ElapsedFrameCount);
				Transport.Send (connectionId, Transport.UnreliableChannelId, m_sendCommandBuffer, 0, (int)binaryWriter.BaseStream.Position, out sendError);
				return;
			}

			binaryWriter.Write (MessageType.Commands);

			foreach (var queuedCommands in unacknowledgedQueuedCommands) {
				// Write the frame index
				binaryWriter.Write (queuedCommands.frameIndex);
				// Write the number of commands
				var commandCount = queuedCommands.queuedCommands.Count;
				binaryWriter.Write ((byte)commandCount);

				// If this is an empty frame, continue
				if (commandCount == 0) {
					continue;
				}

				// Write the command IDs
				var commandIds = new byte[commandCount];
				for (var i = 0; i < commandCount; i++) {
					commandIds [i] = queuedCommands.queuedCommands [i].commandId;
				}
				binaryWriter.Write (commandIds);
				// We're going to save the position, serialize, and then restore the position and write
				// the length of the command list later
				var commandListLengthPosition = binaryWriter.BaseStream.Position;
				binaryWriter.Write ((ushort)0);
				// Write the commands
				foreach (var queuedCommand in queuedCommands.queuedCommands) {
					binaryWriter.Write (queuedCommand.commandId);
					queuedCommand.arguments.Serialize (binaryWriter);
				}
				// Compute the length of the command list
				var lastPosition = binaryWriter.BaseStream.Position;
				var commandListLength = lastPosition - commandListLengthPosition;
				binaryWriter.Seek ((int)commandListLengthPosition, SeekOrigin.Begin);
				// Write the number of bytes in this command list
				binaryWriter.Write ((ushort)commandListLength);
				// Seek back to the end of this buffer
				binaryWriter.Seek ((int)lastPosition, SeekOrigin.Begin);
			}

			Transport.Send (connectionId, Transport.UnreliableChannelId, m_sendCommandBuffer, 0, (int)binaryWriter.BaseStream.Position, out sendError);
		}

		/// <summary>
		/// Send the latest state recorded in this Adaptive instance.
		/// </summary>
		/// <param name="connectionId">Connection identifier.</param>
		protected void SendState (int connectionId)
		{
			var binaryWriter = new BinaryWriter (new MemoryStream (m_sendStateBuffer));
			// Write the message type.
			binaryWriter.Write (MessageType.State);
			// Write the frame index of this state
			binaryWriter.Write (m_latestStateFrameIndex);
			// Write whether or not the state is null
			binaryWriter.Write (m_latestState != null);
			// Now serialize the state
			if (m_latestState != null) {
				m_latestState.Serialize (binaryWriter);
			}
			// Send the state
			byte sendError;
			Transport.Send (connectionId, Transport.ReliableChannelId, m_sendStateBuffer, 0, (int)binaryWriter.BaseStream.Position, out sendError);
		}

		/// <summary>
		/// Send an acknowledgement for the given frame.
		/// </summary>
		/// <param name="frameIndex">Frame index.</param>
		/// <param name="connectionId">Connection identifier.</param>
		protected void AcknowledgeCommands (int frameIndex, int connectionId)
		{
			// A five byte buffer. The first byte is the message type of acknowledge, the next
			// four bytes are the frame index we are acknowledging.
			var acknowledgeBuffer = new byte[5];
			acknowledgeBuffer [0] = MessageType.AcknowledgeCommands;
			var frameIndexBytes = BitConverter.GetBytes (frameIndex);
			frameIndexBytes.CopyTo (acknowledgeBuffer, 1);
			byte acknowledgeError;
			Transport.Send (connectionId, Transport.UnreliableChannelId, acknowledgeBuffer, 0, 5, out acknowledgeError);
		}

		/// <summary>
		/// Send an acknowledgement of state.
		/// </summary>
		/// <param name="connectionId">Connection identifier.</param>
		protected void AcknowledgeState (int connectionId)
		{
			var acknowledgeBuffer = new byte[1] { MessageType.AcknowledgeState };

			byte acknowledgeError;
			Transport.Send (connectionId, Transport.ReliableChannelId, acknowledgeBuffer, 0, 1, out acknowledgeError);
		}

		/// <summary>
		/// When the network clock ticks, try to send commands.
		/// </summary>
		/// <param name="frameIndex">Frame index.</param>
		protected void OnNetworkClockTick (int frameIndex)
		{
			// Send my commands to all my peers
			foreach (var peer in m_peers) {
				SendCommands (peer.Key);
			}
		}

		/// <summary>
		/// Networking ticks with the frame index of the latest frame acknowledged by other peers. Currently
		/// only supports two player. If the peer acknowledges a frame that isn't 1 away from the current latest acknowledge
		/// frame, set <see cref="HiddenSwitch.Network`1.TickAllAcknowledgedFrames"/> to true if you would like a tick
		/// for all the intermediate frames.
		/// </summary>
		public event Action<int> Tick;

		/// <summary>
		/// The latest acknowledged frame from the other peers.
		/// </summary>
		/// <value>The current frame.</value>
		public int ElapsedFrameCount {
			get {
				return LatestAcknowledgedFrame;
			}
		}

		/// <summary>
		/// The latest acknowledged frame from the other peers.
		/// </summary>
		/// <value>The current frame.</value>
		public int LatestAcknowledgedFrame {
			get {
				return m_latestAcknowledgedFrame;
			}
		}

		/// <summary>
		/// The first frame we synchronized to (the state frame typically).
		/// </summary>
		/// <value>The start frame.</value>
		public int StartFrame {
			get {
				return StateStartFrame;
			}
		}

		public int StateStartFrame {
			get {
				return m_stateStartFrame;
			}
		}


		/// <summary>
		/// Raised when all the peers have the current state.
		/// </summary>
		public event Action StateSynchronized;

		/// <summary>
		/// Do all the peers have valid state?
		/// </summary>
		/// <value><c>true</c> if all have state; otherwise, <c>false</c>.</value>
		public bool AllHaveState {
			get {
				return m_allHaveState;
			}
			protected set {
				if (m_allHaveState == value) {
					return;
				}

				m_allHaveState = value;

				if (StateSynchronized != null) {
					StateSynchronized ();
				}
			}
		}
	}
}