using System;

namespace Nethereum.DevP2P.Sync.Peering
{
    public readonly record struct PeerScore(
        int SuccessCount,
        int FailureCount,
        DateTimeOffset LastSeenUtc,
        double ComputedScore)
    {
        public static PeerScore Unknown { get; } = new PeerScore(0, 0, DateTimeOffset.MinValue, 0.0);

        public bool IsUnknown => SuccessCount == 0 && FailureCount == 0;
    }
}
