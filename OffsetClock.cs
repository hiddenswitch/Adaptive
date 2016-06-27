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
	/// Ticks values offset from the provided clock.
	/// </summary>
	public class OffsetClock : IClock
	{
		public int Offset { get; protected set; }

		public IClock InputClock { get; protected set; }

		public bool SkipNegativeValues { get; protected set; }

		public int StartFrame {
			get {
				return InputClock.StartFrame;
			}
			set {
				InputClock.StartFrame = value;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="HiddenSwitch.Multiplayer.OffsetClock"/> class.
		/// </summary>
		/// <param name="inputClock">Input clock.</param>
		/// <param name="offset">Offset. Use a negative value for a negative offset</param>
		/// <param name="skipNegativeValues">If set to <c>true</c>, this clock does not tick for negative values.</param>
		public OffsetClock (IClock inputClock, int offset, bool skipNegativeValues = true)
		{
			InputClock = inputClock;
			Offset = offset;
			SkipNegativeValues = skipNegativeValues;

			InputClock.Tick += OnInputClockTick;
		}

		void OnInputClockTick (int obj)
		{
			if (SkipNegativeValues
			    && OffsetElapsedFrameCount < 0) {
				return;
			}

			if (Tick != null) {
				Tick (OffsetElapsedFrameCount);
			}

			if (LateTick != null) {
				LateTick (OffsetElapsedFrameCount);
			}
		}

		public event Action<int> Tick;
		public event Action<int> LateTick;

		protected int OffsetElapsedFrameCount {
			get {
				return InputClock.ElapsedFrameCount + Offset;
			}
		}

		public int ElapsedFrameCount {
			get {
				return OffsetElapsedFrameCount;
			}
		}

	}
	
}