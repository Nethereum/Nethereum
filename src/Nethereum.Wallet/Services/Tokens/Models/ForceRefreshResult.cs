using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Wallet.Services.Tokens.Models
{
    public class ForceRefreshResult
    {
        public bool Success { get; set; }
        public int TotalTokens { get; set; }
        public int UpdatedTokens { get; set; }
        public string ErrorMessage { get; set; }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public static ForceRefreshResult Succeeded(int total, int updated) => new ForceRefreshResult
        {
            Success = true,
            TotalTokens = total,
            UpdatedTokens = updated
        };

        public static ForceRefreshResult Failed(string error) => new ForceRefreshResult
        {
            Success = false,
            ErrorMessage = error
        };
    }

    public class MultiChainForceRefreshResult
    {
        public Dictionary<long, ChainForceRefreshResult> ChainResults { get; set; }
            = new Dictionary<long, ChainForceRefreshResult>();

        public List<(string Account, long ChainId, string Error)> Errors { get; set; }
            = new List<(string Account, long ChainId, string Error)>();

        public int TotalChainsAttempted => ChainResults.Count;
        public int SuccessfulChains => ChainResults.Values.Count(r => r.Success);
        public int FailedChains => ChainResults.Values.Count(r => !r.Success);
        public bool AllSuccessful => ChainResults.Values.All(r => r.Success);
        public bool AnySuccessful => ChainResults.Values.Any(r => r.Success);

        public int TotalTokensUpdated => ChainResults.Values.Sum(r => r.UpdatedTokens);
    }

    public class ChainForceRefreshResult
    {
        public long ChainId { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int TotalTokens { get; set; }
        public int UpdatedTokens { get; set; }

        public static ChainForceRefreshResult Succeeded(long chainId, int totalTokens, int updatedTokens)
        {
            return new ChainForceRefreshResult
            {
                ChainId = chainId,
                Success = true,
                TotalTokens = totalTokens,
                UpdatedTokens = updatedTokens
            };
        }

        public static ChainForceRefreshResult Failed(long chainId, string error)
        {
            return new ChainForceRefreshResult
            {
                ChainId = chainId,
                Success = false,
                ErrorMessage = error
            };
        }
    }

    public class MultiChainForceRefreshProgress
    {
        public int TotalChains { get; set; }
        public int TotalAccounts { get; set; }
        public int CompletedOperations { get; set; }
        public int TotalOperations => TotalChains * TotalAccounts;
        public int PercentComplete => TotalOperations > 0
            ? (int)((double)CompletedOperations / TotalOperations * 100)
            : 0;

        public MultiChainForceRefreshProgress Clone() => new MultiChainForceRefreshProgress
        {
            TotalChains = TotalChains,
            TotalAccounts = TotalAccounts,
            CompletedOperations = CompletedOperations
        };
    }
}
