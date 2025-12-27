using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.Caching;
using Nethereum.TokenServices.ERC20.Balances;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Events;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.TokenServices.ERC20.Pricing;
using Nethereum.TokenServices.ERC20.Refresh;
using Nethereum.Web3;

namespace Nethereum.TokenServices.ERC20
{
    public class Erc20TokenService : IErc20TokenService
    {
        private readonly ITokenListProvider _tokenListProvider;
        private readonly ITokenBalanceProvider _balanceProvider;
        private readonly ITokenPriceProvider _priceProvider;
        private readonly ITokenEventScanner _eventScanner;

        private ITokenDiscoveryEngine _discoveryEngine;
        private IBatchPriceService _batchPriceService;
        private ITokenRefreshOrchestrator _refreshOrchestrator;

        public ITokenDiscoveryEngine DiscoveryEngine => _discoveryEngine ??=
            new TokenDiscoveryEngine(_tokenListProvider, _balanceProvider);

        public IBatchPriceService BatchPriceService => _batchPriceService ??=
            new BatchPriceService(_priceProvider);

        public ITokenRefreshOrchestrator RefreshOrchestrator => _refreshOrchestrator ??=
            new TokenRefreshOrchestrator(_eventScanner, _balanceProvider, _tokenListProvider);

        public Erc20TokenService(
            ITokenListProvider tokenListProvider = null,
            ITokenBalanceProvider balanceProvider = null,
            ITokenPriceProvider priceProvider = null,
            ITokenEventScanner eventScanner = null,
            ICacheProvider cacheProvider = null)
        {
            var cache = cacheProvider ?? new MemoryCacheProvider();

            // Default: Use ResilientTokenListProvider (embedded first, CoinGecko in background)
            _tokenListProvider = tokenListProvider ?? new ResilientTokenListProvider(
                remoteProvider: new CoinGeckoTokenListProvider(cacheProvider: cache),
                embeddedProvider: new EmbeddedTokenListProvider(),
                cacheProvider: cache);
            _balanceProvider = balanceProvider ?? new MultiCallBalanceProvider();
            _priceProvider = priceProvider ?? new CoinGeckoPriceProvider(cacheProvider: cache);
            _eventScanner = eventScanner ?? new Erc20EventScanner();
        }

        public Task<List<TokenInfo>> GetTokenListAsync(long chainId)
        {
            return _tokenListProvider.GetTokensAsync(chainId);
        }

        public Task<TokenInfo> GetTokenAsync(long chainId, string contractAddress)
        {
            return _tokenListProvider.GetTokenAsync(chainId, contractAddress);
        }

        public async Task<List<TokenBalance>> GetAllBalancesAsync(
            IWeb3 web3,
            string accountAddress,
            long chainId,
            bool includeNative = true,
            NativeTokenConfig nativeToken = null)
        {
            var tokens = await _tokenListProvider.GetTokensAsync(chainId);
            return await GetBalancesForTokensAsync(web3, accountAddress, tokens, includeNative, nativeToken ?? NativeTokenConfig.ForChain(chainId));
        }

        public async Task<List<TokenBalance>> GetBalancesWithPricesAsync(
            IWeb3 web3,
            string accountAddress,
            long chainId,
            string vsCurrency = "usd",
            bool includeNative = true,
            NativeTokenConfig nativeToken = null)
        {
            var tokens = await _tokenListProvider.GetTokensAsync(chainId);
            return await GetBalancesForTokensWithPricesAsync(web3, accountAddress, tokens, vsCurrency, includeNative, nativeToken ?? NativeTokenConfig.ForChain(chainId));
        }

        public async Task<List<TokenBalance>> GetBalancesForTokensAsync(
            IWeb3 web3,
            string accountAddress,
            IEnumerable<TokenInfo> tokens,
            bool includeNative = false,
            NativeTokenConfig nativeToken = null)
        {
            var tokensList = tokens?.ToList() ?? new List<TokenInfo>();
            var result = new List<TokenBalance>();

            if (includeNative && nativeToken != null)
            {
                var nativeBalance = await _balanceProvider.GetNativeBalanceAsync(web3, accountAddress, nativeToken);
                result.Add(nativeBalance);
            }

            if (tokensList.Any())
            {
                var balances = await _balanceProvider.GetBalancesAsync(web3, accountAddress, tokensList);
                result.AddRange(balances);
            }

            return result;
        }

