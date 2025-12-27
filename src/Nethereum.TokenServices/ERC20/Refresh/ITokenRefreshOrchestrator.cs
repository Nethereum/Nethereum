using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.Web3;

namespace Nethereum.TokenServices.ERC20.Refresh
{
    public interface ITokenRefreshOrchestrator
    {
        Task<TokenRefreshResult> RefreshAsync(
            IWeb3 web3,
            string accountAddress,
            long chainId,
            RefreshOptions options,
            CancellationToken cancellationToken = default);

        Task<TokenRefreshResult> RefreshMultipleAccountsAsync(
            IWeb3 web3,
            IEnumerable<string> accountAddresses,
            long chainId,
            RefreshOptions options,
            CancellationToken cancellationToken = default);
    }

    public class RefreshOptions
    {
        public BigInteger FromBlock { get; set; }
        public BigInteger? ToBlock { get; set; }
        public int ReorgSafetyBuffer { get; set; } = 50;
        public bool IncludeNativeToken { get; set; } = true;
    }

    public class TokenRefreshResult
    {
        public bool Success { get; set; }
        public bool EventScanSuccess { get; set; }
        public string EventScanError { get; set; }

        public BigInteger FromBlock { get; set; }
        public BigInteger ToBlock { get; set; }

        public List<TokenBalance> UpdatedBalances { get; set; } = new List<TokenBalance>();
        public List<string> AffectedTokenAddresses { get; set; } = new List<string>();
        public int TokensUpdated { get; set; }
        public int NewTokensFound { get; set; }

        public TokenBalance NativeBalance { get; set; }

        public bool HasEventError => !string.IsNullOrEmpty(EventScanError);

        public static TokenRefreshResult NoChanges(BigInteger fromBlock, BigInteger toBlock) => new TokenRefreshResult
        {
            Success = true,
            EventScanSuccess = true,
            FromBlock = fromBlock,
            ToBlock = toBlock
        };

        public static TokenRefreshResult Failed(string error) => new TokenRefreshResult
        {
            Success = false,
            EventScanSuccess = false,
            EventScanError = error
        };
    }

    public class MultiAccountRefreshResult
    {
        public bool OverallSuccess { get; set; }
        public Dictionary<string, TokenRefreshResult> ResultsByAccount { get; set; }
            = new Dictionary<string, TokenRefreshResult>(StringComparer.OrdinalIgnoreCase);
        public List<string> Errors { get; set; } = new List<string>();

        public int TotalTokensUpdated => ResultsByAccount.Values.Sum(r => r.TokensUpdated);
        public int TotalNewTokensFound => ResultsByAccount.Values.Sum(r => r.NewTokensFound);
    }
}
