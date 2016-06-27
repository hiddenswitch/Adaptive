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
	public class Peer : IEqualityComparer<Peer>
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

		public UnacknowledgedData LastUnacknoledgedData { get; set; }

		public Queue<UnacknowledgedData> UnacknowledgedData { get; set; }

		internal Peer (int connectionId)
		{
			ConnectionId = connectionId;
			UnacknowledgedData = new Queue<HiddenSwitch.Multiplayer.UnacknowledgedData> (30 * 16);
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