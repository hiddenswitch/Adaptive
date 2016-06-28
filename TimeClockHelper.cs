using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace HiddenSwitch.Multiplayer
{

	/// <summary>
	/// A helper that uses Unity coroutines to provide time.
	/// </summary>
	internal class TimeClockHelper : MonoBehaviour
	{
		internal int framesPerSecond = 60;
		internal bool endOfFrame;
		internal TimeClock timeClock;
		internal bool timerCoroutineRunning = false;
		protected float m_lastTime;

		public long ElapsedTicks {
			get;
			protected set;
		}

		internal bool running;

		void Awake ()
		{
			DontDestroyOnLoad (this.gameObject);
		}

		void Start ()
		{
			StartCoroutine (TimerCoroutine ());
		}

		void Update ()
		{
			if (endOfFrame
			    && running) {
				var currentTime = Time.time;
				ElapsedTicks = (long)((currentTime - m_lastTime) * 10e10);
				m_lastTime = currentTime;

				if (timeClock != null) {
					timeClock.HelperTick ();
				}
			} else if (!endOfFrame
			           && running
			           && !timerCoroutineRunning) {
				StartCoroutine (TimerCoroutine ());
				timerCoroutineRunning = true;
			}
		}

		IEnumerator TimerCoroutine ()
		{
			timerCoroutineRunning = true;
			while (true) {
				if (!running) {
					timerCoroutineRunning = false;
					StopAllCoroutines ();
					yield break;

				}
				m_lastTime = Time.time;
				if (endOfFrame) {
					timerCoroutineRunning = false;
					StopAllCoroutines ();
					yield break;
				} else {
					if (framesPerSecond <= 0) {
						running = false;
						break;
					}

					yield return new WaitForSeconds (1.0f / (float)framesPerSecond);
				}

				var currentTime = Time.time;
				ElapsedTicks = (long)((currentTime - m_lastTime) * 10e10);
				m_lastTime = currentTime;

				if (timeClock != null) {
					timeClock.HelperTick ();
				}
			}
		}
	}
	
}