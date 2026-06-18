namespace Nethereum.DevP2P.Common
{
    /// <summary>
    /// Named rate-limit constants shared across DevP2P subsystems. Numeric values
    /// are cited from the sigp/discv5 reference implementation; the discv5 wire
    /// spec does not publish numeric thresholds at this layer, so the design
    /// choice to apply a per-IP token bucket is itself sigp/discv5-derived.
    /// </summary>
    public static class DevP2PRateLimitConstants
    {
        /// <summary>
        /// Refill rate for the per-source-IP inbound discv5 bucket, in packets per
        /// second. Cited from sigp/discv5 <c>src/config.rs:113</c>
        /// (<c>.ip_n_every(9, Duration::from_secs(1))</c>).
        /// Source: https://raw.githubusercontent.com/sigp/discv5/master/src/config.rs
        /// </summary>
        public const int InboundPacketsPerSecondPerIp = 9;

        /// <summary>
        /// Burst capacity for the per-source-IP inbound discv5 bucket. sigp/discv5
        /// uses the same N for both steady refill rate and burst.
        /// Source: https://raw.githubusercontent.com/sigp/discv5/master/src/config.rs
        /// </summary>
        public const int InboundBurstCapacity = 9;

        /// <summary>
        /// LRU cap on the number of distinct source-IP buckets tracked by the
        /// inbound discv5 filter. Cited from sigp/discv5
        /// <c>src/socket/filter/mod.rs:21</c> (<c>const KNOWN_ADDRS_SIZE: usize = 500</c>).
        /// Source: https://raw.githubusercontent.com/sigp/discv5/master/src/socket/filter/mod.rs
        /// </summary>
        public const int KnownSourcesCacheSize = 500;

        /// <summary>
        /// LRU cap on banned-IP slots in the inbound discv5 filter. Cited from
        /// sigp/discv5 <c>src/socket/filter/mod.rs:22</c>
        /// (<c>const BANNED_NODES_SIZE: usize = 50</c>).
        /// Source: https://raw.githubusercontent.com/sigp/discv5/master/src/socket/filter/mod.rs
        /// </summary>
        public const int MaxBannedIpsCached = 50;

        /// <summary>
        /// Burst capacity of the per-peer RLPx Ping bucket. The RLPx base protocol
        /// (https://github.com/ethereum/devp2p/blob/master/rlpx.md) is silent on
        /// Ping cadence and flood thresholds; the threshold is derived from
        /// go-ethereum's reference cadence of 15 s between Pings
        /// (<c>p2p/peer.go pingInterval = 15 * time.Second</c>). A 4-frame burst
        /// covers retransmits and clock skew without enabling sustained flood.
        /// </summary>
        public const int MaxPingsPerWindow = 4;

        /// <summary>
        /// Refill window for the per-peer RLPx Ping bucket, in seconds. Equal to
        /// one third of go-ethereum's reference <c>pingInterval = 15 s</c>
        /// (<c>p2p/peer.go</c>), giving a 3x safety margin against legitimate
        /// jitter while still bounding Pong-emission CPU at ~0.8 frames/sec on
        /// an attacker peer that pegs the bucket empty.
        /// </summary>
        public const int PingWindowSeconds = 5;
    }
}
