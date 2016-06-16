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

	public class ManualClock : IClock
	{
		public int StartFrame { get; set; }

		public event Action<int> Tick;

		protected int m_elapsedFrameCount;

		public int ElapsedFrameCount {
			get {
				return m_elapsedFrameCount;
			}
			set {
				if (m_elapsedFrameCount != value) {
					if (Tick != null) {
						Tick (m_elapsedFrameCount + StartFrame);
					}
					m_elapsedFrameCount = value;
				}
			}
		}

		public void Step ()
		{
			ElapsedFrameCount += 1;
		}
	}
	
}