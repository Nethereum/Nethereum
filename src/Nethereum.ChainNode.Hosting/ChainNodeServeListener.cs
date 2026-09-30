using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.EVM.ForkId;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;

namespace Nethereum.ChainNode.Hosting
{
    public sealed class ChainNodeServeCallbacks
    {
        public Action<TransactionsMessage>? TrustedTransactionsReceived { get; set; }

        public Action<NewBlockMessage>? NewBlockReceived { get; set; }

        public Action<NewPooledTransactionHashesMessage>? PooledTransactionHashesReceived { get; set; }

        public Action<TransactionsMessage>? TransactionsReceived { get; set; }

        public Action<string>? InboundPeerAdded { get; set; }
    }

    public static class ChainNodeServeListener
    {
        public static async Task<PeerListener> StartAsync(
            IChainProfile profile,
            IChainStoreBundle bundle,
            ChainNodeConfig config,
            ChainNodeMempool mempool,
            ChainNodeServeCallbacks callbacks,
            ILoggerFactory loggerFactory,
            CancellationToken ct = default)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            config ??= new ChainNodeConfig();
            callbacks ??= new ChainNodeServeCallbacks();
            loggerFactory ??= NullLoggerFactory.Instance;

            WarnOnMalformedTrustedEnodes(config, loggerFactory.CreateLogger(typeof(ChainNodeServeListener)));

            var listener = new PeerListener(
                config.ResolveNodeKey(),
                bundle,
                BuildListenerOptions(config, mempool, callbacks),
                statusTemplate: await BuildStatusTemplateAsync(profile, bundle).ConfigureAwait(false),
                snapHandler: BuildSnapHandler(bundle, config),
                logger: loggerFactory.CreateLogger<PeerListener>());

            await listener.StartAsync(ct).ConfigureAwait(false);

            loggerFactory.CreateLogger(typeof(ChainNodeServeListener)).LogInformation(
                "eth/snap serve listener on 0.0.0.0:{Port} (NodeId=0x{NodeId}...)",
                listener.Port, listener.NodeId.ToHex().Substring(0, 16));

            return listener;
        }

        private static void WarnOnMalformedTrustedEnodes(ChainNodeConfig config, ILogger logger)
        {
            config.ResolveTrustedNodeIds(out var malformedEnodes);
            foreach (var enode in malformedEnodes)
                logger.LogWarning(
                    "Malformed enode in configuration (dropped from the trusted-peer set): {Enode}", enode);
        }

        private static PeerListenerOptions BuildListenerOptions(
            ChainNodeConfig config, ChainNodeMempool mempool, ChainNodeServeCallbacks callbacks) =>
            new PeerListenerOptions
            {
                ListenPort = config.Network.ListenPort,
                BindAddress = config.Network.BindAddress,
                MaxInboundPeers = config.Network.MaxInboundPeers,
                MaxInboundPerIP = config.Network.MaxInboundPerIP,
                HandshakeTimeoutMs = config.Network.HandshakeTimeoutMs,
                IdleTimeout = config.Network.IdleTimeout,
                ServeSnap = true,
                AdvertiseSnap2 = config.Sync.Snap.AdvertiseSnap2,
                MirrorRemoteStatus = config.Network.MirrorRemoteStatus,
                ClientId = config.Network.ClientId,
                TrustedNodeIds = config.ResolveTrustedNodeIds(),
                TxPool = mempool?.TxPool,
                OnTrustedTransactionsReceived = callbacks.TrustedTransactionsReceived
                    ?? (config.Mempool.EnableTrustedPeerAdmission ? mempool?.TrustedAdmission : null),
                EthPeerRegistry = mempool?.BroadcastPool,
                OnTrustedTransactionsReceivedFrom = callbacks.TrustedTransactionsReceived != null
                    ? null
                    : (config.Mempool.EnableTrustedPeerAdmission ? mempool?.TrustedAdmissionFrom : null),
                OnNewBlockReceived = callbacks.NewBlockReceived,
                OnPooledTransactionHashesReceived = callbacks.PooledTransactionHashesReceived,
                OnTransactionsReceived = callbacks.TransactionsReceived,
                OnInboundPeerAdded = callbacks.InboundPeerAdded,
            };

        public static async Task<Eth68StatusMessage> BuildStatusTemplateAsync(
            IChainProfile profile, IChainStoreBundle bundle)
        {
            var genesisHash = profile.GenesisHash;
            var (blockHeights, timestamps) = profile.ForkThresholds;
            var (headBlock, headTime) = await ChainHeadResolver.ResolveOurHeadAsync(bundle).ConfigureAwait(false);

            var forkId = Eip2124ForkIdCalculator.NewId(
                genesisHash,
                blockHeights ?? Array.Empty<ulong>(),
                timestamps ?? Array.Empty<ulong>(),
                headBlock,
                headTime);

            return new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = profile.NetworkId,
                TotalDifficulty = 0,
                BestHash = bundle.Metadata.GetLastBlockHash() ?? genesisHash,
                GenesisHash = genesisHash,
                ForkHash = forkId.Hash,
                ForkNext = forkId.Next,
            };
        }

        private static PatriciaSnapRequestHandler BuildSnapHandler(IChainStoreBundle bundle, ChainNodeConfig config)
        {
            var nodeStore =
                (bundle as Nethereum.CoreChain.Services.ILatestProofServingBundle)?.LatestProofNodeStore
                ?? bundle.TrieNodes;

            var selector =
                (bundle as Nethereum.CoreChain.Services.IHistoricalProofServingBundle)?.NodeServing
                as ISnapNodeStoreSelector;

            return config.Sync.Snap.SoftResponseLimit.HasValue
                ? new PatriciaSnapRequestHandler(
                    nodeStore, new StateStoreBytecodeStore(bundle.State),
                    softResponseLimit: config.Sync.Snap.SoftResponseLimit.Value, selector: selector,
                    blockAccessLists: bundle.BlockAccessLists)
                : new PatriciaSnapRequestHandler(
                    nodeStore, new StateStoreBytecodeStore(bundle.State), selector: selector,
                    blockAccessLists: bundle.BlockAccessLists);
        }
    }
}
