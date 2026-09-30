using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain.Configuration;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.DevChain.Hosting
{
    public static class DevChainComposition
    {
        public static async Task<DevChainComposedNode> ComposeAsync(
            DevChainServerConfig config,
            DevChainNode node,
            IChainStoreBundle bundle,
            Func<DevChainNode, Task> startNodeAsync,
            ILoggerFactory loggerFactory,
            CancellationToken ct = default)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (startNodeAsync == null) throw new ArgumentNullException(nameof(startNodeAsync));

            loggerFactory ??= NullLoggerFactory.Instance;

            var definition = new DevChainDefinition(config, (b, token) => startNodeAsync(node));

            await definition.EnsureGenesisAsync(bundle, ct).ConfigureAwait(false);

            var profile = await definition.CreateProfileAsync(bundle).ConfigureAwait(false);
            var ethPeers = new Eth68PeerPool();
            var mempool = ChainNodeMempool.Create(profile, bundle, config.Node, ethPeers, loggerFactory);

            var listener = config.Node.Network.Serve
                ? await ChainNodeServeListener.StartAsync(
                    profile, bundle, config.Node, mempool, callbacks: null, loggerFactory, ct).ConfigureAwait(false)
                : null;

            var sync = config.Node.Sync.Mode == SyncMode.None
                ? null
                : await ChainNodeSyncStack.StartAsync(
                    profile, bundle, config.Node, loggerFactory, definition.CreateTip, ct).ConfigureAwait(false);

            if (sync != null) mempool.BridgeDialledPeers(sync.Pool);

            return new DevChainComposedNode
            {
                Node = node,
                Bundle = bundle,
                Profile = profile,
                Mempool = mempool,
                Listener = listener,
                Sync = sync,
            };
        }
    }
}
