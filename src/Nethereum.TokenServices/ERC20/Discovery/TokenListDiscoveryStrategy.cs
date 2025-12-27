using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.Web3;

namespace Nethereum.TokenServices.ERC20.Discovery
{
    public class TokenListDiscoveryStrategy : IDiscoveryStrategy
    {
        private readonly ITokenListProvider _tokenListProvider;
        private readonly ITokenDiscoveryEngine _discoveryEngine;

        public string StrategyName => "TokenList";

        public TokenListDiscoveryStrategy(ITokenListProvider tokenListProvider, ITokenDiscoveryEngine discoveryEngine)
        {
            _tokenListProvider = tokenListProvider ?? throw new ArgumentNullException(nameof(tokenListProvider));
            _discoveryEngine = discoveryEngine ?? throw new ArgumentNullException(nameof(discoveryEngine));
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

            try
            {
                var result = await _discoveryEngine.DiscoverTokensAsync(
                    web3,
                    accountAddress,
                    chainId,
                    options,
                    progress,
                    cancellationToken);

                result.StrategyName = StrategyName;
                return result;
            }
            catch (OperationCanceledException)
            {
                return TokenDiscoveryResult.Cancelled(null, new List<TokenBalance>(), StrategyName);
            }
            catch (Exception ex)
            {
                return TokenDiscoveryResult.Failed(ex.Message, null, StrategyName);
            }
        }

        public async Task<bool> SupportsChainAsync(long chainId)
        {
            return await _tokenListProvider.SupportsChainAsync(chainId);
        }

        public async Task<int> GetExpectedTokenCountAsync(long chainId)
        {
            var tokens = await _tokenListProvider.GetTokensAsync(chainId);
            return tokens?.Count ?? 0;
        }
    }
}
