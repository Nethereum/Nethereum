using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Balances;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.Web3;

namespace Nethereum.TokenServices.ERC20.Discovery
{
    public class TokenDiscoveryEngine : ITokenDiscoveryEngine
    {
        private readonly ITokenListProvider _tokenListProvider;
        private readonly ITokenBalanceProvider _balanceProvider;

        public TokenDiscoveryEngine(
            ITokenListProvider tokenListProvider,
            ITokenBalanceProvider balanceProvider)
        {
            _tokenListProvider = tokenListProvider ?? throw new ArgumentNullException(nameof(tokenListProvider));
            _balanceProvider = balanceProvider ?? throw new ArgumentNullException(nameof(balanceProvider));
        }

        public async Task<TokenDiscoveryResult> DiscoverTokensAsync(
            IWeb3 web3,
            string accountAddress,
            long chainId,
            DiscoveryOptions options = null,
            IProgress<DiscoveryProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var tokenList = await _tokenListProvider.GetTokensAsync(chainId);
            var filteredList = BridgeTokenFilter.FilterBridgeTokens(tokenList);
            return await DiscoverTokensAsync(web3, accountAddress, filteredList, options, progress, cancellationToken);
        }

        public async Task<TokenDiscoveryResult> DiscoverTokensAsync(
            IWeb3 web3,
            string accountAddress,
            IEnumerable<TokenInfo> tokenList,
            DiscoveryOptions options = null,
            IProgress<DiscoveryProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new DiscoveryOptions();

            var tokens = tokenList?.ToList() ?? new List<TokenInfo>();
            if (!tokens.Any())
            {
                return TokenDiscoveryResult.Empty();
            }

            var totalTokens = tokens.Count;
            var discoveredTokens = new List<TokenBalance>();
            var currentProgress = new DiscoveryProgress
            {
                TotalTokens = totalTokens,
                CheckedTokens = options.StartFromIndex
            };

            try
            {
                for (var i = options.StartFromIndex; i < totalTokens; i += options.PageSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var page = tokens.Skip(i).Take(options.PageSize).ToList();
                    var pageEnd = Math.Min(i + options.PageSize, totalTokens);

                    List<TokenBalance> balances;
                    try
                    {
                        balances = await _balanceProvider.GetBalancesAsync(web3, accountAddress, page);
                    }
                    catch (Exception ex)
                    {
                        currentProgress.CheckedTokens = i;
                        return TokenDiscoveryResult.Failed($"Balance check failed at token {i}: {ex.Message}", currentProgress);
                    }

                    foreach (var balance in balances)
                    {
                        if (options.IncludeZeroBalances || balance.Balance > 0)
                        {
                            discoveredTokens.Add(balance);
                        }
                    }

                    currentProgress.CheckedTokens = pageEnd;
                    currentProgress.CurrentPage = i / options.PageSize;
                    currentProgress.TokensWithBalance = discoveredTokens.Count;
                    currentProgress.LastCheckedAddress = page.LastOrDefault()?.Address;

                    progress?.Report(new DiscoveryProgress
                    {
                        CheckedTokens = currentProgress.CheckedTokens,
                        TotalTokens = currentProgress.TotalTokens,
                        CurrentPage = currentProgress.CurrentPage,
                        TokensWithBalance = currentProgress.TokensWithBalance,
                        LastCheckedAddress = currentProgress.LastCheckedAddress
                    });

                    if (options.DelayBetweenPagesMs > 0 && i + options.PageSize < totalTokens)
                    {
                        await Task.Delay(options.DelayBetweenPagesMs, cancellationToken);
                    }
                }

                return new TokenDiscoveryResult
                {
                    Success = true,
                    Completed = true,
                    TokensChecked = totalTokens,
                    TotalTokens = totalTokens,
                    TokensWithBalance = discoveredTokens.Count,
                    DiscoveredTokens = discoveredTokens,
                    FinalProgress = currentProgress
                };
            }
            catch (OperationCanceledException)
            {
                return TokenDiscoveryResult.Cancelled(currentProgress, discoveredTokens);
            }
        }
    }
}