        public async Task<List<TokenBalance>> GetBalancesForTokensWithPricesAsync(
            IWeb3 web3,
            string accountAddress,
            IEnumerable<TokenInfo> tokens,
            string vsCurrency = "usd",
            bool includeNative = false,
            NativeTokenConfig nativeToken = null)
        {
            var balances = await GetBalancesForTokensAsync(web3, accountAddress, tokens, includeNative, nativeToken);

            if (!balances.Any())
            {
                return balances;
            }

            var tokensWithBalances = balances
                .Where(b => b.Balance > 0 && !b.IsNative && !string.IsNullOrEmpty(b.Token?.Address))
                .ToList();

            if (tokensWithBalances.Any())
            {
                var chainId = tokensWithBalances.First().Token.ChainId;
                var contractAddresses = tokensWithBalances.Select(b => b.Token.Address).ToList();

                var prices = await _priceProvider.GetPricesByContractAsync(chainId, contractAddresses, vsCurrency);

                foreach (var balance in tokensWithBalances)
                {
                    if (prices.TryGetValue(balance.Token.Address.ToLowerInvariant(), out var price))
                    {
                        balance.Price = price.Price;
                        balance.PriceCurrency = price.Currency;
                    }
                }
            }

            var nativeBalance = balances.FirstOrDefault(b => b.IsNative);
            if (nativeBalance != null && nativeBalance.Balance > 0)
            {
                var chainId = nativeToken?.ChainId ?? 1;
                var nativePrice = await _priceProvider.GetNativeTokenPriceAsync(chainId, vsCurrency);
                if (nativePrice != null)
                {
                    nativeBalance.Price = nativePrice.Price;
                    nativeBalance.PriceCurrency = nativePrice.Currency;
                }
            }

            return balances;
        }

        public async Task InitializeCacheAsync(IEnumerable<long> chainIds)
        {
            var tasks = chainIds.Select(chainId => _tokenListProvider.GetTokensAsync(chainId));
            await Task.WhenAll(tasks);
        }

        public Task<bool> SupportsChainAsync(long chainId)
        {
            return _tokenListProvider.SupportsChainAsync(chainId);
        }

        public Task<TokenEventScanResult> ScanTransferEventsAsync(
            IWeb3 web3,
            string accountAddress,
            BigInteger fromBlock,
            BigInteger? toBlock = null,
            CancellationToken cancellationToken = default)
        {
            return _eventScanner.ScanTransferEventsAsync(web3, accountAddress, fromBlock, toBlock, cancellationToken);
        }

        public async Task<List<TokenBalance>> RefreshBalancesFromEventsAsync(
            IWeb3 web3,
            string accountAddress,
            long chainId,
            BigInteger fromBlock,
            IEnumerable<TokenInfo> existingTokens = null,
            string vsCurrency = "usd",
            CancellationToken cancellationToken = default)
        {
            var eventResult = await _eventScanner.ScanTransferEventsAsync(
                web3, accountAddress, fromBlock, null, cancellationToken);

            if (!eventResult.Success || !eventResult.AffectedTokenAddresses.Any())
            {
                return new List<TokenBalance>();
            }

            var existingList = existingTokens?.ToList() ?? new List<TokenInfo>();
            var existingAddresses = new HashSet<string>(
                existingList.Select(t => t.Address?.ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase);

            var newAddresses = eventResult.AffectedTokenAddresses
                .Where(a => !existingAddresses.Contains(a.ToLowerInvariant()))
                .ToList();

            var tokensToRefresh = new List<TokenInfo>();

            foreach (var addr in eventResult.AffectedTokenAddresses)
            {
                var existing = existingList.FirstOrDefault(t =>
                    string.Equals(t.Address, addr, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    tokensToRefresh.Add(existing);
                }
                else
                {
                    var tokenInfo = await _tokenListProvider.GetTokenAsync(chainId, addr);
                    if (tokenInfo != null)
                    {
                        tokensToRefresh.Add(tokenInfo);
                    }
                    else
                    {
                        tokensToRefresh.Add(new TokenInfo
                        {
                            Address = addr,
                            ChainId = chainId,
                            Symbol = "???",
                            Name = "Unknown Token",
                            Decimals = 18
                        });
                    }
                }
            }

            if (!tokensToRefresh.Any())
            {
                return new List<TokenBalance>();
            }

            return await GetBalancesForTokensWithPricesAsync(
                web3, accountAddress, tokensToRefresh, vsCurrency, false, null);
        }

        public async Task<Dictionary<string, TokenPrice>> GetPricesForTokensAsync(
            long chainId,
            IEnumerable<string> contractAddresses,
            string vsCurrency = "usd")
        {
            var addresses = contractAddresses?.ToList();
            if (addresses == null || !addresses.Any())
            {
                return new Dictionary<string, TokenPrice>();
            }

            return await _priceProvider.GetPricesByContractAsync(chainId, addresses, vsCurrency);
        }

        public Task<TokenPrice> GetNativeTokenPriceAsync(long chainId, string vsCurrency = "usd")
        {
            return _priceProvider.GetNativeTokenPriceAsync(chainId, vsCurrency);
        }
    }
}
