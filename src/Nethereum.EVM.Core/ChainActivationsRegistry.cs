using System.Collections.Generic;

namespace Nethereum.EVM
{
    public class ChainActivationsRegistry
    {
        public static readonly ChainActivationsRegistry Instance = new ChainActivationsRegistry();

        private readonly Dictionary<long, IChainActivations> _map = new Dictionary<long, IChainActivations>();

        public ChainActivationsRegistry()
        {
            Register(1, MainnetChainActivations.Instance);
        }

        public void Register(long chainId, IChainActivations activations)
        {
            if (activations is null) throw new System.ArgumentNullException(nameof(activations));
            _map[chainId] = activations;
        }

        public bool TryGet(long chainId, out IChainActivations activations) => _map.TryGetValue(chainId, out activations);

        public IChainActivations DefaultForUnregisteredChains { get; set; }

        public IChainActivations Get(long chainId)
        {
            if (_map.TryGetValue(chainId, out var a)) return a;
            if (DefaultForUnregisteredChains != null) return DefaultForUnregisteredChains;

            throw new System.InvalidOperationException(
                $"No activations registered for chain id {chainId}. " +
                "Register an IChainActivations for this chain before replaying blocks, or set " +
                nameof(DefaultForUnregisteredChains) + " to state what an undescribed chain runs.");
        }

        public HardforkName ResolveAt(long chainId, long blockNumber, ulong timestamp)
            => Get(chainId).ResolveAt(blockNumber, timestamp);
    }
}
