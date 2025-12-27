using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.Web3;

namespace Nethereum.TokenServices.ERC20.Discovery
{
    public interface ITokenDiscoveryEngine
    {
        Task<TokenDiscoveryResult> DiscoverTokensAsync(
            IWeb3 web3,
            string accountAddress,
            long chainId,
            DiscoveryOptions options = null,
            IProgress<DiscoveryProgress> progress = null,
            CancellationToken cancellationToken = default);

        Task<TokenDiscoveryResult> DiscoverTokensAsync(
            IWeb3 web3,
            string accountAddress,
            IEnumerable<TokenInfo> tokenList,
            DiscoveryOptions options = null,
            IProgress<DiscoveryProgress> progress = null,
            CancellationToken cancellationToken = default);
    }

    public class DiscoveryOptions
    {
        public int PageSize { get; set; } = 100;
        public int StartFromIndex { get; set; } = 0;
        public bool IncludeZeroBalances { get; set; } = false;
        public int DelayBetweenPagesMs { get; set; } = 0;
    }

    public class DiscoveryProgress
    {
        public int CheckedTokens { get; set; }
        public int TotalTokens { get; set; }
        public int CurrentPage { get; set; }
        public int TokensWithBalance { get; set; }
        public string LastCheckedAddress { get; set; }

        public double PercentComplete => TotalTokens > 0
            ? (double)CheckedTokens / TotalTokens * 100
            : 0;

        public bool IsComplete => TotalTokens > 0 && CheckedTokens >= TotalTokens;
    }

    public class TokenDiscoveryResult
    {
        public bool Success { get; set; }
        public bool Completed { get; set; }
        public bool WasCancelled { get; set; }
        public string ErrorMessage { get; set; }
        public string StrategyName { get; set; }

        public int TokensChecked { get; set; }
        public int TotalTokens { get; set; }
        public int TokensWithBalance { get; set; }

        public List<TokenBalance> DiscoveredTokens { get; set; } = new List<TokenBalance>();

        public DiscoveryProgress FinalProgress { get; set; }

        public static TokenDiscoveryResult Empty(string strategyName = null) => new TokenDiscoveryResult
        {
            Success = true,
            Completed = true,
            TokensChecked = 0,
            TotalTokens = 0,
            StrategyName = strategyName
        };

        public static TokenDiscoveryResult Cancelled(DiscoveryProgress progress, List<TokenBalance> discovered, string strategyName = null) => new TokenDiscoveryResult
        {
            Success = true,
            Completed = false,
            WasCancelled = true,
            TokensChecked = progress?.CheckedTokens ?? 0,
            TotalTokens = progress?.TotalTokens ?? 0,
            TokensWithBalance = discovered?.Count ?? 0,
            DiscoveredTokens = discovered ?? new List<TokenBalance>(),
            FinalProgress = progress,
            StrategyName = strategyName
        };

        public static TokenDiscoveryResult Failed(string error, DiscoveryProgress progress, string strategyName = null) => new TokenDiscoveryResult
        {
            Success = false,
            Completed = false,
            ErrorMessage = error,
            FinalProgress = progress,
            StrategyName = strategyName
        };

        public static TokenDiscoveryResult Successful(List<TokenBalance> tokens, DiscoveryProgress progress, string strategyName = null) => new TokenDiscoveryResult
        {
            Success = true,
            Completed = true,
            TokensChecked = progress?.CheckedTokens ?? tokens?.Count ?? 0,
            TotalTokens = progress?.TotalTokens ?? tokens?.Count ?? 0,
            TokensWithBalance = tokens?.Count ?? 0,
            DiscoveredTokens = tokens ?? new List<TokenBalance>(),
            FinalProgress = progress,
            StrategyName = strategyName
        };
    }
}
