using System;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>Tunable knobs for <see cref="IPeerPool"/>.</summary>
    /// <param name="TargetPeerCount">Steady-state pool size. Dialer fires
    /// until ActivePeers.Count >= TargetPeerCount, then idles.</param>
    /// <param name="MaxConcurrentDials">Cap on simultaneous in-flight dial
    /// attempts.</param>
    /// <param name="DialCooldown">Cooldown after a dial attempt (success or
    /// failure) before the same enode is re-dialed. Sentinel default = 35s.</param>
    /// <param name="DialTimeout">Per-dial timeout: connect + RLPx handshake
    /// + status exchange must all complete within this. Sentinel default = 15s.</param>
    /// <param name="MinPeerLatestBlock">Useless-peer floor. Peers reporting
    /// PeerLatestBlock below this are rejected during handshake. 0 disables
    /// the floor.</param>
    /// <param name="ReseedInterval">How often the reseed loop refreshes its
    /// candidate stream from external discovery sources while the pool is
    /// running. Sentinel default = 30s.</param>
    /// <param name="CandidateQueueCapacity">Bounded capacity of the candidate
    /// channel that feeds the dialer. Excess writes are dropped (we always
    /// prefer fresh discovery over stale queue contents).</param>
    /// <param name="MinDialIntervalPerHost">Hard per-host re-dial floor.
    /// After a dial attempt completes (success or failure) the same enode
    /// will not be re-dialed for at least this interval. Layered with
    /// <see cref="DialCooldown"/>; the effective gate is the larger of the
    /// two. Sentinel default = 30s.</param>
    /// <param name="DialBudgetPerSecond">Outbound dial rate limit. Implemented
    /// as a token bucket refilled at this many tokens per second, capped at
    /// the same value (i.e. burst = budget). The dial loop awaits a token
    /// before each handshake attempt. 0 disables rate limiting. Default = 5.</param>
    /// <param name="MaxPeersPerIPv4Subnet">Cap on concurrent admitted peers
    /// sharing the same IPv4 /<see cref="IPv4SubnetPrefix"/> subnet. Eclipse
    /// defence: per-host caps alone don't stop a /24 owner from filling the
    /// outbound pool with up to subnet-size peers. Default 10;
    /// 0 disables the v4 subnet cap.</param>
    /// <param name="IPv4SubnetPrefix">IPv4 prefix length used to group peers
    /// into subnets (default 24 = /24).</param>
    /// <param name="MaxPeersPerIPv6Subnet">Cap on concurrent admitted peers
    /// sharing the same IPv6 /<see cref="IPv6SubnetPrefix"/> prefix. 0
    /// disables the v6 subnet cap.</param>
    /// <param name="IPv6SubnetPrefix">IPv6 prefix length used to group peers
    /// into subnets (default 64 = /64; interface IDs are typically host-
    /// assigned below the /64 boundary, so this is the meaningful "owner"
    /// boundary).</param>
    public sealed record PeerPoolOptions(
        int TargetPeerCount = 16,
        int MaxConcurrentDials = 10,
        TimeSpan DialCooldown = default,
        TimeSpan DialTimeout = default,
        ulong MinPeerLatestBlock = 0,
        TimeSpan ReseedInterval = default,
        int CandidateQueueCapacity = 2048,
        TimeSpan MinDialIntervalPerHost = default,
        int DialBudgetPerSecond = 5,
        int MaxPeersPerIPv4Subnet = 10,
        int IPv4SubnetPrefix = 24,
        int MaxPeersPerIPv6Subnet = 10,
        int IPv6SubnetPrefix = 64)
    {
        /// <summary>Default 35s if <see cref="DialCooldown"/> was left at the
        /// sentinel (zero); otherwise the configured value.</summary>
        public TimeSpan EffectiveDialCooldown =>
            DialCooldown == default ? TimeSpan.FromSeconds(35) : DialCooldown;

        /// <summary>Default 15s if <see cref="DialTimeout"/> was left at the
        /// sentinel (zero); otherwise the configured value.</summary>
        public TimeSpan EffectiveDialTimeout =>
            DialTimeout == default ? TimeSpan.FromSeconds(15) : DialTimeout;

        /// <summary>Default 30s if <see cref="ReseedInterval"/> was left at the
        /// sentinel (zero); otherwise the configured value.</summary>
        public TimeSpan EffectiveReseedInterval =>
            ReseedInterval == default ? TimeSpan.FromSeconds(30) : ReseedInterval;

        /// <summary>Default 30s if <see cref="MinDialIntervalPerHost"/> was
        /// left at the sentinel (zero); otherwise the configured value.</summary>
        public TimeSpan EffectiveMinDialIntervalPerHost =>
            MinDialIntervalPerHost == default ? TimeSpan.FromSeconds(30) : MinDialIntervalPerHost;

        /// <summary>Returns the larger of <see cref="EffectiveDialCooldown"/>
        /// and <see cref="EffectiveMinDialIntervalPerHost"/>. Used by the dial
        /// loop as the per-host re-dial gate.</summary>
        public TimeSpan EffectivePerHostReDialGate =>
            EffectiveDialCooldown >= EffectiveMinDialIntervalPerHost
                ? EffectiveDialCooldown
                : EffectiveMinDialIntervalPerHost;
    }
}
