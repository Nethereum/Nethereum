using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20;
using Nethereum.TokenServices.ERC20.Balances;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.TokenServices.ERC20.Pricing;
using Nethereum.TokenServices.ERC20.Refresh;
using Nethereum.TokenServices.MultiAccount.Models;
using MultiAccountRefreshResultModel = Nethereum.TokenServices.MultiAccount.Models.MultiAccountRefreshResult;
using Nethereum.Web3;

namespace Nethereum.TokenServices.MultiAccount
{
    public class MultiAccountTokenService : IMultiAccountTokenService
    {
        private readonly IErc20TokenService _tokenService;
        private readonly ITokenRefreshOrchestrator _refreshOrchestrator;

        public MultiAccountTokenService(
            IErc20TokenService tokenService,
            ITokenRefreshOrchestrator refreshOrchestrator = null)
        {
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _refreshOrchestrator = refreshOrchestrator;
        }

        public async Task<MultiAccountScanResult> ScanAsync(
            IEnumerable<string> accounts,
            IEnumerable<long> chainIds,
            Func<long, IWeb3> web3Factory,
            IDiscoveryStrategy strategy,
            MultiAccountScanOptions options = null,
            IProgress<MultiAccountProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (accounts == null) throw new ArgumentNullException(nameof(accounts));
            if (chainIds == null) throw new ArgumentNullException(nameof(chainIds));
            if (web3Factory == null) throw new ArgumentNullException(nameof(web3Factory));
            if (strategy == null) throw new ArgumentNullException(nameof(strategy));

            options ??= new MultiAccountScanOptions();

            var accountList = accounts.ToList();
            var chainList = chainIds.ToList();

            if (!accountList.Any() || !chainList.Any())
            {
                return MultiAccountScanResult.Successful(0, new Dictionary<long, ChainScanResult>(), new Dictionary<string, AccountScanResult>());
            }

            var progressState = new MultiAccountProgress
            {
                TotalAccounts = accountList.Count,
                TotalChains = chainList.Count
            };

            foreach (var chainId in chainList)
            {
                var expectedTokens = await strategy.GetExpectedTokenCountAsync(chainId);
                progressState.ChainProgress[chainId] = new ChainProgress
                {
                    ChainId = chainId,
                    TotalTokens = expectedTokens * accountList.Count
                };
            }

            ReportProgress(progress, progressState);

            var chainResults = new ConcurrentDictionary<long, ChainScanResult>();
            var accountResults = new ConcurrentDictionary<string, AccountScanResult>();
            var progressLock = new object();

            var semaphore = new SemaphoreSlim(options.MaxParallelChains);
            var chainTasks = chainList.Select(async chainId =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return 0;

                    return await ScanChainAsync(
                        chainId,
                        accountList,
                        web3Factory,
                        strategy,
                        options,
                        progressState,
                        chainResults,
                        accountResults,
                        progressLock,
                        progress,
                        cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            var results = await Task.WhenAll(chainTasks);
            var totalTokensFound = results.Sum();

            if (cancellationToken.IsCancellationRequested)
            {
                return MultiAccountScanResult.Cancelled(
                    chainResults.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    accountResults.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
            }

            return MultiAccountScanResult.Successful(
                totalTokensFound,
                chainResults.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                accountResults.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        public async Task<MultiAccountRefreshResultModel> RefreshBalancesAsync(
            IEnumerable<string> accounts,
            IEnumerable<long> chainIds,
            Func<long, IWeb3> web3Factory,
            Func<string, long, ulong?> getLastScannedBlock,
            MultiAccountScanOptions options = null,
            IProgress<MultiAccountProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (_refreshOrchestrator == null)
            {
                return MultiAccountRefreshResultModel.Failed("Refresh orchestrator not configured");
            }

            if (accounts == null) throw new ArgumentNullException(nameof(accounts));
            if (chainIds == null) throw new ArgumentNullException(nameof(chainIds));
            if (web3Factory == null) throw new ArgumentNullException(nameof(web3Factory));

            options ??= new MultiAccountScanOptions();

            var accountList = accounts.ToList();
            var chainList = chainIds.ToList();

            if (!accountList.Any() || !chainList.Any())
            {
                return MultiAccountRefreshResultModel.Successful(0, 0, new Dictionary<long, ChainRefreshResult>());
            }

            var progressState = new MultiAccountProgress
            {
                TotalAccounts = accountList.Count,
                TotalChains = chainList.Count
            };

            foreach (var chainId in chainList)
            {
                progressState.ChainProgress[chainId] = new ChainProgress
                {
                    ChainId = chainId,
                    TotalTokens = 0
                };
            }

            ReportProgress(progress, progressState);

            var chainResults = new ConcurrentDictionary<long, ChainRefreshResult>();
            var totalTokensUpdated = 0;
            var totalNewTokens = 0;
            var progressLock = new object();

            var semaphore = new SemaphoreSlim(options.MaxParallelChains);
            var chainTasks = chainList.Select(async chainId =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return (0, 0);

                    var web3 = web3Factory(chainId);
                    if (web3 == null)
                    {
                        chainResults[chainId] = new ChainRefreshResult
                        {
                            ChainId = chainId,
                            Success = false,
                            ErrorMessage = "No Web3 provider available"
                        };
                        return (0, 0);
                    }

                    lock (progressLock)
                    {
                        progressState.CurrentChainId = chainId;
                        progressState.ChainProgress[chainId].IsScanning = true;
                        ReportProgress(progress, progressState);
                    }

                    var fromBlock = BigInteger.Zero;
                    foreach (var account in accountList)
                    {
                        var lastBlock = getLastScannedBlock?.Invoke(account, chainId);
                        if (lastBlock.HasValue && lastBlock.Value > 0)
                        {
                            if (fromBlock == BigInteger.Zero || lastBlock.Value < (ulong)fromBlock)
                            {
                                fromBlock = new BigInteger(lastBlock.Value);
                            }
                        }
                    }

                    var refreshOptions = new RefreshOptions
                    {
                        FromBlock = fromBlock,
                        IncludeNativeToken = options.IncludeNativeToken
                    };

                    var refreshResult = await _refreshOrchestrator.RefreshMultipleAccountsAsync(
                        web3,
                        accountList,
                        chainId,
                        refreshOptions,
                        cancellationToken);

                    lock (progressLock)
                    {
                        progressState.ChainProgress[chainId].IsScanning = false;
                        progressState.ChainProgress[chainId].IsComplete = true;
                        progressState.ChainProgress[chainId].TokensFound = refreshResult.NewTokensFound;
                        progressState.ChainProgress[chainId].HasError = !refreshResult.Success;
                        progressState.ChainProgress[chainId].ErrorMessage = refreshResult.EventScanError;
                        progressState.CompletedChains++;
                        progressState.TokensFound += refreshResult.NewTokensFound;
                        ReportProgress(progress, progressState);
                    }

                    chainResults[chainId] = new ChainRefreshResult
                    {
                        ChainId = chainId,
                        Success = refreshResult.Success,
                        TokensUpdated = refreshResult.TokensUpdated,
                        NewTokensFound = refreshResult.NewTokensFound,
                        FromBlock = refreshResult.FromBlock,
                        ToBlock = refreshResult.ToBlock,
                        UpdatedBalances = refreshResult.UpdatedBalances,
                        ErrorMessage = refreshResult.EventScanError
                    };

                    return (refreshResult.TokensUpdated, refreshResult.NewTokensFound);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            var results = await Task.WhenAll(chainTasks);

            if (cancellationToken.IsCancellationRequested)
            {
                return MultiAccountRefreshResultModel.Cancelled();
            }

            totalTokensUpdated = results.Sum(r => r.Item1);
            totalNewTokens = results.Sum(r => r.Item2);

            return MultiAccountRefreshResultModel.Successful(
                totalTokensUpdated,
                totalNewTokens,
                chainResults.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        public async Task<MultiAccountPriceResult> RefreshPricesAsync(
            IEnumerable<(string account, long chainId, IEnumerable<TokenBalance> tokens)> accountTokens,
            string currency = "usd",
            CancellationToken cancellationToken = default)
        {
            if (accountTokens == null)
            {
                return MultiAccountPriceResult.Failed("No tokens provided");
            }

            try
            {
                var tokensByChain = new Dictionary<long, HashSet<string>>();
                var hasNativeByChain = new Dictionary<long, bool>();
                var allTokens = new List<(string account, long chainId, TokenBalance token)>();

                foreach (var (account, chainId, tokens) in accountTokens)
                {
                    if (tokens == null) continue;

                    if (!tokensByChain.ContainsKey(chainId))
                    {
                        tokensByChain[chainId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        hasNativeByChain[chainId] = false;
                    }

                    foreach (var token in tokens)
                    {
                        if (token.IsNative)
                        {
                            hasNativeByChain[chainId] = true;
                        }
                        else if (!string.IsNullOrEmpty(token.Token?.Address))
                        {
                            tokensByChain[chainId].Add(token.Token.Address);
                        }
                        allTokens.Add((account, chainId, token));
                    }
                }

                var priceRequest = new BatchPriceRequest(currency);
                foreach (var kvp in tokensByChain)
                {
                    if (kvp.Value.Any() || hasNativeByChain[kvp.Key])
                    {
                        priceRequest.AddChain(kvp.Key, kvp.Value, hasNativeByChain[kvp.Key]);
                    }
                }

                if (!priceRequest.ChainRequests.Any())
                {
                    return MultiAccountPriceResult.Successful(currency, 0);
                }

                var priceResult = await _tokenService.BatchPriceService.GetPricesAsync(priceRequest, cancellationToken);

                if (!priceResult.Success)
                {
                    return MultiAccountPriceResult.Failed(string.Join("; ", priceResult.Errors));
                }

                var updatedPerChain = new Dictionary<long, int>();
                var totalUpdated = 0;

                foreach (var (account, chainId, token) in allTokens)
                {
                    if (!updatedPerChain.ContainsKey(chainId))
                    {
                        updatedPerChain[chainId] = 0;
                    }

                    if (token.IsNative)
                    {
                        if (priceResult.TryGetNativePrice(chainId, out var nativePrice))
                        {
                            token.Price = nativePrice.Price;
                            token.PriceCurrency = nativePrice.Currency;
                            updatedPerChain[chainId]++;
                            totalUpdated++;
                        }
                    }
                    else if (token.Token?.Address != null)
                    {
                        if (priceResult.TryGetPrice(chainId, token.Token.Address, out var price))
                        {
                            token.Price = price.Price;
                            token.PriceCurrency = price.Currency;
                            updatedPerChain[chainId]++;
                            totalUpdated++;
                        }
                    }
                }

                return MultiAccountPriceResult.Successful(currency, totalUpdated, updatedPerChain);
            }
            catch (OperationCanceledException)
            {
                return MultiAccountPriceResult.Failed("Operation cancelled");
            }
            catch (Exception ex)
            {
                return MultiAccountPriceResult.Failed(ex.Message);
            }
        }

        private async Task<int> ScanChainAsync(
            long chainId,
            List<string> accounts,
            Func<long, IWeb3> web3Factory,
            IDiscoveryStrategy strategy,
            MultiAccountScanOptions options,
            MultiAccountProgress progressState,
            ConcurrentDictionary<long, ChainScanResult> chainResults,
            ConcurrentDictionary<string, AccountScanResult> accountResults,
            object progressLock,
            IProgress<MultiAccountProgress> progress,
            CancellationToken cancellationToken)
        {
            var chainProgress = progressState.ChainProgress[chainId];
            chainProgress.IsScanning = true;

            var web3 = web3Factory(chainId);
            if (web3 == null)
            {
                chainResults[chainId] = new ChainScanResult
                {
                    ChainId = chainId,
                    Success = false,
                    ErrorMessage = "No Web3 provider available"
                };
                chainProgress.HasError = true;
                chainProgress.ErrorMessage = "No Web3 provider available";
                chainProgress.IsScanning = false;

                lock (progressLock)
                {
                    progressState.CompletedChains++;
                    ReportProgress(progress, progressState);
                }
                return 0;
            }

            var chainTokensFound = 0;

            lock (progressLock)
            {
                progressState.CurrentChainId = chainId;
                ReportProgress(progress, progressState);
            }

            foreach (var account in accounts)
            {
                if (cancellationToken.IsCancellationRequested)
                    return chainTokensFound;

                lock (progressLock)
                {
                    progressState.CurrentAccount = account;
                    ReportProgress(progress, progressState);
                }

                var baseChecked = chainProgress.TokensChecked;
                var baseFound = chainProgress.TokensFound;

                var discoveryProgress = new Progress<DiscoveryProgress>(p =>
                {
                    lock (progressLock)
                    {
                        chainProgress.TokensChecked = baseChecked + p.CheckedTokens;
                        chainProgress.TokensFound = baseFound + p.TokensWithBalance;
                        progressState.TokensChecked = progressState.ChainProgress.Values.Sum(c => c.TokensChecked);
                        progressState.TokensFound = progressState.ChainProgress.Values.Sum(c => c.TokensFound);
                        ReportProgress(progress, progressState);
                    }
                });

                try
                {
                    var discoveryOptions = new DiscoveryOptions
                    {
                        PageSize = options.PageSize,
                        DelayBetweenPagesMs = options.DelayBetweenPagesMs,
                        IncludeZeroBalances = options.IncludeZeroBalances
                    };

                    var result = await strategy.DiscoverAsync(
                        web3,
                        account,
                        chainId,
                        discoveryOptions,
                        discoveryProgress,
                        cancellationToken);

                    if (result.Success && result.DiscoveredTokens?.Count > 0)
                    {
                        chainTokensFound += result.DiscoveredTokens.Count;

                        if (!accountResults.ContainsKey(account))
                        {
                            accountResults[account] = new AccountScanResult
                            {
                                AccountAddress = account,
                                Success = true
                            };
                        }

                        accountResults[account].TokensByChain[chainId] = result.DiscoveredTokens;
                        accountResults[account].TokensFound += result.DiscoveredTokens.Count;
                    }
                }
                catch (Exception ex)
                {
                    chainProgress.HasError = true;
                    chainProgress.ErrorMessage = ex.Message;
                }

                lock (progressLock)
                {
                    progressState.CompletedAccounts++;
                }
            }

            chainProgress.IsComplete = true;
            chainProgress.IsScanning = false;

            chainResults[chainId] = new ChainScanResult
            {
                ChainId = chainId,
                Success = !chainProgress.HasError,
                TokensFound = chainTokensFound,
                TokensChecked = chainProgress.TokensChecked,
                StrategyUsed = strategy.StrategyName,
                ErrorMessage = chainProgress.ErrorMessage
            };

            lock (progressLock)
            {
                progressState.CompletedChains++;
                ReportProgress(progress, progressState);
            }

            return chainTokensFound;
        }

        private static void ReportProgress(IProgress<MultiAccountProgress> progress, MultiAccountProgress state)
        {
            progress?.Report(new MultiAccountProgress
            {
                TotalAccounts = state.TotalAccounts,
                CompletedAccounts = state.CompletedAccounts,
                TotalChains = state.TotalChains,
                CompletedChains = state.CompletedChains,
                CurrentChainId = state.CurrentChainId,
                CurrentChainName = state.CurrentChainName,
                CurrentAccount = state.CurrentAccount,
                TotalTokensToCheck = state.TotalTokensToCheck,
                TokensChecked = state.TokensChecked,
                TokensFound = state.TokensFound,
                ChainProgress = state.ChainProgress.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ChainProgress
                    {
                        ChainId = kvp.Value.ChainId,
                        ChainName = kvp.Value.ChainName,
                        IsScanning = kvp.Value.IsScanning,
                        IsComplete = kvp.Value.IsComplete,
                        HasError = kvp.Value.HasError,
                        ErrorMessage = kvp.Value.ErrorMessage,
                        TokensChecked = kvp.Value.TokensChecked,
                        TokensFound = kvp.Value.TokensFound,
                        TotalTokens = kvp.Value.TotalTokens
                    })
            });
        }
    }
}
