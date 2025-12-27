using System.Collections.Generic;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.MultiAccount.Models
{
    public class MultiAccountScanResult
    {
        public bool Success { get; set; }
        public bool WasCancelled { get; set; }
        public string ErrorMessage { get; set; }

        public int TotalTokensFound { get; set; }
        public int TotalAccountsScanned { get; set; }
        public int TotalChainsScanned { get; set; }

        public Dictionary<long, ChainScanResult> ChainResults { get; set; } = new Dictionary<long, ChainScanResult>();
        public Dictionary<string, AccountScanResult> AccountResults { get; set; } = new Dictionary<string, AccountScanResult>();

        public static MultiAccountScanResult Successful(
            int tokensFound,
            Dictionary<long, ChainScanResult> chainResults,
            Dictionary<string, AccountScanResult> accountResults)
        {
            return new MultiAccountScanResult
            {
                Success = true,
                TotalTokensFound = tokensFound,
                TotalChainsScanned = chainResults?.Count ?? 0,
                TotalAccountsScanned = accountResults?.Count ?? 0,
                ChainResults = chainResults ?? new Dictionary<long, ChainScanResult>(),
                AccountResults = accountResults ?? new Dictionary<string, AccountScanResult>()
            };
        }

        public static MultiAccountScanResult Cancelled(
            Dictionary<long, ChainScanResult> chainResults = null,
            Dictionary<string, AccountScanResult> accountResults = null)
        {
            return new MultiAccountScanResult
            {
                Success = true,
                WasCancelled = true,
                ChainResults = chainResults ?? new Dictionary<long, ChainScanResult>(),
                AccountResults = accountResults ?? new Dictionary<string, AccountScanResult>()
            };
        }

        public static MultiAccountScanResult Failed(string error)
        {
            return new MultiAccountScanResult
            {
                Success = false,
                ErrorMessage = error
            };
        }
    }

    public class ChainScanResult
    {
        public long ChainId { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int TokensFound { get; set; }
        public int TokensChecked { get; set; }
        public string StrategyUsed { get; set; }
    }

    public class AccountScanResult
    {
        public string AccountAddress { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int TokensFound { get; set; }
        public Dictionary<long, List<TokenBalance>> TokensByChain { get; set; } = new Dictionary<long, List<TokenBalance>>();
    }
}
