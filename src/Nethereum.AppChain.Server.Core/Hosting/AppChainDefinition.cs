using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Signer;

namespace Nethereum.AppChain.Server.Hosting
{
    public sealed class AppChainDefinition : IChainDefinition
    {
        private readonly AppChainServerConfig _config;
        private readonly Func<IChainStoreBundle, CancellationToken, Task> _ensureGenesis;

        public AppChainDefinition(
            AppChainServerConfig config,
            Func<IChainStoreBundle, CancellationToken, Task> ensureGenesis = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _ensureGenesis = ensureGenesis;
        }

        public Task EnsureGenesisAsync(IChainStoreBundle bundle, CancellationToken ct) =>
            _ensureGenesis == null ? Task.CompletedTask : _ensureGenesis(bundle, ct);

        public async Task<IChainProfile> CreateProfileAsync(IChainStoreBundle bundle)
        {
            var genesisHash = await bundle.Blocks.GetHashByNumberAsync(0).ConfigureAwait(false);

            if (genesisHash == null)
                throw new InvalidOperationException(
                    $"AppChain '{_config.ChainName}' has no genesis block; a node cannot assert an identity without one.");

            return new DefaultChainProfile(
                (ulong)_config.ChainId,
                genesisHash,
                _config.ForkSchedule,
                _config.Node.ResolveDialEnodes(),
                StableIdentityOf(_config),
                bundle);
        }

        public ICanonicalStateRootSource CreateTip(PeerPoolManager pool) => null;

        private static EthECKey StableIdentityOf(AppChainServerConfig config) =>
            string.IsNullOrEmpty(config.Node.Network.NodeKeyHex)
                ? null
                : new EthECKey(config.Node.Network.NodeKeyHex);
    }
}
