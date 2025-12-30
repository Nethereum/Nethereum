using System;
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
using Nethereum.TokenServices.Refresh;
using Nethereum.Util;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Services.Tokens.Models;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI;
using Nethereum.Web3;

namespace Nethereum.Wallet.Services.Tokens
{
    public class TokenManagementService : ITokenManagementService
    {
        public const string NativeTokenAddress = "0x0000000000000000000000000000000000000000";
        private const int DiscoveryPageSize = 100;
        private const int DiscoveryDelayBetweenPagesMs = 300;
        private const int ReorgSafetyBuffer = 50;
        private const ulong MaxEventScanBlockRange = 100;

        private readonly ITokenStorageService _tokenStorage;
        private readonly IErc20TokenService _tokenService;
        private readonly IRpcClientFactory _rpcClientFactory;
        private readonly IChainManagementService _chainService;
        private readonly IResourceRefreshCoordinator _refreshCoordinator;

        public event EventHandler<TokensUpdatedEventArgs> TokensUpdated;

        public TokenManagementService(
            ITokenStorageService tokenStorage,
            IErc20TokenService tokenService,
            IRpcClientFactory rpcClientFactory,
            IChainManagementService chainService,
            IResourceRefreshCoordinator refreshCoordinator = null)
        {
            _tokenStorage = tokenStorage ?? throw new ArgumentNullException(nameof(tokenStorage));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _rpcClientFactory = rpcClientFactory ?? throw new ArgumentNullException(nameof(rpcClientFactory));
            _chainService = chainService ?? throw new ArgumentNullException(nameof(chainService));
            _refreshCoordinator = refreshCoordinator;
        }

        #region Discovery

        public async Task<DiscoveryResult> StartOrResumeDiscoveryAsync(
            string accountAddress,
            long chainId,
            IProgress<TokenDiscoveryProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var data = await _tokenStorage.GetAccountTokenDataAsync(accountAddress, chainId);

            if (data.DiscoveryComplete)
            {
                return DiscoveryResult.AlreadyComplete();
            }

            if (_refreshCoordinator != null)
            {
                await _refreshCoordinator.EnsureChainResourcesAsync(chainId);
            }

            var web3 = await GetWeb3ForChainAsync(chainId);

            data.DiscoveryProgress ??= new TokenDiscoveryProgress();
            var startIndex = data.DiscoveryProgress.CheckedTokens;

            var discoveryOptions = new DiscoveryOptions
            {
                PageSize = DiscoveryPageSize,
                StartFromIndex = startIndex,
                IncludeZeroBalances = false,
                DelayBetweenPagesMs = DiscoveryDelayBetweenPagesMs
            };

            var discoveryProgress = new Progress<Nethereum.TokenServices.ERC20.Discovery.DiscoveryProgress>(p =>
            {
                var totalPages = (p.TotalTokens + DiscoveryPageSize - 1) / DiscoveryPageSize;

                data.DiscoveryProgress.CheckedTokens = p.CheckedTokens;
                data.DiscoveryProgress.TotalTokens = p.TotalTokens;
                data.DiscoveryProgress.CurrentPage = p.CurrentPage;
                data.DiscoveryProgress.TotalPages = totalPages;
                data.DiscoveryProgress.TokensFoundSoFar = p.TokensWithBalance;
                data.DiscoveryProgress.LastCheckedAddress = p.LastCheckedAddress;

                _ = _tokenStorage.SaveAccountTokenDataAsync(accountAddress, chainId, data);

                progress?.Report(new TokenDiscoveryProgress
                {
                    CheckedTokens = p.CheckedTokens,
                    TotalTokens = p.TotalTokens,
                    CurrentPage = p.CurrentPage,
                    TotalPages = totalPages,
                    TokensFoundSoFar = p.TokensWithBalance,
                    LastCheckedAddress = p.LastCheckedAddress
                });
            });

            try
            {
                var result = await _tokenService.DiscoveryEngine.DiscoverTokensAsync(
                    web3, accountAddress, chainId, discoveryOptions, discoveryProgress, cancellationToken);

                if (!result.Success)
                {
                    return DiscoveryResult.Failed(result.ErrorMessage, result.TokensChecked, result.TotalTokens);
                }

                foreach (var balance in result.DiscoveredTokens)
                {
                    var existingToken = data.Tokens.FirstOrDefault(t =>
                        t.ContractAddress.IsTheSameAddress(balance.Token?.Address));

                    if (existingToken == null)
                    {
                        data.Tokens.Add(MapToAccountToken(balance, chainId, false));
                    }
                    else
                    {
                        existingToken.Balance = balance.Balance;
                        existingToken.LastUpdated = DateTime.UtcNow;
                    }
                }

                if (result.Completed)
                {
                    await AddNativeTokenIfMissingAsync(web3, accountAddress, chainId, data);

                    var currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                    data.DiscoveryComplete = true;
                    data.DiscoveryCompletedAtBlock = (ulong)currentBlock.Value;
                    data.LastScannedBlock = (ulong)currentBlock.Value;
                }

                await _tokenStorage.SaveAccountTokenDataAsync(accountAddress, chainId, data);

                if (result.Completed)
                {
                    RaiseTokensUpdated(accountAddress, chainId, data.Tokens, TokenUpdateType.Discovery);
                }

                return new DiscoveryResult
                {
                    Success = true,
                    Completed = result.Completed,
                    WasCancelled = result.WasCancelled,
                    TokensFound = result.TokensWithBalance,
                    TokensChecked = result.TokensChecked,
                    TotalTokens = result.TotalTokens
                };
            }
            catch (OperationCanceledException)
            {
                await _tokenStorage.SaveAccountTokenDataAsync(accountAddress, chainId, data);
                return DiscoveryResult.Cancelled(data.DiscoveryProgress.CheckedTokens, data.DiscoveryProgress.TotalTokens, data.Tokens.Count);
            }
        }

