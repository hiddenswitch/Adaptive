using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using PeerId = System.Int32;
using ConnectionId = System.Int32;

namespace HiddenSwitch.Multiplayer
{

	/// <summary>
	/// An object that allows you to query information about a peer.
	/// </summary>
	public sealed class Peer : IEqualityComparer<Peer>
	{
		public PeerId? Id {
			get;
			set;
		}

		public ConnectionId ConnectionId {
			get;
			set;
		}

		public bool HasPeerInfo {
			get {
				return Id.HasValue;
			}
		}

		public bool HasState { get; set; }

		public bool HasError { get; private set; }

		public int? Max { get; private set; }

		public int? Min { get; private set; }

		public SortedDictionary<int, UnacknowledgedData> UnacknowledgedData { get; private set; }

		internal Peer (int connectionId)
		{
			ConnectionId = connectionId;
			UnacknowledgedData = new SortedDictionary<int, UnacknowledgedData> ();
		}

		public void AcknowledgeFrameAndOlder (int frameIndex)
		{
			if (HasError) {
				return;
			}

			if (!Min.HasValue) {
				return;
			}

			var min = Min.GetValueOrDefault ();

			for (var i = frameIndex; i >= min; i--) {
				UnacknowledgedData.Remove (i);
			}

			Min = frameIndex + 1;
			CheckMinAndMax ();
		}

		public void Enqueue (UnacknowledgedData data)
		{
			if (Max.HasValue) {
				Set (data, Max.GetValueOrDefault () + 1);
			} else {
				Set (data, 0);				
			}
		}

		public void Set (UnacknowledgedData data, int frameIndex)
		{
			UnacknowledgedData [frameIndex] = data;
			if (Max.HasValue) {
				Max = Math.Max (Max.GetValueOrDefault (), frameIndex);
			} else {
				Max = frameIndex;
			}
			if (Min.HasValue) {
				Min = Math.Min (Min.GetValueOrDefault (), frameIndex);
			} else {
				Min = frameIndex;
			}

			CheckMinAndMax ();
		}

		void CheckMinAndMax ()
		{
			if (Max.HasValue
			    && !UnacknowledgedData.ContainsKey (Max.GetValueOrDefault ())) {
				Max = null;
			}

			if (Min.HasValue
			    && !UnacknowledgedData.ContainsKey (Min.GetValueOrDefault ())) {
				Min = null;
			}
		}

		public override int GetHashCode ()
		{
			return ConnectionId;
		}

		#region IEqualityComparer implementation

		public bool Equals (Peer x, Peer y)
		{
			if (x == null && y == null) {
				return true;
			} else if (x == null || y == null) {
				return false;
			}
			
			return x.GetHashCode () == y.GetHashCode ();
		}

		public int GetHashCode (Peer obj)
		{
			if (obj == null) {
				return Int32.MinValue;
			}
			return obj.GetHashCode ();
		}

		#endregion
	}
	
}