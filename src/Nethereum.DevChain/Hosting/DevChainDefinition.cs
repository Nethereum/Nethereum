using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ChainNode.Hosting;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevChain.Configuration;
using Nethereum.DevP2P.Sync.Peering;

namespace Nethereum.DevChain.Hosting
{
    public sealed class DevChainDefinition : IChainDefinition
    {
        private readonly DevChainServerConfig _config;
        private readonly Func<IChainStoreBundle, CancellationToken, Task> _ensureGenesis;

        public DevChainDefinition(
            DevChainServerConfig config,
            Func<IChainStoreBundle, CancellationToken, Task> ensureGenesis)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _ensureGenesis = ensureGenesis ?? throw new ArgumentNullException(nameof(ensureGenesis));
        }

        public Task EnsureGenesisAsync(IChainStoreBundle bundle, CancellationToken ct) =>
            _ensureGenesis(bundle, ct);

        public async Task<IChainProfile> CreateProfileAsync(IChainStoreBundle bundle)
        {
            var genesisHash = await bundle.Blocks.GetHashByNumberAsync(0).ConfigureAwait(false);

            if (genesisHash == null)
                throw new InvalidOperationException(
                    $"DevChain '{_config.ChainId}' has no genesis block; a node cannot assert an identity without one.");

            return new DefaultChainProfile(
                (ulong)_config.ChainId,
                genesisHash,
                _config.Chain.ForkSchedule,
                _config.Node.ResolveDialEnodes(),
                bundle: bundle);
        }

        public ICanonicalStateRootSource CreateTip(PeerPoolManager pool) => null;
    }
}