        public async Task<TokenDiscoveryProgress> GetDiscoveryProgressAsync(string accountAddress, long chainId)
        {
            var data = await _tokenStorage.GetAccountTokenDataAsync(accountAddress, chainId);
            return data.DiscoveryProgress ?? new TokenDiscoveryProgress();
        }

        public async Task<bool> IsDiscoveryCompleteAsync(string accountAddress, long chainId)
        {
            var data = await _tokenStorage.GetAccountTokenDataAsync(accountAddress, chainId);
            return data.DiscoveryComplete;
        }

        public async Task ResetDiscoveryAsync(string accountAddress, long chainId)
        {
            var data = await _tokenStorage.GetAccountTokenDataAsync(accountAddress, chainId);
            data.DiscoveryComplete = false;
            data.DiscoveryCompletedAtBlock = 0;
            data.LastScannedBlock = 0;
            data.DiscoveryProgress = null;
            data.Tokens.Clear();
            await _tokenStorage.SaveAccountTokenDataAsync(accountAddress, chainId, data);
        }

        public async Task ResetDiscoveryAsync(string accountAddress, IEnumerable<long> chainIds)
        {
            var chains = chainIds?.ToList() ?? new List<long>();
            foreach (var chainId in chains)
            {
                await ResetDiscoveryAsync(accountAddress, chainId);
            }
        }

