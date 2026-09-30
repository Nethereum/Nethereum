using System;

namespace Nethereum.DevP2P.Peering
{
    public sealed class DialSchedulerOptions
    {
        public int MaxActiveDials { get; set; } = 16;

        public TimeSpan DialHistoryExpiration { get; set; } = TimeSpan.FromMinutes(5);

        public TimeSpan TrustedHistoryExpiration { get; set; } = TimeSpan.FromSeconds(30);
    }
}
