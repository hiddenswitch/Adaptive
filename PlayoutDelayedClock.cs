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

	public class PlayoutDelayedClock : ManualClock
	{
		/// <summary>
		/// How many frames should be buffered before executing the commands that occurred during those frames?
		/// </summary>
		public int PlayoutDelayFrameCount { get; set; }

		/// <summary>
		/// How many command lists per frame do we expect? When the number of command lists we have received for a given
		/// frame index is equal to (or exceeds) PeerCount, this frame has enough information to execute.
		/// </summary>
		/// <value>The peer count.</value>
		public byte PeerCount { get; set; }

		/// <summary>
		/// Use a time clock to smoothly execute commands until it needs to be stopped for buffering
		/// </summary>
		public TimeClock TimeClock { get; protected set; }

		/// <summary>
		/// What should be considered as the first frame of the simulation? This frame will consider
		/// all prior frames as ready, even if we've never seen them before.
		/// There are situations where FirstFrameIndex is not zero. For example, if we're connecting to a game
		/// in progress, we want the FirstFrameIndex to correspond to the frame index of the first state we received
		/// from the other peers.
		/// </summary>
		/// <value>The first index of the frame.</value>
		public int FirstFrameIndex { get; set; }

		/// <summary>
		/// A dictionary from frame indices to counts that indicates how many peers worth of command we have
		/// in order to execute.
		/// </summary>
		protected Dictionary<FrameIndex, FrameReadyInfo> m_frameCommands = new Dictionary<FrameIndex, FrameReadyInfo> ();
		/// <summary>
		/// This is the highest index of a frame we can execute.
		/// </summary>
		protected int m_highestExecutableFrame = int.MinValue;

		/// <summary>
		/// Initializes a new instance of the <see cref="HiddenSwitch.Multiplayer.PlayoutDelayedClock"/> class.
		/// 
		/// This clock will start ticking when we have buffered enough frames to start executing. This clock will
		/// stop ticking if we have exhausted our buffer. This ensures smooth execution playback.
		/// </summary>
		/// <param name="simulationRate">The number of ticks per second we should try to achieve. In the future, this value will change
		/// dynamically to smooth out the ticks from this clock.</param>
		/// <param name="playoutDelayFrameCount">How many frames should be buffered before we start ticking?</param>
		/// <param name="firstFrameIndex">First frame index. Set this to a nonzero value if, for example, you are connecting
		/// to a game in progress and don't have older frame data. See <see cref="HiddenSwitch.Multiplayer.PlaoutDelayedClock.FirstFrameIndex" /></param>
		public PlayoutDelayedClock (int simulationRate = 30, int playoutDelayFrameCount = 4, int firstFrameIndex = 0) : base ()
		{
			PlayoutDelayFrameCount = playoutDelayFrameCount;
			FirstFrameIndex = firstFrameIndex;
			TimeClock = new TimeClock (autostart: false, framesPerSecond: simulationRate);
			TimeClock.Tick += OnTimeClockTick;
		}

		/// <summary>
		/// Call this method when you receive commands, possibly empty, from a peer in the game. It doesn't matter which
		/// peer, because you will not receive redundant commands.
		/// 
		/// TODO: Optimize for situations where you receive command lists for multiple frames.
		/// </summary>
		/// <param name="frameIndex">Frame index.</param>
		public void IncrementReadyForFrame (FrameIndex frameIndex)
		{
			// TODO: Analyze the rate that we receive ready frames. Then, adjust the simulation rate appropriately.

			// Is this the first time we're seeing this frame?
			if (!m_frameCommands.ContainsKey (frameIndex)) {
				m_frameCommands [frameIndex] = new FrameReadyInfo ();
			}

			var frameInfo = m_frameCommands [frameIndex];

			// Is this the very first frame of the simulation? If so, initialize that all prior frames
			// to this one are ready.
			// Simulations might start at later times than zero if we are starting the simulation late, like
			// connecting to an existing game.
			if (frameIndex == FirstFrameIndex) {
				frameInfo.AllPriorFramesReady = true;	
			} else {
				// If the prior frame's record doesn't exist, all prior frames could not possibly be ready.
				// For convenience, make the record now anyway.
				if (!m_frameCommands.ContainsKey (frameIndex - 1)) {
					m_frameCommands [frameIndex - 1] = new FrameReadyInfo ();
				} else {
					// All of this frame's prior frames are ready if
					// 1. The prior frame's all prior frames are ready and
					// 2. The prior frame is ready.
					frameInfo.AllPriorFramesReady = m_frameCommands [frameIndex - 1].AllPriorFramesReady
					&& m_frameCommands [frameIndex - 1].PeersReady >= PeerCount;
				}
			}

			// Increment the number of peers ready
			frameInfo.PeersReady++;

			// If this frame is now ready, and all my prior frames are ready, I can definitely execute this frame
			if (frameInfo.PeersReady >= PeerCount
			    && frameInfo.AllPriorFramesReady) {
				m_highestExecutableFrame = frameIndex;

				// I have to keep propagating forward that all prior frames are ready until they are not ready.
				var nextFrameIndex = frameIndex + 1;
				while (m_frameCommands.ContainsKey (nextFrameIndex)
				       && m_frameCommands [nextFrameIndex].PeersReady >= PeerCount) {
					m_frameCommands [nextFrameIndex].AllPriorFramesReady = true;
					m_highestExecutableFrame = nextFrameIndex;
					nextFrameIndex += 1;
				}
			}

			// Note, we will remove old frame infos as they get executed.

			// I have updated my highest executable frame. I should now check if I can resume my buffer.
			if (!TimeClock.Running
			    && TimeClock.ElapsedFrameCount - PlayoutDelayFrameCount >= m_highestExecutableFrame) {
				// Start the clock. This will cause it to eat up the buffer, which is intended
				// This clock is actually responsible for executing the simulation steps
				TimeClock.Start ();
			}
		}

		/// <summary>
		/// This method handles the ticking of the time clock internally used to provide a smooth execution of gameplay.
		/// Theoretically, this could be ticking with a non-timed clock.
		/// </summary>
		/// <param name="elapsedTimeFrames">Elapsed time frames.</param>
		protected void OnTimeClockTick (int elapsedTimeFrames)
		{
			// We don't use elapsed time frames. It's not relevant to our simulation. This PlayoutDelayedClock's
			// elapsed frames corresponds to the current simulation frame.

			// The clock should only be started when we have enough frames buffered to ensure smooth playback.
			// This depends on the PlayoutDelayFrameCount.

			// First, check that we can execute the next frame.
			if (m_highestExecutableFrame > ElapsedFrameCount) {
				// Go ahead and step.
				Step ();
				// Clear out all the old frame infos. Keep the one for the just elapsed frame
				// because it contains an accourate "AllPriorFramesReady" value that will propagate
				// to future frames.
				var staleFrameInfoIndex = ElapsedFrameCount - 1;
				while (m_frameCommands.ContainsKey (staleFrameInfoIndex)) {
					m_frameCommands.Remove (staleFrameInfoIndex);
					staleFrameInfoIndex--;
				}
			} else {
				// We have exhausted our buffer. Stop the time clock, which stops the execution
				// of the playout delayed clock until enough frame information has been buffered.
				TimeClock.Stop ();
			}
		}

		public sealed class FrameReadyInfo
		{
			public byte PeersReady;
			public bool AllPriorFramesReady;
		}
	}
	
}