using System;

namespace Nethereum.TokenServices.Refresh
{
    public enum RefreshType
    {
        Platforms,
        TokenList,
        CoinMapping
    }

    public enum RefreshPriority
    {
        Critical = 0,
        High = 1,
        Normal = 2,
        Low = 3
    }

    public class RefreshJob
    {
        public RefreshType Type { get; set; }
        public long? ChainId { get; set; }
        public RefreshPriority Priority { get; set; }
        public int RetryCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastAttempt { get; set; }
        public string LastError { get; set; }

        public static RefreshJob ForPlatforms(RefreshPriority priority = RefreshPriority.Normal)
        {
            return new RefreshJob
            {
                Type = RefreshType.Platforms,
                Priority = priority
            };
        }

        public static RefreshJob ForTokenList(long chainId, RefreshPriority priority = RefreshPriority.Normal)
        {
            return new RefreshJob
            {
                Type = RefreshType.TokenList,
                ChainId = chainId,
                Priority = priority
            };
        }

        public static RefreshJob ForCoinMapping(long chainId, RefreshPriority priority = RefreshPriority.Normal)
        {
            return new RefreshJob
            {
                Type = RefreshType.CoinMapping,
                ChainId = chainId,
                Priority = priority
            };
        }

        public string GetCacheKey()
        {
            return Type switch
            {
                RefreshType.Platforms => "coingecko:platforms",
                RefreshType.TokenList => $"tokenlist:{ChainId}",
                RefreshType.CoinMapping => $"coinslist:{ChainId}",
                _ => throw new InvalidOperationException($"Unknown refresh type: {Type}")
            };
        }

        public string GetJobId()
        {
            return Type switch
            {
                RefreshType.Platforms => "platforms",
                RefreshType.TokenList => $"tokenlist:{ChainId}",
                RefreshType.CoinMapping => $"coins:{ChainId}",
                _ => throw new InvalidOperationException($"Unknown refresh type: {Type}")
            };
        }
    }
}
