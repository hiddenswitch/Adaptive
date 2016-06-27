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
	public class Network<TState, TInput>
		where TState : State, new()
		where TInput : Input, new()
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

		protected ITransport m_transport;

		public ITransport Transport {
			get {
				return m_transport;
			}
			set {
				if (m_transport != null) {
					m_transport.Received -= HandleTransportReceive;
				}

				m_transport = value;
				m_transport.Received += HandleTransportReceive;
			}
		}

		public bool TickAllAcknowledgedFrames { get; set; }

		private IClock m_clock;

		public IClock Clock {
			get {
				return m_clock;
			}
			set {
				if (m_clock != null) {
					m_clock.LateTick -= OnNetworkClockLateTick;
				}

				m_clock = value;
				m_clock.LateTick += OnNetworkClockLateTick;
			}
		}

		protected FrameIndex m_stateStartFrame;
		protected Dictionary<ConnectionId, Peer> m_peers = new Dictionary<ConnectionId, Peer> ();
		protected Dictionary<FrameIndex, NetworkFrame> m_frames = new Dictionary<FrameIndex, NetworkFrame> ();
		protected TState m_latestState;

		/// <summary>
		/// The latest state initialized in the constructor or received over the network from the other party.
		/// </summary>
		/// <value>The state of the latest.</value>
		public TState LatestState {
			get {
				return m_latestState;
			}
			set {
				m_latestState = value;
			}
		}

		protected int m_latestStateFrameIndex = 0;
		protected int m_latestAcknowledgedFrame = -1;
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
				case MessageType.AcknowledgeInput:
					// Clear out my unacknowledged command lists
					var peer = GetPeer (connectionId);
					var acknowledgedFrameIndex = binaryReader.ReadInt32 ();
					var stack = peer.UnacknowledgedData;
					while (stack.Count > 0
					       && stack.Peek ().frameIndex <= acknowledgedFrameIndex) {
						stack.Dequeue ();
					}

					if (stack.Count == 0) {
						peer.LastUnacknoledgedData = null;
					}

					var currentLatestAcknowledgedFrame = m_latestAcknowledgedFrame;
					m_latestAcknowledgedFrame = acknowledgedFrameIndex;

					// Tick, now that our commands have been acknowledged
					if (TickAllAcknowledgedFrames) {
						for (var i = currentLatestAcknowledgedFrame + 1; i <= m_latestAcknowledgedFrame; i++) {
							if (DidAcknowledgeFrame != null) {
								DidAcknowledgeFrame (i);
							}
						}
					} else {
						if (DidAcknowledgeFrame != null) {
							DidAcknowledgeFrame (m_latestAcknowledgedFrame);
						}
					}
					break;
				case MessageType.Input:
					// Interpret the incoming commands and input
					// Get the peer we're communicating with
					peerId = GetPeer (connectionId).Id.GetValueOrDefault ();
					// First, handle the input frames
					var frameIndexToAcknowledge = binaryReader.ReadInt32 ();
					var inputCount = binaryReader.ReadByte ();
					var startFrameIndex = frameIndexToAcknowledge - inputCount + 1;
					for (var frameIndex = startFrameIndex; frameIndex < inputCount + startFrameIndex; frameIndex++) {
						if (!m_frames.ContainsKey (frameIndex)) {
							m_frames [frameIndex] = new NetworkFrame ();
							m_frames [frameIndex].frameIndex = frameIndex;
							m_frames [frameIndex].data [peerId] = new PeerFrameData ();
						}
						var frameData = m_frames [frameIndex].data [peerId];
						if (frameIndex == startFrameIndex) {
							// Always deserialize the first input
							frameData.input = new TInput ();
							frameData.input.Deserialize (binaryReader);
						} else {
							// Read a bool to see if the input differs
							var isInputDifferentFromPreviousFrame = binaryReader.ReadBoolean ();
							if (isInputDifferentFromPreviousFrame) {
								frameData.input = new TInput ();
								frameData.input.Deserialize (binaryReader);
							} else {
								frameData.input = (TInput)m_frames [frameIndex - 1].data [peerId].input.Clone ();
							}
						}
					}

					// Always tick all received frames
					if (DidReceiveFrame != null) {
						for (var frameIndex = startFrameIndex; frameIndex < inputCount + startFrameIndex; frameIndex++) {
							DidReceiveFrame (frameIndex);
						}
					}

					// Reply with an acknowledge for the commands
					AcknowledgeData (frameIndexToAcknowledge, connectionId);
					break;
				case MessageType.Empty:
					// We have received no commands for this frame. Just acknowledge.
					// TODO: Interpret an empty frame as unchanged input
					// Read the frame index we are acknowledging.
					var frameIndexOfEmpty = binaryReader.ReadInt32 ();
					// Acknowledging...
					AcknowledgeData (frameIndexOfEmpty, connectionId);
					break;
				case MessageType.State:
					// First, mark this peer as having state since I received
					// state from this peer
					peerId = GetPeer (connectionId).Id.GetValueOrDefault ();
					GetPeer (connectionId).HasState = true;
					// Load in the entire game state
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

					// I may have possibly updated my state, and I received state from a peer.
					// Check if all the peers have state at this point.
					CheckAllHaveState ();

					// Acknowledge receipt of the state
					AcknowledgeState (connectionId);

					break;
				case MessageType.AcknowledgeState:
					// Mark the peer as having received the state.
					GetPeer (connectionId).HasState = true;
					// If all the peers have the latest state, we can start the execution timer
					CheckAllHaveState ();
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
					}

					m_peers [connectionId].Id = peerId;

					// Now that I have peer info, send my latest copy of the state
					SendState (connectionId);
					break;
				}

				binaryReader.Close ();
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
		/// Queue an input for the current frame. Assumes teh input belongs to this peer ID.
		/// </summary>
		/// <param name="input">Input.</param>
		/// <param name="frameIndex">Frame index.</param>
		public void QueueInput (Input input, int frameIndex = int.MinValue)
		{
			// If we didn't proide a frame index, we are going to assume this
			// is data for the frame after the latest acknowledged frame
			if (frameIndex == int.MinValue) {
				frameIndex = LatestAcknowledgedFrame + 1;
			}

			// If this command is coming late, throw an exception
			if (frameIndex < ElapsedFrameCount) {
				throw new LateDataException () {
					Input = input,
					FrameIndex = frameIndex
				};
			}

			foreach (var peerRecord in m_peers) {
				var peer = peerRecord.Value;
				var peerId = peerRecord.Value.Id.GetValueOrDefault ();
				var data = peer.UnacknowledgedData;
				// Should we enqueue on this frame? First check if there is anything in the queue
				if (data.Count > 0) {
					// Look at the latest item on the queue
					var latestQueue = peer.LastUnacknoledgedData;
					// If we are queuing the current frame still, make sure this command gets serialized into this frame's commands list
					if (latestQueue.frameIndex == frameIndex) {
						latestQueue.input = input;
						// We have enqueued the command in the appropriate place, we can move onto the next peer
						continue;
					} else if (latestQueue.frameIndex > frameIndex) {
						// Too old, error condition
						throw new LateDataException () {
							Input = input,
							FrameIndex = frameIndex
						};
					}
					// Otherwise, we're queueing something newer, so we will just stick it on the queue.
				}
				var latestData = new UnacknowledgedData () { 
					frameIndex = frameIndex,
					input = input
				};
				data.Enqueue (latestData);

				peer.LastUnacknoledgedData = latestData;
			}
		}

		internal Peer GetPeer (ConnectionId connectionId)
		{
			if (!m_peers.ContainsKey (connectionId)) {
				m_peers [connectionId] = new Peer (connectionId);
			}

			return m_peers [connectionId];
		}

		/// <summary>
		/// Check whether or not all the peers have received state. If they have,
		/// this method raises the proper events.
		/// </summary>
		protected void CheckAllHaveState ()
		{
			var allHaveState = true;
			foreach (var peer in m_peers) {
				if (!peer.Value.HasState) {
					allHaveState = false;
					break;
				}
			}

			if (m_latestState == null) {
				allHaveState = false;
			}

			if (allHaveState) {
				AllHaveState = true;
			}
		}

		/// <summary>
		/// Sends all the hereto unacknowledged commands and input to the given peer ID. Or, if there are no
		/// unacknowledged commands or just an empty command list, send the empty message.
		/// </summary>
		/// <param name="connectionId">Connection ID</param>
		internal void SendData (ConnectionId connectionId)
		{
			// TODO: When we're sending commands to multiple peers, cache this work
			var memoryStream = new MemoryStream (m_sendCommandBuffer);
			var binaryWriter = new BinaryWriter (memoryStream);
			byte sendError;

			// If there are no unacknowledged frames
			var peer = GetPeer (connectionId);
			var unacknowledgedQueuedData = peer.UnacknowledgedData;

			// If there are no unacknowledged commands, send empty commands with the tickrate clock's frame index
			if (unacknowledgedQueuedData.Count == 0) {
				// An empty message should be interpreted as the input is UNCHANGED
				binaryWriter.Write (MessageType.Empty);
				binaryWriter.Write (Clock.ElapsedFrameCount - 1);
				Transport.Send (connectionId, Transport.UnreliableChannelId, m_sendCommandBuffer, 0, (int)binaryWriter.BaseStream.Position, out sendError);
				return;
			}

			binaryWriter.Write (MessageType.Input);

			// Let's deal with inputs first.
			// Write the frame of the latest input
			binaryWriter.Write (peer.LastUnacknoledgedData.frameIndex);
			binaryWriter.Write ((byte)unacknowledgedQueuedData.Count);
			Input previousInput = null;
			foreach (var data in unacknowledgedQueuedData) {
				if (previousInput == null) {
					// If this is the first input, serialize it directly
					previousInput = data.input;
					previousInput.Serialize (binaryWriter);
					// Later we will use this to compute a diff
					continue;
				}

				// Does the current input differ from the previous input?
				var isDifferentFromPreviousInput = previousInput.Equals (data.input);
				// If it does differ, we're going to mark as such and serialize the input
				binaryWriter.Write (isDifferentFromPreviousInput);
				if (isDifferentFromPreviousInput) {
					data.input.Serialize (binaryWriter);
				}
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
		protected void AcknowledgeData (int frameIndex, int connectionId)
		{
			// A five byte buffer. The first byte is the message type of acknowledge, the next
			// four bytes are the frame index we are acknowledging.
			var acknowledgeBuffer = new byte[5];
			acknowledgeBuffer [0] = MessageType.AcknowledgeInput;
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
		/// When the network clock ticks, try to send commands. This tick happens late, so that input generally
		/// has arrived by the time the data is sent.
		/// </summary>
		/// <param name="frameIndex">Frame index.</param>
		protected void OnNetworkClockLateTick (int elapsedFrames)
		{
			// Send my commands to all my peers
			foreach (var peer in m_peers) {
				SendData (peer.Key);
			}
		}

		/// <summary>
		/// Networking ticks with the frame index of the latest frame acknowledged by other peers. Currently
		/// only supports two player. If the peer acknowledges a frame that isn't 1 away from the current latest acknowledge
		/// frame, set <see cref="HiddenSwitch.Network`1.TickAllAcknowledgedFrames"/> to true if you would like a tick
		/// for all the intermediate frames.
		/// </summary>
		public event Action<int> DidAcknowledgeFrame;

		/// <summary>
		/// Raised when a frame is received from a peer. The first argument is the frame number.
		/// </summary>
		public event Action<int> DidReceiveFrame;

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
			set {
				StateStartFrame = value;
			}
		}


		/// <summary>
		/// Gets or sets the frame that the state started at
		/// </summary>
		/// <value>The state start frame.</value>
		public int StateStartFrame {
			get {
				return m_stateStartFrame;
			}
			set {
				m_stateStartFrame = value;
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

				if (StateSynchronized != null
				    && m_allHaveState) {
					StateSynchronized ();
				}
			}
		}

		/// <summary>
		/// Gets a simulation frame data structure for the given frame
		/// </summary>
		/// <returns>The simulation frame.</returns>
		/// <param name="forFrame">For frame.</param>
		public SimulationFrame GetSimulationFrame (FrameIndex forFrame, bool removePreviousFrames = true)
		{
			var simulationFrame = new SimulationFrame ();
			simulationFrame.FrameIndex = forFrame;
			simulationFrame.Inputs = new Dictionary<int, Input> ();
			var frame = m_frames [forFrame];
			foreach (var data in frame.data) {
				simulationFrame.Inputs [data.Key] = data.Value.input;
			}
			if (removePreviousFrames) {
				var i = -1;
				while (m_frames.ContainsKey (forFrame + i)) {
					m_frames.Remove (forFrame + i);
					i--;
				}
			}
			return simulationFrame;
		}
	}

	public class Network<TState> : Network<TState, Input>
		where TState : State, new()
	{
		public Network (IClock clock, PeerId? peerId = null, ITransport transport = null, TState startState = null) : base (clock, peerId, transport, startState)
		{
		}
	}

}