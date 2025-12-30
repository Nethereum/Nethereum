using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Wallet.Services.Tokens.Models;

namespace Nethereum.Wallet.Services.Tokens
{
    public interface ITokenManagementService
    {
        // === DISCOVERY (One-time, resumable) ===
        Task<DiscoveryResult> StartOrResumeDiscoveryAsync(
            string accountAddress,
            long chainId,
            IProgress<TokenDiscoveryProgress> progress = null,
            CancellationToken cancellationToken = default);

        // === MULTI-CHAIN DISCOVERY (Parallel across chains) ===
        Task<MultiChainDiscoveryResult> DiscoverAllChainsAsync(
            string accountAddress,
            IEnumerable<long> chainIds,
            IProgress<MultiChainDiscoveryProgress> progress = null,
            CancellationToken cancellationToken = default);

        Task<TokenDiscoveryProgress> GetDiscoveryProgressAsync(string accountAddress, long chainId);
        Task<bool> IsDiscoveryCompleteAsync(string accountAddress, long chainId);
        Task ResetDiscoveryAsync(string accountAddress, long chainId);
        Task ResetDiscoveryAsync(string accountAddress, IEnumerable<long> chainIds);
        Task<int> GetExpectedTokenCountAsync(long chainId);
        Task<Dictionary<long, int>> GetExpectedTokenCountsAsync(IEnumerable<long> chainIds);

        // === EVENT UPDATES (Incremental) ===
        Task<EventScanResult> ScanForBalanceChangesAsync(
            string accountAddress,
            long chainId,
            CancellationToken cancellationToken = default);

        // === PRICE DECORATION (Non-blocking) ===
        Task DecorateWithPricesAsync(string accountAddress, long chainId);

        // === BATCH PRICE DECORATION (Optimized for multiple accounts) ===
        Task DecorateWithPricesAsync(IEnumerable<string> accountAddresses, IEnumerable<long> chainIds);

        // === SMART REFRESH (Events + Prices, prices fail silently) ===
        Task<RefreshResult> RefreshAsync(
            string accountAddress,
            long chainId,
            CancellationToken cancellationToken = default);

        // === MULTI-CHAIN REFRESH (Continues on failure, parallel execution) ===
        Task<MultiChainRefreshResult> RefreshAllChainsAsync(
            string accountAddress,
            IEnumerable<long> chainIds,
            CancellationToken cancellationToken = default);

        Task<MultiChainRefreshResult> RefreshAllChainsAsync(
            IEnumerable<string> accountAddresses,
            IEnumerable<long> chainIds,
            CancellationToken cancellationToken = default);

        // === FORCE REFRESH (All balances via Multicall, not event-based) ===
        Task<ForceRefreshResult> ForceRefreshAllBalancesAsync(
            string accountAddress,
            long chainId,
            CancellationToken cancellationToken = default);

        Task<MultiChainForceRefreshResult> ForceRefreshAllChainsAsync(
            IEnumerable<string> accountAddresses,
            IEnumerable<long> chainIds,
            IProgress<MultiChainForceRefreshProgress> progress = null,
            CancellationToken cancellationToken = default);

        // === STATUS ===
        Task<ChainScanStatus> GetChainStatusAsync(string accountAddress, long chainId);
        Task<Dictionary<long, ChainScanStatus>> GetAllChainStatusesAsync(string accountAddress, IEnumerable<long> chainIds);

        // === DATA ACCESS ===
        Task<List<AccountToken>> GetAccountTokensAsync(string accountAddress, long chainId, bool includeHidden = false);
        Task<AccountToken> GetTokenAsync(string accountAddress, long chainId, string contractAddress);

        // === CUSTOM TOKENS ===
        Task<bool> AddCustomTokenAsync(long chainId, string contractAddress);
        Task<bool> AddCustomTokenAsync(long chainId, CustomToken token);
        Task<bool> UpdateCustomTokenAsync(long chainId, CustomToken token);
        Task<bool> DeleteCustomTokenAsync(long chainId, string contractAddress);

        // === TOKEN SETTINGS ===
        Task SetTokenHiddenAsync(string accountAddress, long chainId, string contractAddress, bool hidden);

        // === CACHE ===
        Task InitializeCacheAsync(IEnumerable<long> chainIds);

        // === EVENTS ===
        event EventHandler<TokensUpdatedEventArgs> TokensUpdated;
    }

    public class TokensUpdatedEventArgs : EventArgs
    {
        public string AccountAddress { get; set; }
        public long ChainId { get; set; }
        public List<AccountToken> Tokens { get; set; }
        public TokenUpdateType UpdateType { get; set; }
    }

    public enum TokenUpdateType
    {
        Discovery,
        BalanceChange,
        PriceUpdate,
        Custom
    }
}
