using System;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Read-only snapshot of a peer's score components. The score itself is
    /// a single double in [0, +∞) computed from recency × success-ratio
    /// (mirrors <see cref="Nethereum.DevP2P.NodeDb.PersistentPeerCache"/>'s
    /// internal Score function). Higher is better.
    ///
    /// <para>Stage 3's FetchRequestScheduler reads this when choosing a peer
    /// for a header / body / receipt batch — preferring higher-scored peers
    /// when in-flight counts are equal. Stage 2 surfaces the score via
    /// <see cref="PeerPoolManager.GetScore(string)"/>; Stage 3+ consumes it
    /// without taking a hard dependency on PersistentPeerCache.</para>
    /// </summary>
    /// <param name="SuccessCount">Total successful handshakes recorded against
    /// this enode across the lifetime of the persistent cache.</param>
    /// <param name="FailureCount">Total failed handshakes (transport errors +
    /// UselessPeerException + protocol rejects).</param>
    /// <param name="LastSeenUtc">UTC timestamp of the most recent successful
    /// handshake (DateTimeOffset.MinValue if never seen).</param>
    /// <param name="ComputedScore">recency × success-ratio. Halves every hour
    /// since LastSeenUtc; bounded below by 0.</param>
    public readonly record struct PeerScore(
        int SuccessCount,
        int FailureCount,
        DateTimeOffset LastSeenUtc,
        double ComputedScore)
    {
        /// <summary>Sentinel value for an enode the cache has no record of.</summary>
        public static PeerScore Unknown { get; } = new PeerScore(0, 0, DateTimeOffset.MinValue, 0.0);

        /// <summary>True when this score represents a peer the cache has never
        /// seen successfully — Unknown values should not be compared by score
        /// (a never-seen peer is not 'worse' than a known-failing peer; both
        /// are 'untrusted' but for different reasons).</summary>
        public bool IsUnknown => SuccessCount == 0 && FailureCount == 0;
    }
}
