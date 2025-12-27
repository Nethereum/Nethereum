using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Balances;
using Nethereum.TokenServices.ERC20.Events;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.Web3;

namespace Nethereum.TokenServices.ERC20.Discovery
{
    public class EventLogDiscoveryStrategy : IDiscoveryStrategy
    {
        private readonly ITokenEventScanner _eventScanner;
        private readonly ITokenBalanceProvider _balanceProvider;
        private readonly ITokenListProvider _tokenListProvider;
        private readonly BigInteger _defaultFromBlock;

        public string StrategyName => "EventLog";

        public EventLogDiscoveryStrategy(
            ITokenEventScanner eventScanner,
            ITokenBalanceProvider balanceProvider,
            ITokenListProvider tokenListProvider = null,
            BigInteger? defaultFromBlock = null)
        {
            _eventScanner = eventScanner ?? throw new ArgumentNullException(nameof(eventScanner));
            _balanceProvider = balanceProvider ?? throw new ArgumentNullException(nameof(balanceProvider));
            _tokenListProvider = tokenListProvider;
            _defaultFromBlock = defaultFromBlock ?? BigInteger.Zero;
        }

        public async Task<TokenDiscoveryResult> DiscoverAsync(
            IWeb3 web3,
            string accountAddress,
            long chainId,
            DiscoveryOptions options = null,
            IProgress<DiscoveryProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new DiscoveryOptions();
            var discoveredTokens = new List<TokenBalance>();
            var currentProgress = new DiscoveryProgress();

            try
            {
                progress?.Report(new DiscoveryProgress
                {
                    CheckedTokens = 0,
                    TotalTokens = 0,
                    TokensWithBalance = 0
                });

                var eventResult = await _eventScanner.ScanTransferEventsAsync(
                    web3,
                    accountAddress,
                    _defaultFromBlock,
                    null,
                    cancellationToken);

                if (!eventResult.Success)
                {
                    return TokenDiscoveryResult.Failed(
                        eventResult.ErrorMessage ?? "Event scan failed",
                        currentProgress,
                        StrategyName);
                }

                var tokenAddresses = eventResult.AffectedTokenAddresses?
                    .Where(a => !string.IsNullOrEmpty(a))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();

                if (!tokenAddresses.Any())
                {
                    return TokenDiscoveryResult.Empty(StrategyName);
                }

                currentProgress.TotalTokens = tokenAddresses.Count;
                progress?.Report(new DiscoveryProgress
                {
                    CheckedTokens = 0,
                    TotalTokens = tokenAddresses.Count,
                    TokensWithBalance = 0
                });

                var tokenInfoList = await BuildTokenInfoListAsync(chainId, tokenAddresses);

                var pageSize = options.PageSize > 0 ? options.PageSize : 100;
                for (var i = 0; i < tokenInfoList.Count; i += pageSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var page = tokenInfoList.Skip(i).Take(pageSize).ToList();
                    var balances = await _balanceProvider.GetBalancesAsync(web3, accountAddress, page);

                    foreach (var balance in balances)
                    {
                        if (options.IncludeZeroBalances || balance.Balance > 0)
                        {
                            discoveredTokens.Add(balance);
                        }
                    }

                    currentProgress.CheckedTokens = Math.Min(i + pageSize, tokenInfoList.Count);
                    currentProgress.TokensWithBalance = discoveredTokens.Count;
                    currentProgress.LastCheckedAddress = page.LastOrDefault()?.Address;

                    progress?.Report(new DiscoveryProgress
                    {
                        CheckedTokens = currentProgress.CheckedTokens,
                        TotalTokens = currentProgress.TotalTokens,
                        TokensWithBalance = currentProgress.TokensWithBalance,
                        LastCheckedAddress = currentProgress.LastCheckedAddress
                    });

                    if (options.DelayBetweenPagesMs > 0 && i + pageSize < tokenInfoList.Count)
                    {
                        await Task.Delay(options.DelayBetweenPagesMs, cancellationToken);
                    }
                }

                return TokenDiscoveryResult.Successful(discoveredTokens, currentProgress, StrategyName);
            }
            catch (OperationCanceledException)
            {
                return TokenDiscoveryResult.Cancelled(currentProgress, discoveredTokens, StrategyName);
            }
            catch (Exception ex)
            {
                return TokenDiscoveryResult.Failed(ex.Message, currentProgress, StrategyName);
            }
        }

        public Task<bool> SupportsChainAsync(long chainId)
        {
            return Task.FromResult(true);
        }

        public Task<int> GetExpectedTokenCountAsync(long chainId)
        {
            return Task.FromResult(0);
        }

        private async Task<List<TokenInfo>> BuildTokenInfoListAsync(long chainId, List<string> tokenAddresses)
        {
            var result = new List<TokenInfo>();
            Dictionary<string, TokenInfo> knownTokens = null;

            if (_tokenListProvider != null)
            {
                var tokenList = await _tokenListProvider.GetTokensAsync(chainId);
                knownTokens = tokenList?
                    .Where(t => !string.IsNullOrEmpty(t.Address))
                    .ToDictionary(t => t.Address.ToLowerInvariant(), t => t, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var address in tokenAddresses)
            {
                if (knownTokens != null && knownTokens.TryGetValue(address.ToLowerInvariant(), out var knownToken))
                {
                    result.Add(knownToken);
                }
                else
                {
                    result.Add(new TokenInfo
                    {
                        Address = address,
                        ChainId = chainId,
                        Symbol = "???",
                        Name = "Unknown Token",
                        Decimals = 18
                    });
                }
            }

            return result;
        }
    }
}
