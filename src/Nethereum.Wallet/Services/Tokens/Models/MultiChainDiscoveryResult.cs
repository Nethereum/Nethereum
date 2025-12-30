using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Wallet.Services.Tokens.Models
{
    public class MultiChainDiscoveryResult
    {
        public Dictionary<long, ChainDiscoveryResult> ChainResults { get; set; }
            = new Dictionary<long, ChainDiscoveryResult>();

        public int TotalChainsAttempted => ChainResults.Count;
        public int CompletedChains => ChainResults.Values.Count(r => r.Completed);
        public int FailedChains => ChainResults.Values.Count(r => !r.Success);
        public int InProgressChains => ChainResults.Values.Count(r => r.Success && !r.Completed);
        public bool AllCompleted => ChainResults.Values.All(r => r.Completed);
        public bool AnyCompleted => ChainResults.Values.Any(r => r.Completed);

        public int TotalTokensFound => ChainResults.Values.Sum(r => r.TokensFound);
        public int TotalTokensChecked => ChainResults.Values.Sum(r => r.TokensChecked);

        public List<(long ChainId, string Error)> GetErrors()
        {
            return ChainResults
                .Where(kvp => !kvp.Value.Success)
                .Select(kvp => (kvp.Key, kvp.Value.ErrorMessage))
                .ToList();
        }
    }

    public class ChainDiscoveryResult
    {
        public long ChainId { get; set; }
        public bool Success { get; set; }
        public bool Completed { get; set; }
        public bool WasCancelled { get; set; }
        public string ErrorMessage { get; set; }
        public int TokensFound { get; set; }
        public int TokensChecked { get; set; }
        public int TotalTokens { get; set; }

        public static ChainDiscoveryResult FromDiscoveryResult(long chainId, DiscoveryResult result)
        {
            return new ChainDiscoveryResult
            {
                ChainId = chainId,
                Success = result.Success,
                Completed = result.Completed,
                WasCancelled = result.WasCancelled,
                ErrorMessage = result.ErrorMessage,
                TokensFound = result.TokensFound,
                TokensChecked = result.TokensChecked,
                TotalTokens = result.TotalTokens
            };
        }

        public static ChainDiscoveryResult Failed(long chainId, string error)
        {
            return new ChainDiscoveryResult
            {
                ChainId = chainId,
                Success = false,
                Completed = false,
                ErrorMessage = error
            };
        }

        public static ChainDiscoveryResult AlreadyComplete(long chainId)
        {
            return new ChainDiscoveryResult
            {
                ChainId = chainId,
                Success = true,
                Completed = true
            };
        }
    }

    public class MultiChainDiscoveryProgress
    {
        public int TotalChains { get; set; }
        public int CompletedChains { get; set; }
        public Dictionary<long, TokenDiscoveryProgress> ChainProgress { get; set; }
            = new Dictionary<long, TokenDiscoveryProgress>();

        public double OverallPercentComplete => TotalChains > 0
            ? ChainProgress.Values.Sum(p => p.PercentComplete) / TotalChains
            : 0;
    }
}
