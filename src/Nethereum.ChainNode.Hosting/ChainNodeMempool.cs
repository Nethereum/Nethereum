using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Mempool;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.Model.P2P;

namespace Nethereum.ChainNode.Hosting
{
    public sealed class ChainNodeMempool : IDisposable
    {
        private readonly IChainProfile _profile;
        private EthBroadcastPeerBridge _broadcastBridge;
        private readonly ILogger _logger;

        private ChainNodeMempool(
            ITxPool txPool, RelayMempool relay, Eth68PeerPool broadcastPool,
            IChainProfile profile, ILogger logger)
        {
            _profile = profile;
            _logger = logger;
            TxPool = txPool;
            Relay = relay;
            BroadcastPool = broadcastPool;
        }

        public ITxPool TxPool { get; }

        public RelayMempool Relay { get; }

        public Eth68PeerPool BroadcastPool { get; }

        public Action<TransactionsMessage> TrustedAdmission =>
            new TrustedTransactionAdmission(Relay, _logger).Admit;

        public Action<TransactionsMessage, Guid> TrustedAdmissionFrom =>
            new TrustedTransactionAdmission(Relay, _logger).AdmitFrom;

        public static ChainNodeMempool Create(
            IChainProfile profile, IChainStoreBundle bundle, ChainNodeConfig config,
            Eth68PeerPool broadcastPool, ILoggerFactory loggerFactory)
        {
            var txPool = config.Mempool.CreatePool();

            var relay = new RelayMempool(
                txPool,
                broadcastPool,
                bundle,
                new MempoolAdmissionValidator(profile.NetworkId),
                loggerFactory?.CreateLogger<RelayMempool>(),
                config.Mempool.Retention,
                config.Mempool.Relay);

            return new ChainNodeMempool(
                txPool, relay, broadcastPool, profile,
                loggerFactory?.CreateLogger<ChainNodeMempool>());
        }

        public void BridgeDialledPeers(PeerPoolManager pool)
        {
            if (pool == null) return;

            _broadcastBridge = new EthBroadcastPeerBridge(
                pool, BroadcastPool, _profile.NetworkId, _profile.GenesisHash);
        }

        public void Dispose() => _broadcastBridge?.Dispose();
    }
}
