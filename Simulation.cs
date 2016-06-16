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
	public delegate void SimulationCommandHandler (State mutableState, CommandArguments arguments, PeerId peerId, FrameIndex frameIndex);
	public delegate void SimulationCommandHandler<TState, TCommandArguments> (TState mutableState, TCommandArguments arguments, PeerId peerId, FrameIndex frameIndex)
		where TCommandArguments : CommandArguments, new()
		where TState : State, new();

	public delegate void SimulationRenderedHandler<TState> (TState immutableState, int renderFrameIndex)
		where TState : State, new();
	
	public class Simulation<TState> : IClock
		where TState : State, new()
	{
		private IClock m_executionClock;

		public IClock ExecutionClock {
			get { return m_executionClock; }
			set { 
				if (m_executionClock != null) {
					m_executionClock.Tick -= OnExecutionClockTick;
				}

				m_executionClock = value;
				m_executionClock.Tick += OnExecutionClockTick;
			}
		}

		protected Dictionary<byte, SimulationCommandHandler> m_commandHandlers = new Dictionary<byte, SimulationCommandHandler> ();
		protected Dictionary<FrameIndex, SimulationFrame> m_frames = new Dictionary<FrameIndex, SimulationFrame> ();
		protected TState m_latestState;

		/// <summary>
		/// Get the latest state from this simulation.
		/// </summary>
		/// <value>The state.</value>
		public TState State {
			get {
				return m_latestState;
			}
		}

		public event SimulationRenderedHandler<TState> Rendered;

		public Simulation (IClock executionClock, TState startingState = null)
		{
			ExecutionClock = executionClock;
			m_latestState = startingState ?? new TState ();
		}

		public SimulationFrame this [FrameIndex frameIndex] {
			get {
				return GetFrame (frameIndex);
			}
			set {
				SetFrame (frameIndex, value);
			}
		}

		public SimulationFrame GetFrame (FrameIndex frameIndex)
		{
			if (m_frames.ContainsKey (frameIndex)) {
				return m_frames [frameIndex];
			} else {
				return null;
			}
		}

		public void SetFrame (FrameIndex frameIndex, SimulationFrame value)
		{
			m_frames [frameIndex] = value;
		}

		/// <summary>
		/// Execute commands for the given frame.
		/// </summary>
		/// <param name="frameIndex">Frame index.</param>
		void OnExecutionClockTick (FrameIndex frameIndex)
		{
			// In case something is hanging on to an old reference of the state, clone.
			// Note, code which never drops a reference to the state variable will leak memory here.
			var nextState = (TState)m_latestState.Clone ();

			var frame = this [frameIndex];
			// If there is no frame, no commands were issued for this frame index.
			if (frame != null) {
				foreach (var command in frame.Commands) {
					m_commandHandlers [command.commandId].Invoke (nextState, command.arguments, command.peerId, frameIndex);
				}	
			}


			m_latestState = nextState;

			// Clear out this frame since it has executed
			this.m_frames.Remove (frameIndex);
		}

		/// <summary>
		/// Adds the command handler.
		/// </summary>
		/// <param name="id">Identifier.</param>
		/// <param name="handler">Handler.</param>
		/// <typeparam name="TCommandArguments">The 1st type parameter.</typeparam>
		public void AddCommandHandler<TCommandArguments> (byte id, SimulationCommandHandler<TState, TCommandArguments> handler)
			where TCommandArguments : CommandArguments, new()
		{
			m_commandHandlers.Add (id, delegate(State mutableState, CommandArguments arguments, int peerId, int frameIndex) {
				TCommandArguments typedArguments = arguments as TCommandArguments;
				TState typedState = mutableState as TState;
				if (handler != null) {
					handler (typedState, typedArguments, peerId, frameIndex);
				}
			});
		}

		/// <summary>
		/// Calls a command with the given id and arguments. This command is sent with the current rendering frame as its time,
		/// not the latest frame (which may be possible to render, but isn't rendered yet).
		/// </summary>
		/// <param name="id">Identifier.</param>
		/// <param name="command">Command.</param>
		/// <param name="frameIndex">The execution frame this command should run against. When using a playout delayed clock, pass in the
		/// last executable frame. This could depend on the type of command.</param> 
		/// <typeparam name="TCommandArguments">The 1st type parameter.</typeparam>
		public virtual void CallCommand<TCommandArguments> (byte id, TCommandArguments command, PeerId peerId, FrameIndex? frameIndex = null)
			where TCommandArguments : CommandArguments
		{
			int theFrameIndex = frameIndex.HasValue ? frameIndex.GetValueOrDefault() : ElapsedFrameCount;
			if (!m_frames.ContainsKey (theFrameIndex)) {
				m_frames [theFrameIndex] = new SimulationFrame ();
			}

			var frame = m_frames [theFrameIndex];

			frame.Commands.Add (new CommandWithArgumentsAndPeer () {
				arguments = command,
				commandId = id,
				peerId = peerId
			});
		}

		/// <summary>
		/// Corresponds to a render tick, which may be offset / interpolated / smoothed with respect to the execution clock.
		/// </summary>
		public event Action<int> Tick;

		/// <summary>
		/// Gets the number of executed frames.
		/// </summary>
		/// <value>The current frame.</value>
		public int ElapsedFrameCount {
			get {
				return ExecutionClock.ElapsedFrameCount;
			}
		}

		public int StartFrame {
			get {
				return ExecutionClock.StartFrame;
			}
		}
	}
	
}