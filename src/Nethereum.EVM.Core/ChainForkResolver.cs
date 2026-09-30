using System;

namespace Nethereum.EVM
{
    public sealed class ChainForkResolver
    {
        private readonly ChainActivationsRegistry _chains;
        private readonly HardforkRegistry _rules;

        public ChainForkResolver(ChainActivationsRegistry chains, HardforkRegistry rules)
        {
            _chains = chains ?? throw new ArgumentNullException(nameof(chains));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public IChainActivations ActivationsFor(long chainId) => _chains.Get(chainId);

        public HardforkName ResolveForkAt(long chainId, long blockNumber, ulong timestamp) =>
            _chains.ResolveAt(chainId, blockNumber, timestamp);

        public HardforkConfig ResolveConfigAt(long chainId, long blockNumber, ulong timestamp) =>
            _rules.Get(ResolveForkAt(chainId, blockNumber, timestamp));

        public HardforkConfig ResolveConfigAtHead(long chainId, long headBlockNumber, ulong headTimestamp) =>
            ResolveConfigAt(chainId, headBlockNumber, headTimestamp);
    }
}
