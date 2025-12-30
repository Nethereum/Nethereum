using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Wallet.Services.Tokens.Models
{
    public class MultiChainRefreshResult
    {
        public Dictionary<long, ChainRefreshResult> ChainResults { get; set; }
            = new Dictionary<long, ChainRefreshResult>();

        public int TotalChainsAttempted => ChainResults.Count;
        public int SuccessfulChains => ChainResults.Values.Count(r => r.Success);
        public int FailedChains => ChainResults.Values.Count(r => !r.Success);
        public bool AllSuccessful => ChainResults.Values.All(r => r.Success);
        public bool AnySuccessful => ChainResults.Values.Any(r => r.Success);

        public int TotalTokensUpdated => ChainResults.Values.Sum(r => r.TokensUpdated);
        public int TotalNewTokensFound => ChainResults.Values.Sum(r => r.NewTokensFound);

        public List<(long ChainId, string Error, ChainScanErrorType? ErrorType)> GetErrors()
        {
            return ChainResults
                .Where(kvp => !kvp.Value.Success)
                .Select(kvp => (kvp.Key, kvp.Value.ErrorMessage, kvp.Value.ErrorType))
                .ToList();
        }
    }

    public class ChainRefreshResult
    {
        public long ChainId { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public ChainScanErrorType? ErrorType { get; set; }
        public int TokensUpdated { get; set; }
        public int NewTokensFound { get; set; }
        public bool PricesUpdated { get; set; }

        public static ChainRefreshResult Succeeded(long chainId, int tokensUpdated, int newTokensFound, bool pricesUpdated)
        {
            return new ChainRefreshResult
            {
                ChainId = chainId,
                Success = true,
                TokensUpdated = tokensUpdated,
                NewTokensFound = newTokensFound,
                PricesUpdated = pricesUpdated
            };
        }

        public static ChainRefreshResult Failed(long chainId, string error, ChainScanErrorType? errorType = null)
        {
            return new ChainRefreshResult
            {
                ChainId = chainId,
                Success = false,
                ErrorMessage = error,
                ErrorType = errorType
            };
        }
    }
}
