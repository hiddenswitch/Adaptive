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

		public Adaptive (IAdaptiveDelegate<TState, TInput> gameManager, 
		                 int simulationDelay = 4,
		                 int maxPeerDelay = 16,
		                 TimeClock inputClock = null, 
		                 TimeClock simulationClock = null,
		                 TimeClock bufferClock = null,
		                 Network<TState, TInput> network = null)
		{
			m_BufferClock = bufferClock ?? new TimeClock (framesPerSecond: 60, autostart: true, endOfFrame: true);
			m_BufferClock.Tick += CheckBuffer;
			PeerDelay = maxPeerDelay;
			GameManager = gameManager;
			SimulationClock = new PlayoutDelayedClock (simulationRate: 60, playoutDelayFrameCount: simulationDelay, timeClock: simulationClock);
			// Only support two player for now, so support the two peers (myself and the other guy)
			SimulationClock.PeerCount = 2;
			Simulation = new Simulation<TState, TInput> (SimulationClock);
			InputClock = inputClock ?? new TimeClock (framesPerSecond: 60, autostart: false, endOfFrame: true, startFrame: 0);
			InputClock.Tick += OnInputClockTick;
			Network = network ?? new Network<TState, TInput> (clock: InputClock);
			Network.TickAllAcknowledgedFrames = true;
			Network.DidAcknowledgeFrame += OnMyFrameAcknowledged;
			Network.StateSynchronized += OnStateSynchronized;
			Network.DidReceiveFrame += OnReceivedFrameFromNetwork;
		}

		void OnReceivedFrameFromNetwork (int frameIndex)
		{
			var simulationFrame = Network.GetSimulationFrame (frameIndex);
			Simulation.SetOrExtendFrame (frameIndex, simulationFrame);
			// I have received data from the peer
			SimulationClock.IncrementReadyForFrame (frameIndex);
		}

		void CheckBuffer (int elapsedSentinelFrames)
		{
			// Did the input clock get too far ahead of the other peers? If so, pause the input. This will have the effect
			// of pausing the simulation if the network's buffer gets depleted.
			var isInputTooFarInTheFuture = InputClock.ElapsedFrameCount - PeerDelay > Network.LatestAcknowledgedFrame;
			InputClock.Running = !isInputTooFarInTheFuture;
		}

		void OnInputClockTick (int elapsedFrames)
		{
			var frameIndex = elapsedFrames - 1;
			// Gather the input from the game manager
			var input = GameManager.GetCurrentInput ();

			// Set my input into the simulation too
			Simulation.SetInput (input, MyPeerId, frameIndex);

			// Set the input for the network
			Network.QueueInput (input, frameIndex: frameIndex);
		}

		/// <summary>
		/// Connects to a host and returns the peer ID
		/// </summary>
		/// <param name="hostName">Host name.</param>
		public Peer Connect (string hostName)
		{
			return Network.AddPeer (hostName, 6002);
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
			InputClock.Start ();
		}

		void OnMyFrameAcknowledged (int frameIndex)
		{
			// my peer has acknowledged the frame, so my
			// input is ready to be processed
			SimulationClock.IncrementReadyForFrame (frameIndex: frameIndex);
		}
	}
}