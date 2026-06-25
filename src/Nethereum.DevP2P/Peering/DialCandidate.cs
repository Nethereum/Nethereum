using System;

namespace Nethereum.DevP2P.Peering
{
    /// <summary>
    /// Input to <see cref="DialScheduler.TryReserveSlotAsync"/>: a stable
    /// identifier for the peer plus its trusted-vs-untrusted classification.
    /// The scheduler does not interpret <see cref="Key"/> beyond using it for
    /// equality in dial-history and ratio tracking — callers may pass an
    /// enode URL, a node-id hex, an <c>ip:port</c> tuple, or any other
    /// canonical form, provided it is stable across <see cref="DialScheduler.TryReserveSlotAsync"/> /
    /// <see cref="DialScheduler.ReleaseSlot"/> / <see cref="DialScheduler.OnPeerConnected"/>
    /// pairings for the same logical peer.
    /// </summary>
    public sealed class DialCandidate
    {
        /// <summary>Stable identifier for the peer.</summary>
        public string Key { get; }

        /// <summary>
        /// True if the candidate is in the operator's trusted-peers list.
        /// Trusted candidates bypass the concurrent-dial cap and the
        /// inbound/outbound ratio cap (the trusted-connection flag).
        /// They still get dial-history suppression on the shorter
        /// <see cref="DialSchedulerOptions.TrustedHistoryExpiration"/>.
        /// </summary>
        public bool IsTrusted { get; }

        public DialCandidate(string key, bool isTrusted = false)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
            Key = key;
            IsTrusted = isTrusted;
        }
    }
}
