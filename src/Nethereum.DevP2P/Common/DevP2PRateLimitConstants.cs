namespace Nethereum.DevP2P.Common
{
    public static class DevP2PRateLimitConstants
    {
        public const int InboundPacketsPerSecondPerIp = 9;

        public const int InboundBurstCapacity = 9;

        public const int KnownSourcesCacheSize = 500;

        public const int MaxBannedIpsCached = 50;
    }
}
