using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Web3;

namespace Nethereum.TokenServices.ERC20.Discovery
{
    public interface IDiscoveryStrategy
    {
        string StrategyName { get; }

        Task<TokenDiscoveryResult> DiscoverAsync(
            IWeb3 web3,
            string accountAddress,
            long chainId,
            DiscoveryOptions options = null,
            IProgress<DiscoveryProgress> progress = null,
            CancellationToken cancellationToken = default);

        Task<bool> SupportsChainAsync(long chainId);

        Task<int> GetExpectedTokenCountAsync(long chainId);
    }
}
