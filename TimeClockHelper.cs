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
		internal int framesPerSecond = 30;
		internal bool endOfFrame;
		internal TimeClock timeClock;

		internal bool running;

		void Awake ()
		{
			DontDestroyOnLoad (this.gameObject);
		}

		void Start ()
		{
			StartCoroutine (Timer ());
		}

		IEnumerator Timer ()
		{
			while (true) {
				while (running) {
					if (endOfFrame) {
						yield return new WaitForEndOfFrame ();
					} else {
						if (framesPerSecond <= 0) {
							running = false;
							break;
						}

						yield return new WaitForSeconds (1.0f / (float)framesPerSecond);
					}

					timeClock.HelperTick ();
				}
				while (!running) {
					yield return null;
				}
			}
		}
	}
	
}