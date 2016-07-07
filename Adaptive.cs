using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace HiddenSwitch.Multiplayer
{
	public interface IAdaptiveDelegate<TState, TInput>
		where TState : State, new()
		where TInput : Input, new()
	{
		TInput GetCurrentInput ();

		TState GetStartState ();
	}

	public class Adaptive<TState, TInput>
		where TState : State, new()
		where TInput : Input, new()
	{
		public Simulation<TState, TInput> Simulation { get; set; }

		public Network<TState, TInput> Network { get; set; }

		public PlayoutDelayedClock SimulationClock { get; set; }

		public TimeClock InputClock { get; set; }

		public IAdaptiveDelegate<TState, TInput> GameManager { get; set; }

		public int MyPeerId { get { return Network.MyPeerId; } }

		public int PeerDelay { get; protected set; }

		public bool IsServer { get; protected set; }

		protected TimeClock m_BufferClock;
		protected bool m_started;
		protected int m_maxElapsedInputFrames = -1;


		public Adaptive (IAdaptiveDelegate<TState, TInput> gameManager = null, 
		                 int simulationDelay = 4,
		                 int maxPeerDelay = 16,
		                 TimeClock inputClock = null, 
		                 TimeClock simulationClock = null,
		                 TimeClock bufferClock = null,
		                 Network<TState, TInput> network = null,
		                 int framesPerSecond = 60,
		                 int port = 12500)
		{
			m_BufferClock = bufferClock ?? new TimeClock (framesPerSecond: framesPerSecond, autostart: true, endOfFrame: true);
			m_BufferClock.LateTick += CheckBuffer;
			PeerDelay = maxPeerDelay;
			GameManager = gameManager;

			SimulationClock = new PlayoutDelayedClock (simulationRate: framesPerSecond, playoutDelayFrameCount: simulationDelay, timeClock: simulationClock);

			// Only support two player for now, so support the two peers (myself and the other guy)
			SimulationClock.PeerCount = 2;
			Simulation = new Simulation<TState, TInput> (SimulationClock);
			InputClock = inputClock ?? new TimeClock (framesPerSecond: framesPerSecond, autostart: false, endOfFrame: true, startFrame: 0);
			InputClock.Tick += OnInputClockTick;

			Network = network ?? new Network<TState, TInput> (clock: InputClock, port: port);
			Network.TickAllAcknowledgedFrames = true;
			Network.DidAcknowledgeFrame += OnMyFrameAcknowledged;
			Network.Ready += OnStateSynchronized;
			Network.DidReceiveFrame += OnReceivedFrameFromNetwork;


		}

		//		protected int m_greatestFrameFromNetwork = -1;

		void OnReceivedFrameFromNetwork (int frameIndex, int peerId)
		{
			var simulationFrame = Network.GetSimulationFrame (frameIndex);
			Simulation.SetOrExtendFrame (frameIndex, simulationFrame);
			// I have received data from the peer
			SimulationClock.SetReadyForFrame (frameIndex, peerId);
		}

		void CheckBuffer (int elapsedSentinelFrames)
		{
			// Did the input clock get too far ahead of the other peers? If so, pause the input. This will have the effect
			// of pausing the simulation if the network's buffer gets depleted.
			var isInputTooFarInTheFuture = InputClock.ElapsedFrameCount - PeerDelay > Network.LatestAcknowledgedFrame;
			InputClock.Running = m_started && !isInputTooFarInTheFuture;
		}


		void OnInputClockTick (int elapsedFrames)
		{
			if (elapsedFrames <= m_maxElapsedInputFrames) {
				return;
			}

			m_maxElapsedInputFrames = System.Math.Max (elapsedFrames, m_maxElapsedInputFrames);

			var frameIndex = elapsedFrames - 1;
			// Gather the input from the game manager
			var input = GameManager.GetCurrentInput ();

			// If we have already processed this frame, don't process it again

			// Set my input into the simulation too
			Simulation.SetInput (input, MyPeerId, frameIndex);

			// Set the input for the network
			Network.QueueInput (input, frameIndex: frameIndex);
		}

		/// <summary>
		/// Connects to a host and returns the peer ID
		/// </summary>
		/// <param name="hostName">Host name.</param>
		public Peer Connect (string hostName, int port = -1)
		{
			if (port == -1) {
				port = this.Network.Port;
			}
			return Network.AddPeer (hostName, port);
		}

		public void Host (TState withState)
		{
			Network.LatestState = (TState)withState.Clone ();
			Simulation.SetStartState (withState, 0);
			IsServer = true;
		}

		void OnStateSynchronized ()
		{
			// If I am the client, we should set my start frame information
			if (!IsServer) {
				Simulation.SetStartState (Network.LatestState, Network.StateStartFrame);
				InputClock.StartFrame = Simulation.StartFrame;
			}

			// Start the clocks!
			m_started = true;
			InputClock.Start ();
		}

		void OnMyFrameAcknowledged (int frameIndex, int peerId)
		{
			// my peer has acknowledged the frame, so my
			// input is ready to be processed
			// make sure to set all frames prior to this one acknowledged too
			SimulationClock.SetReadyForFrame (frameIndex, MyPeerId, allPrior: true);
		}
	}
}