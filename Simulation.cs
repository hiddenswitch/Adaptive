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
	public delegate void SimulationInputHandler<TState, TInput> (TState mutableState, KeyValuePair<PeerId, TInput>[] inputs, FrameIndex frameIndex)
		where TState : State, new()
		where TInput : Input, new();
	
	public class Simulation<TState, TInput> : IClock
		where TState : State, new()
		where TInput : Input, new()
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

		protected Dictionary<FrameIndex, SimulationFrame> m_frames = new Dictionary<FrameIndex, SimulationFrame> ();
		protected TState m_latestState;

		public int FrameBuffer { get; set; }

		/// <summary>
		/// Get the latest state from this simulation.
		/// </summary>
		/// <value>The state.</value>
		public TState LatestState {
			get {
				return m_latestState == null ? null : (TState)m_latestState.Clone ();
			}
			set {
				m_latestState = value;
			}
		}

		public Simulation (IClock executionClock, TState startingState = null, int frameBuffer = 4)
		{
			ExecutionClock = executionClock;
			m_latestState = startingState;
			FrameBuffer = frameBuffer;
		}

		public SimulationFrame this [FrameIndex frameIndex] {
			get {
				return GetFrame (frameIndex);
			}
			set {
				SetOrExtendFrame (frameIndex, value);
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

		/// <summary>
		/// A function that takes a mutable gamestate, an input and the current frame and mutates
		/// the gamestate to reflect the latest input.
		/// </summary>
		/// <value>The input handler.</value>
		public SimulationInputHandler<TState, TInput> InputHandler {
			get;
			set;
		}

		public void SetOrExtendFrame (FrameIndex frameIndex, SimulationFrame value)
		{
			// If the frame already exists, extend the current simulation frame with the provided frame
			if (m_frames.ContainsKey (frameIndex)) {
				var currentFrame = m_frames [frameIndex];
				foreach (var inputData in value.Inputs) {
					currentFrame.Inputs [inputData.Key] = inputData.Value;
				}
			} else {
				m_frames [frameIndex] = value;
			}
		}

		/// <summary>
		/// Execute commands for the given frame.
		/// </summary>
		/// <param name="elapsedFrames">How many frames have elapsed?</param>
		void OnExecutionClockTick (FrameIndex elapsedFrames)
		{
			// Execute the frame prior to the one that just elapsed
			var frameIndex = elapsedFrames - 1;
			var nextState = m_latestState;

			var frame = this [frameIndex];

			// Execute inputs
			if (InputHandler != null) {
				var inputs = new KeyValuePair<PeerId, TInput>[frame.Inputs.Count];
				// Copy with casting
				var uncastedInputs = new KeyValuePair<PeerId, Input>[frame.Inputs.Count];
				frame.Inputs.CopyTo (uncastedInputs, 0);
				for (var i = 0; i < uncastedInputs.Length; i++) {
					var kv = uncastedInputs [i];
					inputs [i] = new KeyValuePair<int, TInput> (kv.Key, (TInput)kv.Value);
				}
				InputHandler (nextState, inputs, frameIndex);
			} else {
				throw new InvalidOperationException ("No InputHandler was specified on this simulation.");
			}

			m_latestState = nextState;

			// Clear out the frame since it has executed
			var oldFrameOffset = -FrameBuffer;
			while (m_frames.ContainsKey (frameIndex + oldFrameOffset)) {
				this.m_frames.Remove (frameIndex + oldFrameOffset);
				oldFrameOffset--;
			}

			// Tick
			if (Tick != null) {
				Tick (elapsedFrames);
			}
			if (LateTick != null) {
				LateTick (elapsedFrames);
			}
		}

		/// <summary>
		/// Sets the latest input from the given peer.
		/// </summary>
		/// <returns>The latest input.</returns>
		/// <param name="input">Input.</param>
		/// <param name="peerId">Peer identifier.</param>
		public void SetInput (TInput input, PeerId peerId, FrameIndex frameIndex)
		{
			if (!m_frames.ContainsKey (frameIndex)) {
				m_frames [frameIndex] = new SimulationFrame ();
			}

			m_frames [frameIndex].Inputs [peerId] = input;
		}

		/// <summary>
		/// Corresponds to a render tick, which may be offset / interpolated / smoothed with respect to the execution clock.
		/// </summary>
		public event Action<int> Tick;

		public event Action<int> LateTick;

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
			set {
				ExecutionClock.StartFrame = value;
			}
		}

		public void SetStartState (TState state, FrameIndex frameIndex)
		{
			ExecutionClock.StartFrame = frameIndex;
			m_latestState = (TState)state.Clone ();
		}
	}

	public class Simulation<TState> : Simulation<TState, Input>
		where TState: State, new()
	{
		public Simulation (IClock executionClock, TState startingState = null) : base (executionClock, startingState)
		{
		}
	}
}