        public async Task<MultiChainDiscoveryResult> DiscoverAllChainsAsync(
            string accountAddress,
            IEnumerable<long> chainIds,
            IProgress<MultiChainDiscoveryProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var chainList = chainIds?.ToList() ?? new List<long>();
            var result = new MultiChainDiscoveryResult();
            var progressState = new MultiChainDiscoveryProgress
            {
                TotalChains = chainList.Count
            };

            var tasks = chainList.Select(async chainId =>
            {
                try
                {
                    var chainProgress = new Progress<TokenDiscoveryProgress>(p =>
                    {
                        lock (progressState)
                        {
                            progressState.ChainProgress[chainId] = p;
                            progress?.Report(new MultiChainDiscoveryProgress
                            {
                                TotalChains = progressState.TotalChains,
                                CompletedChains = progressState.CompletedChains,
                                ChainProgress = new Dictionary<long, TokenDiscoveryProgress>(progressState.ChainProgress)
                            });
                        }
                    });

                    var discoveryResult = await StartOrResumeDiscoveryAsync(
                        accountAddress, chainId, chainProgress, cancellationToken);

                    lock (result)
                    {
                        result.ChainResults[chainId] = ChainDiscoveryResult.FromDiscoveryResult(chainId, discoveryResult);
                        if (discoveryResult.Completed)
                        {
                            progressState.CompletedChains++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (result)
                    {
                        result.ChainResults[chainId] = ChainDiscoveryResult.Failed(chainId, ex.Message);
                    }
                }
            });

            await Task.WhenAll(tasks);
            return result;
        }

        public async Task<int> GetExpectedTokenCountAsync(long chainId)
        {
            var tokens = await _tokenService.GetTokenListAsync(chainId);
            return tokens?.Count ?? 0;
        }

        public async Task<Dictionary<long, int>> GetExpectedTokenCountsAsync(IEnumerable<long> chainIds)
        {
            var result = new Dictionary<long, int>();
            var tasks = chainIds.Select(async chainId =>
            {
                var count = await GetExpectedTokenCountAsync(chainId);
                lock (result)
                {
                    result[chainId] = count;
                }
            });
            await Task.WhenAll(tasks);
            return result;
        }

        #endregion

        #region Event-Based Updates

        private const ulong EventScanPageSize = 10_000;

        public async Task<EventScanResult> ScanForBalanceChangesAsync(
            string accountAddress,
            long chainId,
            CancellationToken cancellationToken = default)
        {
            var data = await _tokenStorage.GetAccountTokenDataAsync(accountAddress, chainId);
            var web3 = await GetWeb3ForChainAsync(chainId);

            var currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var finalToBlock = (ulong)currentBlock.Value;

            var scanFromBlock = data.LastScannedBlock > ReorgSafetyBuffer
                ? data.LastScannedBlock - ReorgSafetyBuffer
                : (finalToBlock > ReorgSafetyBuffer ? finalToBlock - ReorgSafetyBuffer : 0);

            if (finalToBlock > scanFromBlock + MaxEventScanBlockRange)
            {
                scanFromBlock = finalToBlock - MaxEventScanBlockRange;
            }

            await AddNativeTokenIfMissingAsync(web3, accountAddress, chainId, data);
            await UpdateNativeBalanceAsync(web3, accountAddress, data);

            if (scanFromBlock >= finalToBlock)
            {
                await _tokenStorage.SaveAccountTokenDataAsync(accountAddress, chainId, data);
                RaiseTokensUpdated(accountAddress, chainId, data.Tokens, TokenUpdateType.BalanceChange);
                return EventScanResult.NoChanges(scanFromBlock, finalToBlock);
            }

            var newTokenAddresses = new List<string>();
            var pageFromBlock = scanFromBlock;

            while (pageFromBlock < finalToBlock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pageToBlock = Math.Min(pageFromBlock + EventScanPageSize, finalToBlock);

                try
                {
                    var eventResult = await _tokenService.ScanTransferEventsAsync(
                        web3, accountAddress, new BigInteger(pageFromBlock), new BigInteger(pageToBlock), cancellationToken);

                    if (eventResult.Success)
                    {
                        foreach (var tokenAddress in eventResult.AffectedTokenAddresses)
                        {
                            var existingToken = data.Tokens.FirstOrDefault(t =>
                                t.ContractAddress.IsTheSameAddress(tokenAddress));

                            if (existingToken == null)
                            {
                                var tokenInfo = await _tokenService.GetTokenAsync(chainId, tokenAddress);

                                if (tokenInfo != null && !BridgeTokenFilter.IsBridgeToken(tokenInfo.Name, tokenInfo.Symbol))
                                {
                                    data.Tokens.Add(new AccountToken
                                    {
                                        ContractAddress = tokenAddress,
                                        Symbol = tokenInfo.Symbol ?? "???",
                                        Name = tokenInfo.Name ?? "Unknown",
                                        Decimals = tokenInfo.Decimals,
                                        ChainId = chainId,
                                        LogoURI = tokenInfo.LogoUri,
                                        IsNative = false,
                                        Balance = BigInteger.Zero,
                                        LastUpdated = DateTime.UtcNow
                                    });
                                    newTokenAddresses.Add(tokenAddress);
                                }
                            }
                        }
                    }

                    data.LastScannedBlock = pageToBlock;
                    data.LastEventScan = DateTime.UtcNow;
                    await _tokenStorage.SaveAccountTokenDataAsync(accountAddress, chainId, data);

                    pageFromBlock = pageToBlock;
                }
                catch (Exception)
                {
                    data.LastScannedBlock = pageFromBlock;
                    await _tokenStorage.SaveAccountTokenDataAsync(accountAddress, chainId, data);
                    break;
                }
            }

            if (data.Tokens.Any())
            {
                await RefreshAllBalancesViaMulticallAsync(web3, accountAddress, chainId, data);
                await _tokenStorage.SaveAccountTokenDataAsync(accountAddress, chainId, data);
            }

            if (newTokenAddresses.Any())
            {
                RaiseTokensUpdated(accountAddress, chainId, data.Tokens, TokenUpdateType.BalanceChange);
            }

            return new EventScanResult
            {
                Success = true,
                TokensUpdated = data.Tokens.Count,
                NewTokensFound = newTokenAddresses.Count,
                FromBlock = scanFromBlock,
                ToBlock = data.LastScannedBlock,
                UpdatedAddresses = newTokenAddresses
            };
        }

        private async Task RefreshAllBalancesViaMulticallAsync(IWeb3 web3, string accountAddress, long chainId, AccountTokenData data)
        {
            var tokensToRefresh = data.Tokens
                .Where(t => !t.IsNative)
                .Select(t => new TokenInfo
                {
                    Address = t.ContractAddress,
                    Symbol = t.Symbol,
                    Name = t.Name,
                    Decimals = t.Decimals,
                    ChainId = chainId
                })
                .ToList();

            if (!tokensToRefresh.Any()) return;

            var balances = await _tokenService.GetBalancesForTokensAsync(web3, accountAddress, tokensToRefresh);
            if (balances == null) return;

            foreach (var balance in balances)
            {
                var token = data.Tokens.FirstOrDefault(t =>
                    t.ContractAddress.IsTheSameAddress(balance.Token?.Address));

                if (token != null)
                {
                    token.Balance = balance.Balance;
                    token.LastUpdated = DateTime.UtcNow;
                }
            }
        }

        #endregion

        #region Price Decoration

        public async Task DecorateWithPricesAsync(string accountAddress, long chainId)
        {
            var data = await _tokenStorage.GetAccountTokenDataAsync(accountAddress, chainId);
            if (data?.Tokens == null || !data.Tokens.Any()) return;

            var settings = await _tokenStorage.GetTokenSettingsAsync();
            var currency = settings?.Currency ?? "usd";

            try
            {
                var tokenAddresses = data.Tokens
                    .Where(t => !t.IsNative && t.Balance > 0)
                    .Select(t => t.ContractAddress)
                    .ToList();

                var hasNative = data.Tokens.Any(t => t.IsNative);

                var priceRequest = new BatchPriceRequest(currency)
                    .AddChain(chainId, tokenAddresses, hasNative);

                var priceResult = await _tokenService.BatchPriceService.GetPricesAsync(priceRequest);

                foreach (var token in data.Tokens.Where(t => !t.IsNative))
                {
                    if (priceResult.TryGetPrice(chainId, token.ContractAddress, out var price))
                    {
                        token.Price = price.Price;
                        token.PriceCurrency = currency;
                        token.Value = CalculateValue(token.Balance, token.Decimals, price.Price);
                        token.PriceLastUpdated = DateTime.UtcNow;
                    }
                }

                var nativeToken = data.Tokens.FirstOrDefault(t => t.IsNative);
                if (nativeToken != null && priceResult.TryGetNativePrice(chainId, out var nativePrice))
                {
                    nativeToken.Price = nativePrice.Price;
                    nativeToken.PriceCurrency = currency;
                    nativeToken.Value = CalculateValue(nativeToken.Balance, nativeToken.Decimals, nativePrice.Price);
                    nativeToken.PriceLastUpdated = DateTime.UtcNow;
                }

                data.LastPriceUpdate = DateTime.UtcNow;
                await _tokenStorage.SaveAccountTokenDataAsync(accountAddress, chainId, data);

                RaiseTokensUpdated(accountAddress, chainId, data.Tokens, TokenUpdateType.PriceUpdate);
            }
            catch
            {
                // Price decoration failures are silent - balances still valid
            }
            finally
            {
                if (_refreshCoordinator != null)
                {
                    await _refreshCoordinator.TryProcessNextJobAsync();
                }
            }
        }

        public async Task DecorateWithPricesAsync(
            IEnumerable<string> accountAddresses,
            IEnumerable<long> chainIds)
        {
            var accounts = accountAddresses?.ToList() ?? new List<string>();
            var chains = chainIds?.ToList() ?? new List<long>();

            if (!accounts.Any() || !chains.Any()) return;

            var settings = await _tokenStorage.GetTokenSettingsAsync();
            var currency = settings?.Currency ?? "usd";

            var allTokensByChain = new Dictionary<long, HashSet<string>>();
            var hasNativeByChain = new Dictionary<long, bool>();
            var accountDataCache = new Dictionary<(string, long), AccountTokenData>();

            foreach (var account in accounts)
            {
                foreach (var chainId in chains)
                {
                    var data = await _tokenStorage.GetAccountTokenDataAsync(account, chainId);
                    accountDataCache[(account, chainId)] = data;

                    if (!allTokensByChain.ContainsKey(chainId))
                    {
                        allTokensByChain[chainId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        hasNativeByChain[chainId] = false;
                    }

                    foreach (var token in data.Tokens.Where(t => !t.IsNative && t.Balance > 0))
                    {
                        allTokensByChain[chainId].Add(token.ContractAddress);
                    }

                    if (data.Tokens.Any(t => t.IsNative))
                    {
                        hasNativeByChain[chainId] = true;
                    }
                }
            }

            var priceRequest = new BatchPriceRequest(currency);
            foreach (var kvp in allTokensByChain)
            {
                priceRequest.AddChain(kvp.Key, kvp.Value, hasNativeByChain[kvp.Key]);
            }

            try
            {
                var priceResult = await _tokenService.BatchPriceService.GetPricesAsync(priceRequest);

                foreach (var account in accounts)
                {
                    foreach (var chainId in chains)
                    {
                        var data = accountDataCache[(account, chainId)];

                        foreach (var token in data.Tokens.Where(t => !t.IsNative))
                        {
                            if (priceResult.TryGetPrice(chainId, token.ContractAddress, out var price))
                            {
                                token.Price = price.Price;
                                token.PriceCurrency = currency;
                                token.Value = CalculateValue(token.Balance, token.Decimals, price.Price);
                                token.PriceLastUpdated = DateTime.UtcNow;
                            }
                        }

                        var nativeToken = data.Tokens.FirstOrDefault(t => t.IsNative);
                        if (nativeToken != null && priceResult.TryGetNativePrice(chainId, out var nativePrice))
                        {
                            nativeToken.Price = nativePrice.Price;
                            nativeToken.PriceCurrency = currency;
                            nativeToken.Value = CalculateValue(nativeToken.Balance, nativeToken.Decimals, nativePrice.Price);
                            nativeToken.PriceLastUpdated = DateTime.UtcNow;
                        }

                        data.LastPriceUpdate = DateTime.UtcNow;
                        await _tokenStorage.SaveAccountTokenDataAsync(account, chainId, data);

                        RaiseTokensUpdated(account, chainId, data.Tokens, TokenUpdateType.PriceUpdate);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Price decoration failed: {ex.Message}");
            }
        }

        private decimal CalculateValue(BigInteger balance, int decimals, decimal price)
        {
            if (balance == 0 || price == 0) return 0;
            var divisor = BigInteger.Pow(10, decimals);
            var balanceDecimal = (decimal)balance / (decimal)divisor;
            return balanceDecimal * price;
        }

        #endregion

        #region Smart Refresh

        public async Task<RefreshResult> RefreshAsync(
            string accountAddress,
            long chainId,
            CancellationToken cancellationToken = default)
        {
            var eventResult = await ScanForBalanceChangesAsync(accountAddress, chainId, cancellationToken);

            if (!eventResult.Success)
            {
                return RefreshResult.Failed(eventResult.ErrorMessage);
            }

            return RefreshResult.Success(eventResult.TokensUpdated, eventResult.NewTokensFound);
        }

        public async Task<MultiChainRefreshResult> RefreshAllChainsAsync(
            string accountAddress,
            IEnumerable<long> chainIds,
            CancellationToken cancellationToken = default)
        {
            return await RefreshAllChainsAsync(new[] { accountAddress }, chainIds, cancellationToken);
        }

        public async Task<MultiChainRefreshResult> RefreshAllChainsAsync(
            IEnumerable<string> accountAddresses,
            IEnumerable<long> chainIds,
            CancellationToken cancellationToken = default)
        {
            var accounts = accountAddresses?.ToList() ?? new List<string>();
            var chains = chainIds?.ToList() ?? new List<long>();
            var result = new MultiChainRefreshResult();

            if (!accounts.Any() || !chains.Any())
            {
                return result;
            }

            var chainTasks = chains.Select(async chainId =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return ChainRefreshResult.Failed(chainId, "Cancelled", ChainScanErrorType.Unknown);
                }

                try
                {
                    var totalTokensUpdated = 0;
                    var totalNewTokensFound = 0;

                    foreach (var account in accounts)
                    {
                        var data = await _tokenStorage.GetAccountTokenDataAsync(account, chainId);
                        data.ScanStatus ??= new ChainScanStatus();
                        data.ScanStatus.MarkScanning();
                        await _tokenStorage.SaveAccountTokenDataAsync(account, chainId, data);

                        try
                        {
                            var refreshResult = await RefreshAsync(account, chainId, cancellationToken);

                            if (refreshResult.HasBalanceError)
                            {
                                data.ScanStatus = ChainScanStatus.FromException(new Exception(refreshResult.BalanceError));
                            }
                            else
                            {
                                data.ScanStatus.MarkSuccess();
                                totalTokensUpdated += refreshResult.TokensUpdated;
                                totalNewTokensFound += refreshResult.NewTokensFound;
                            }
                        }
                        catch (Exception ex)
                        {
                            data.ScanStatus = ChainScanStatus.FromException(ex);
                        }

                        await _tokenStorage.SaveAccountTokenDataAsync(account, chainId, data);
                    }

                    var firstAccountData = await _tokenStorage.GetAccountTokenDataAsync(accounts.First(), chainId);
                    if (firstAccountData.ScanStatus?.HasError == true)
                    {
                        return ChainRefreshResult.Failed(
                            chainId,
                            firstAccountData.ScanStatus.ErrorMessage,
                            firstAccountData.ScanStatus.ErrorType);
                    }

                    return ChainRefreshResult.Succeeded(chainId, totalTokensUpdated, totalNewTokensFound, true);
                }
                catch (Exception ex)
                {
                    var status = ChainScanStatus.FromException(ex);
                    return ChainRefreshResult.Failed(chainId, status.ErrorMessage, status.ErrorType);
                }
            });

            var chainResults = await Task.WhenAll(chainTasks);

            await DecorateWithPricesAsync(accounts, chains);

            foreach (var chainResult in chainResults)
            {
                result.ChainResults[chainResult.ChainId] = chainResult;
            }

            return result;
        }

        #endregion

        #region Force Refresh (All Balances)

        public async Task<ForceRefreshResult> ForceRefreshAllBalancesAsync(
            string accountAddress,
            long chainId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var data = await _tokenStorage.GetAccountTokenDataAsync(accountAddress, chainId);
                var web3 = await GetWeb3ForChainAsync(chainId);

                await UpdateNativeBalanceAsync(web3, accountAddress, data);

                var erc20Tokens = data.Tokens
                    .Where(t => !t.IsNative && !string.IsNullOrEmpty(t.ContractAddress))
                    .Select(t => new Nethereum.TokenServices.ERC20.Models.TokenInfo
                    {
                        Address = t.ContractAddress,
                        Symbol = t.Symbol,
                        Name = t.Name,
                        Decimals = t.Decimals,
                        LogoUri = t.LogoURI
                    })
                    .ToList();

                if (!erc20Tokens.Any())
                {
                    await _tokenStorage.SaveAccountTokenDataAsync(accountAddress, chainId, data);
                    RaiseTokensUpdated(accountAddress, chainId, data.Tokens, TokenUpdateType.BalanceChange);
                    return ForceRefreshResult.Succeeded(data.Tokens.Count, 1);
                }

                var balances = await _tokenService.GetBalancesForTokensAsync(
                    web3, accountAddress, erc20Tokens);

                var updatedCount = 0;
                foreach (var balance in balances)
                {
                    var address = balance.Token?.Address;
                    if (string.IsNullOrEmpty(address)) continue;

                    var token = data.Tokens.FirstOrDefault(t =>
                        t.ContractAddress.IsTheSameAddress(address));

                    if (token != null && token.Balance != balance.Balance)
                    {
                        token.Balance = balance.Balance;
                        token.LastUpdated = DateTime.UtcNow;
                        updatedCount++;
                    }
                }

                data.LastEventScan = DateTime.UtcNow;
                await _tokenStorage.SaveAccountTokenDataAsync(accountAddress, chainId, data);
                RaiseTokensUpdated(accountAddress, chainId, data.Tokens, TokenUpdateType.BalanceChange);

                return ForceRefreshResult.Succeeded(data.Tokens.Count, updatedCount);
            }
            catch (Exception ex)
            {
                return ForceRefreshResult.Failed(ex.Message);
            }
        }

        public async Task<MultiChainForceRefreshResult> ForceRefreshAllChainsAsync(
            IEnumerable<string> accountAddresses,
            IEnumerable<long> chainIds,
            IProgress<MultiChainForceRefreshProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var accounts = accountAddresses?.ToList() ?? new List<string>();
            var chains = chainIds?.ToList() ?? new List<long>();
            var result = new MultiChainForceRefreshResult();

            if (!accounts.Any() || !chains.Any())
            {
                return result;
            }

            var progressState = new MultiChainForceRefreshProgress
            {
                TotalChains = chains.Count,
                TotalAccounts = accounts.Count
            };

            var chainTasks = chains.Select(async chainId =>
            {
                var chainTotalTokens = 0;
                var chainUpdatedTokens = 0;
                var chainSuccess = true;
                string chainError = null;

                foreach (var account in accounts)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    try
                    {
                        var refreshResult = await ForceRefreshAllBalancesAsync(
                            account, chainId, cancellationToken);

                        chainTotalTokens += refreshResult.TotalTokens;
                        chainUpdatedTokens += refreshResult.UpdatedTokens;

                        if (!refreshResult.Success)
                        {
                            chainSuccess = false;
                            chainError = refreshResult.ErrorMessage;
                            lock (result)
                            {
                                result.Errors.Add((account, chainId, refreshResult.ErrorMessage));
                            }
                        }

                        lock (progressState)
                        {
                            progressState.CompletedOperations++;
                            progress?.Report(progressState.Clone());
                        }
                    }
                    catch (Exception ex)
                    {
                        chainSuccess = false;
                        chainError = ex.Message;
                        lock (result)
                        {
                            result.Errors.Add((account, chainId, ex.Message));
                        }

                        lock (progressState)
                        {
                            progressState.CompletedOperations++;
                            progress?.Report(progressState.Clone());
                        }
                    }
                }

                lock (result)
                {
                    result.ChainResults[chainId] = chainSuccess
                        ? ChainForceRefreshResult.Succeeded(chainId, chainTotalTokens, chainUpdatedTokens)
                        : ChainForceRefreshResult.Failed(chainId, chainError);
                }
            });

            await Task.WhenAll(chainTasks);

            await DecorateWithPricesAsync(accounts, chains);

            return result;
        }

        #endregion

        #region Status

        public async Task<ChainScanStatus> GetChainStatusAsync(string accountAddress, long chainId)
        {
            var data = await _tokenStorage.GetAccountTokenDataAsync(accountAddress, chainId);
            return data.ScanStatus ?? new ChainScanStatus();
        }

        public async Task<Dictionary<long, ChainScanStatus>> GetAllChainStatusesAsync(
            string accountAddress,
            IEnumerable<long> chainIds)
        {
            var result = new Dictionary<long, ChainScanStatus>();
            var chains = chainIds?.ToList() ?? new List<long>();

            foreach (var chainId in chains)
            {
                var status = await GetChainStatusAsync(accountAddress, chainId);
                result[chainId] = status;
            }

            return result;
        }

        #endregion

        #region Data Access

        public async Task<List<AccountToken>> GetAccountTokensAsync(string accountAddress, long chainId, bool includeHidden = false)
        {
            var data = await _tokenStorage.GetAccountTokenDataAsync(accountAddress, chainId);
            return includeHidden
                ? data.Tokens
                : data.Tokens.Where(t => !t.IsHidden).ToList();
        }

        public async Task<AccountToken> GetTokenAsync(string accountAddress, long chainId, string contractAddress)
        {
            var data = await _tokenStorage.GetAccountTokenDataAsync(accountAddress, chainId);
            return data.Tokens.FirstOrDefault(t => t.ContractAddress.IsTheSameAddress(contractAddress));
        }

        #endregion

        #region Custom Tokens

        public async Task<bool> AddCustomTokenAsync(long chainId, string contractAddress)
        {
            try
            {
                var web3 = await GetWeb3ForChainAsync(chainId);
                var erc20 = web3.Eth.ERC20.GetContractService(contractAddress);

                var symbol = await erc20.SymbolQueryAsync();
                var name = await erc20.NameQueryAsync();
                var decimals = await erc20.DecimalsQueryAsync();

                var customToken = new CustomToken
                {
                    ContractAddress = contractAddress,
                    Symbol = symbol,
                    Name = name,
                    Decimals = (int)decimals,
                    ChainId = chainId,
                    AddedAt = DateTime.UtcNow
                };

                await _tokenStorage.AddCustomTokenAsync(chainId, customToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> AddCustomTokenAsync(long chainId, CustomToken token)
        {
            try
            {
                token.ChainId = chainId;
                token.AddedAt = DateTime.UtcNow;
                await _tokenStorage.AddCustomTokenAsync(chainId, token);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateCustomTokenAsync(long chainId, CustomToken token)
        {
            try
            {
                await _tokenStorage.UpdateCustomTokenAsync(chainId, token);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteCustomTokenAsync(long chainId, string contractAddress)
        {
            try
            {
                await _tokenStorage.DeleteCustomTokenAsync(chainId, contractAddress);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task SetTokenHiddenAsync(string accountAddress, long chainId, string contractAddress, bool hidden)
        {
            var data = await _tokenStorage.GetAccountTokenDataAsync(accountAddress, chainId);
            var token = data.Tokens.FirstOrDefault(t => t.ContractAddress.IsTheSameAddress(contractAddress));
            if (token != null)
            {
                token.IsHidden = hidden;
                await _tokenStorage.SaveAccountTokenDataAsync(accountAddress, chainId, data);
            }
        }

        #endregion

        #region Cache

        public async Task InitializeCacheAsync(IEnumerable<long> chainIds)
        {
            var chainIdsList = chainIds?.ToList() ?? new List<long>();

            if (_refreshCoordinator != null && chainIdsList.Any())
            {
                _refreshCoordinator.QueueStaleResources(chainIdsList);
            }

            await _tokenService.InitializeCacheAsync(chainIdsList);
        }

        #endregion

        #region Private Helpers

        private async Task<IWeb3> GetWeb3ForChainAsync(long chainId)
        {
            var chain = await _chainService.GetChainAsync(chainId);
            if (chain == null)
            {
                throw new InvalidOperationException($"Chain {chainId} not found");
            }
            var client = await _rpcClientFactory.CreateClientAsync(chain);
            return new Web3.Web3(client);
        }

        private async Task<NativeTokenConfig> GetNativeTokenConfigAsync(long chainId)
        {
            var chain = await _chainService.GetChainAsync(chainId);
            if (chain == null) return null;

            var nativeCurrency = chain.NativeCurrency;
            return NativeTokenConfig.ForChain(
                chainId,
                nativeCurrency?.Symbol ?? "ETH",
                nativeCurrency?.Name ?? "Ether",
                nativeCurrency?.Decimals ?? 18,
                chain.IsTestnet);
        }

        private async Task AddNativeTokenIfMissingAsync(IWeb3 web3, string accountAddress, long chainId, AccountTokenData data)
        {
            var nativeConfig = await GetNativeTokenConfigAsync(chainId);
            if (nativeConfig == null) return;

            var existingNative = data.Tokens.FirstOrDefault(t => t.IsNative);
            if (existingNative != null) return;

            try
            {
                var balance = await web3.Eth.GetBalance.SendRequestAsync(accountAddress);
                data.Tokens.Insert(0, new AccountToken
                {
                    ContractAddress = NativeTokenAddress,
                    Symbol = nativeConfig.Symbol,
                    Name = nativeConfig.Name,
                    Decimals = nativeConfig.Decimals,
                    ChainId = chainId,
                    Balance = balance.Value,
                    LastUpdated = DateTime.UtcNow,
                    IsNative = true
                });
            }
            catch
            {
                // Native balance fetch failed, continue without it
            }
        }

        private async Task UpdateNativeBalanceAsync(IWeb3 web3, string accountAddress, AccountTokenData data)
        {
            var nativeToken = data.Tokens.FirstOrDefault(t => t.IsNative);
            if (nativeToken == null) return;

            try
            {
                var balance = await web3.Eth.GetBalance.SendRequestAsync(accountAddress);
                nativeToken.Balance = balance.Value;
                nativeToken.LastUpdated = DateTime.UtcNow;
            }
            catch
            {
                // Native balance fetch failed, keep existing balance
            }
        }

        private AccountToken MapToAccountToken(TokenBalance balance, long chainId, bool isCustom)
        {
            return new AccountToken
            {
                ContractAddress = balance.IsNative ? NativeTokenAddress : balance.Token?.Address,
                Symbol = balance.Token?.Symbol,
                Name = balance.Token?.Name,
                Decimals = balance.Token?.Decimals ?? 18,
                LogoURI = balance.Token?.LogoUri,
                ChainId = chainId,
                Balance = balance.Balance,
                LastUpdated = DateTime.UtcNow,
                IsCustom = isCustom,
                IsHidden = false,
                IsNative = balance.IsNative,
                Price = balance.Price ?? 0,
                PriceCurrency = balance.PriceCurrency,
                Value = balance.Value ?? 0,
                PriceLastUpdated = balance.Price.HasValue ? DateTime.UtcNow : (DateTime?)null
            };
        }

        private void RaiseTokensUpdated(string accountAddress, long chainId, List<AccountToken> tokens, TokenUpdateType updateType)
        {
            TokensUpdated?.Invoke(this, new TokensUpdatedEventArgs
            {
                AccountAddress = accountAddress,
                ChainId = chainId,
                Tokens = tokens,
                UpdateType = updateType
            });
        }

        #endregion
    }
}
