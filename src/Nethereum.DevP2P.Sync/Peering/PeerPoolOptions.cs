using System;

namespace Nethereum.DevP2P.Sync.Peering
{
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
        int IPv6SubnetPrefix = 64,
        TimeSpan TrustedRedialInterval = default)
    {
        public TimeSpan EffectiveTrustedRedialInterval =>
            TrustedRedialInterval <= TimeSpan.Zero ? TimeSpan.FromSeconds(15) : TrustedRedialInterval;

        public TimeSpan EffectiveDialCooldown =>
            DialCooldown == default ? TimeSpan.FromSeconds(35) : DialCooldown;

        public TimeSpan EffectiveDialTimeout =>
            DialTimeout == default ? TimeSpan.FromSeconds(15) : DialTimeout;

        public TimeSpan EffectiveReseedInterval =>
            ReseedInterval == default ? TimeSpan.FromSeconds(30) : ReseedInterval;

        public TimeSpan EffectiveMinDialIntervalPerHost =>
            MinDialIntervalPerHost == default ? TimeSpan.FromSeconds(30) : MinDialIntervalPerHost;

        public TimeSpan EffectivePerHostReDialGate =>
            EffectiveDialCooldown >= EffectiveMinDialIntervalPerHost
                ? EffectiveDialCooldown
                : EffectiveMinDialIntervalPerHost;
    }
}
