using System.Collections.Generic;
using System.Numerics;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.MultiAccount.Models
{
    public class MultiAccountRefreshResult
    {
        public bool Success { get; set; }
        public bool WasCancelled { get; set; }
        public string ErrorMessage { get; set; }

        public int TokensUpdated { get; set; }
        public int NewTokensFound { get; set; }

        public Dictionary<long, ChainRefreshResult> ChainResults { get; set; } = new Dictionary<long, ChainRefreshResult>();

        public static MultiAccountRefreshResult Successful(
            int tokensUpdated,
            int newTokensFound,
            Dictionary<long, ChainRefreshResult> chainResults)
        {
            return new MultiAccountRefreshResult
            {
                Success = true,
                TokensUpdated = tokensUpdated,
                NewTokensFound = newTokensFound,
                ChainResults = chainResults ?? new Dictionary<long, ChainRefreshResult>()
            };
        }

        public static MultiAccountRefreshResult Cancelled()
        {
            return new MultiAccountRefreshResult
            {
                Success = true,
                WasCancelled = true
            };
        }

        public static MultiAccountRefreshResult Failed(string error)
        {
            return new MultiAccountRefreshResult
            {
                Success = false,
                ErrorMessage = error
            };
        }
    }

    public class ChainRefreshResult
    {
        public long ChainId { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int TokensUpdated { get; set; }
        public int NewTokensFound { get; set; }
        public BigInteger FromBlock { get; set; }
        public BigInteger ToBlock { get; set; }
        public List<TokenBalance> UpdatedBalances { get; set; } = new List<TokenBalance>();
    }
}
