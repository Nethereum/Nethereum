namespace Nethereum.DevP2P.Peering
{
    /// <summary>
    /// Direction of a connected peer relative to this node, used by
    /// <see cref="DialScheduler.OnPeerConnected"/> to maintain the
    /// inbound/outbound ratio counters. Maps to the inbound-connection
    /// flag on each peer.
    /// </summary>
    public enum PeerDirection
    {
        /// <summary>Peer initiated the TCP connection to us (we accepted).</summary>
        Inbound,

        /// <summary>We initiated the TCP connection to the peer.</summary>
        Outbound
    }
}
