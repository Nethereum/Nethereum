using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Bundler
{
    public sealed class BundlerChainRules
    {
        private readonly IWeb3 _web3;
        private readonly BundlerConfig _config;
        private readonly ChainForkResolver _forkResolver;

        public BundlerChainRules(IWeb3 web3, BundlerConfig config, ChainForkResolver forkResolver = null)
        {
            _web3 = web3;
            _config = config;
            _forkResolver = forkResolver
                ?? DefaultChainForkResolver.Default;
        }

        public async Task<HardforkConfig> ResolveAsync()
        {
            var chainId = (long)(_config.ChainId
                ?? (await _web3.Eth.ChainId.SendRequestAsync().ConfigureAwait(false)).Value);

            var head = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync().ConfigureAwait(false);
            var block = await _web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(head).ConfigureAwait(false);

            var stated = _config.ResolveForkSchedule(chainId);
            var activations = stated != null
                ? ChainRules.ForConsensus(stated)
                : _forkResolver.ActivationsFor(chainId);

            return DefaultMainnetHardforkRegistry.Instance.Get(
                activations.ResolveAt((long)head.Value, (ulong)block.Timestamp.Value));
        }
    }
}
