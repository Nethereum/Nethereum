using System.Collections.Generic;

namespace Nethereum.EVM
{
    /// <summary>
    /// Chain-id → <see cref="IChainActivations"/> lookup. Defaults only register
    /// mainnet (chain id 1); L2s, testnets, and AppChains register their own
    /// activations table once at startup.
    ///
    /// Unknown chain ids throw — silent fallback would replay transactions
    /// against the wrong fork rules.
    /// </summary>
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

        public IChainActivations Get(long chainId)
        {
            if (_map.TryGetValue(chainId, out var a)) return a;
            throw new System.InvalidOperationException(
                $"No activations registered for chain id {chainId}. " +
                "Register an IChainActivations for this chain before replaying blocks.");
        }

        public HardforkName ResolveAt(long chainId, long blockNumber, ulong timestamp)
            => Get(chainId).ResolveAt(blockNumber, timestamp);
    }
}
