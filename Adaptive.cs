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
	/// The Adaptive networking class.
	/// </summary>
	public class Adaptive<TState> : INetworkReceiver
		where TState : State, new()
	{
		/// <summary>
		/// How many frames should be buffered before executing the commands that occurred during those frames?
		/// </summary>
		public int PlayoutDelayFrameCount = 10;
		public int Port = 6002;

		/// <summary>
		/// Get a public hostname that can be used to connect to this peer.
		/// If you are using a relay service, this still returns a valid connectable peer.
		/// </summary>
		/// <value>The hostname.</value>
		public string Hostname {
			get {
				throw new NotImplementedException ();
			}
		}

		protected int m_ReliableChannelId;
		protected int m_UnreliableChannelId;
		protected int m_HostId;
		protected int m_myPeerId;

		public PeerId MyPeerId {
			get {
				return m_myPeerId;
			}
		}

		protected static GameObject m_NetworkReceiverGameObject;

		protected Dictionary<ConnectionId, Peer> m_peers = new Dictionary<int, Peer> ();
		protected Dictionary<byte, Action<TState, BinaryReader>> m_commandHandlers = new Dictionary<byte, Action<TState, BinaryReader>> ();
		protected Dictionary<FrameIndex, Frame> m_frames = new Dictionary<FrameIndex, Frame> ();
		protected Dictionary<PeerId, Queue<UnacknowledgedCommands>> m_unacknowledgedCommandQueue = new Dictionary<PeerId, Queue<UnacknowledgedCommands>> ();
		protected TState m_latestState;
		protected int m_latestStateFrameIndex = 0;
		/// <summary>
		/// A reusable command send buffer.
		/// </summary>
		protected byte[] m_sendCommandBuffer = new byte[32 * 1024];
		/// <summary>
		/// A reusable send state buffer
		/// </summary>
		protected byte[] m_sendStateBuffer = new byte[32 * 1024];

		public Adaptive (PeerId? peerId = null)
		{
			// Setup a peer ID for myself. Just a random value for now
			m_myPeerId = peerId.HasValue ? peerId.GetValueOrDefault () : UnityEngine.Random.Range (1, int.MaxValue);

			// Create an object that handles network receive events from the transport layer
			if (m_NetworkReceiverGameObject == null) {
				m_NetworkReceiverGameObject = new GameObject ("Network Receiver");
				var receiver = m_NetworkReceiverGameObject.AddComponent<NetworkTransportReceiveHelper> ();
				receiver.adaptiveDelegate = this;
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

		/// <summary>
		/// Connect to another peer running Adaptive
		/// </summary>
		/// <returns>The peer.</returns>
		/// <param name="hostName">Host name.</param>
		/// <param name="port">Port.</param>
		public Peer AddPeer (string hostName, int port)
		{
			byte error;
			var connectionId = NetworkTransport.Connect (m_HostId, hostName, port, 0, out error);

			// TODO: Handle error
			var peer = new Peer (connectionId);
			// Prepare a peer record
			m_peers.Add (connectionId, peer);
			return peer;
		}

		/// <summary>
		/// Adds a handler for a given command.
		/// 
		/// The handler is given two arguments. The first argument to your handler is an instance of TCommand. This
		/// TCommand type subclasses Command. It should contain fields that correspond to the arguments of the logical
		/// parts of your handler. The second argument is an instance of TState. TState is a type that subclasses
		/// State and contains a logical representation of your game.
		/// </summary>
		/// <returns>A callable ID.</returns>
		/// <param name="id">The ID of this command. Used in the SendCommand method.</param> 
		/// <param name="handler">Handler for this command. Used by the simulator logic to execute the command at the right time.</param>
		public void AddCommandHandler<TCommandArguments> (byte id, Action<TCommandArguments, TState> handler)
			where TCommandArguments : CommandArguments, new()
		{
			// Wrap the command handler
			m_commandHandlers.Add (id, (TState state, BinaryReader reader) => {
				var command = new TCommandArguments ();
				command.Deserialize (reader);
				// We probably don't need to do anything to compare the state before and after
				handler.Invoke (command, state);
			});
		}

		/// <summary>
		/// Calls a command with the given id and arguments. This command is sent with the current rendering frame as its time,
		/// not the latest frame (which may be possible to render, but isn't rendered yet).
		/// </summary>
		/// <param name="id">Identifier.</param>
		/// <param name="command">Command.</param>
		/// <typeparam name="TCommand">The 1st type parameter.</typeparam>
		public void CallCommand<TCommandArguments> (byte id, TCommandArguments command)
			where TCommandArguments : CommandArguments
		{
			
		}

		/// <summary>
		/// Receives network data. Supports simulation.
		/// </summary>
		/// <param name="recHostId">Rec host identifier.</param>
		/// <param name="connectionId">Connection identifier.</param>
		/// <param name="channelId">Channel identifier.</param>
		/// <param name="recBuffer">Rec buffer.</param>
		/// <param name="bufferSize">Buffer size.</param>
		/// <param name="dataSize">Data size.</param>
		/// <param name="error">Error.</param>
		/// <param name="recData">Rec data.</param>
		public void Receive (int recHostId,
		                     int connectionId,
		                     int channelId,
		                     byte[] recBuffer,
		                     int bufferSize,
		                     int dataSize,
		                     byte error,
		                     NetworkEventType recData)
		{
			switch (recData) {
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
				NetworkTransport.Send (m_HostId, connectionId, m_ReliableChannelId, peerInfoBuffer, 5, out peerIdMessageError);
				break;
			case NetworkEventType.DataEvent:
				// TODO: Handle unusually short frames
				// Check the message type byte
				var binaryReader = new BinaryReader (new MemoryStream (recBuffer, 0, dataSize, false));
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
								m_frames [thisFrameIndex] = new Frame ();
								m_frames [thisFrameIndex].frameIndex = thisFrameIndex;
							}

							// Save these commands as belonging to the given peerId
							m_frames [thisFrameIndex].commands [peerId] = new CommandList () {
								// Store the index of where the data starts because we might be sharing
								// this binary reader with other command lists
								dataStartIndex = binaryReader.BaseStream.Position,
								// Store the length of the command list for this frame
								dataLength = commandListLength,
								// A pointer to the binary reader
								data = binaryReader,
								// The commands
								commandIds = commandIds,
								frameIndex = thisFrameIndex
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
					// If this value is greater than my current state, process it
					if (stateFrameIndex > m_latestStateFrameIndex) {
						// Process the state
						TState state = new TState ();
						state.Deserialize (binaryReader);
						m_latestState = state;
						m_latestStateFrameIndex = stateFrameIndex;	
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
				break;
			}
		}

		/// <summary>
		/// Executes the given commands against the given state. Modifies the state in place.
		/// </summary>
		/// <param name="state">State.</param>
		/// <param name="commands">Commands.</param>
		internal void ExecuteCommands (TState state, CommandList commands)
		{
			if (commands == null
				|| commands.commandIds == null
			    || commands.commandIds.Length == 0) {
				// This is an empty command list. Skip it.
				return;
			}

			// Seek to the appropriate place in the stream, since we may be processing multiple command lists
			commands.data.BaseStream.Seek (commands.dataStartIndex, SeekOrigin.Begin);

			for (var i = 0; i < commands.commandIds.Length; i++) {
				// Assert that the byte matches the command byte
				var commandId = commands.commandIds [i];
				if (commandId != commands.data.ReadByte ()) {
					// TODO: Throw an exception
				}

				// Execute based on the handler
				var handler = m_commandHandlers [commandId];
				handler.Invoke (state, commands.data);
				// Assert that if there is another command left, that the subsequent byte
				// matches the byte that is in the commands list for this frame
				if (i + 1 < commands.commandIds.Length
				    && (byte)commands.data.PeekChar () != commands.commandIds [i]) {
					// TODO: Throw an exception
				}
			}
		}

		/// <summary>
		/// Sends all the hereto unacknowledged commands to the given peer ID. Or, if there are no
		/// unacknowledged commands or just an empty command list, send the empty message.
		/// </summary>
		/// <param name="peerId">Peer identifier.</param>
		internal void SendCommands (PeerId peerId)
		{
			var memoryStream = new MemoryStream (m_sendCommandBuffer);
			var binaryWriter = new BinaryWriter (memoryStream);

			var unacknowledgedQueuedCommands = m_unacknowledgedCommandQueue [peerId];

			if (unacknowledgedQueuedCommands.Count == 0) {
				binaryWriter.Write (MessageType.EmptyCommands);
			} else {
				binaryWriter.Write (MessageType.Commands);
			}

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

			// Send the packet
			// Find the connectionId for this peer
			var connectionId = 0;
			foreach (var peer in m_peers) {
				if (peer.Value.Id == peerId) {
					connectionId = peer.Key;
				}
			}

			byte sendError;
			NetworkTransport.Send (m_HostId, connectionId, m_UnreliableChannelId, m_sendCommandBuffer, (int)binaryWriter.BaseStream.Position, out sendError);
		}

		/// <summary>
		/// Send the latest state recorded in this Adaptive instance.
		/// </summary>
		/// <param name="connectionId">Connection identifier.</param>
		internal void SendState (int connectionId)
		{
			var binaryWriter = new BinaryWriter (new MemoryStream (m_sendStateBuffer));
			// Write the message type.
			binaryWriter.Write (MessageType.State);
			// Write the frame index of this state
			binaryWriter.Write (m_latestStateFrameIndex);
			// Now serialize the state
			m_latestState.Serialize (binaryWriter);
			// Send the state
			byte sendError;
			NetworkTransport.Send (m_HostId, connectionId, m_ReliableChannelId, m_sendStateBuffer, (int)binaryWriter.BaseStream.Position, out sendError);
		}

		/// <summary>
		/// Send an acknowledgement for the given frame.
		/// </summary>
		/// <param name="frameIndex">Frame index.</param>
		/// <param name="connectionId">Connection identifier.</param>
		internal void AcknowledgeCommands (int frameIndex, int connectionId)
		{
			// A five byte buffer. The first byte is the message type of acknowledge, the next
			// four bytes are the frame index we are acknowledging.

			var acknowledgeBuffer = new byte[5];
			acknowledgeBuffer [0] = MessageType.AcknowledgeCommands;
			var frameIndexBytes = BitConverter.GetBytes (frameIndex);
			frameIndexBytes.CopyTo (acknowledgeBuffer, 1);
			byte acknowledgeError;
			NetworkTransport.Send (m_HostId, connectionId, m_UnreliableChannelId, acknowledgeBuffer, 5, out acknowledgeError);
		}

		/// <summary>
		/// Send an acknowledgement of state.
		/// </summary>
		/// <param name="connectionId">Connection identifier.</param>
		internal void AcknowledgeState (int connectionId)
		{
			var acknowledgeBuffer = new byte[1] { MessageType.AcknowledgeState };

			byte acknowledgeError;
			NetworkTransport.Send (m_HostId, connectionId, m_ReliableChannelId, acknowledgeBuffer, 1, out acknowledgeError);
		}
	}


}