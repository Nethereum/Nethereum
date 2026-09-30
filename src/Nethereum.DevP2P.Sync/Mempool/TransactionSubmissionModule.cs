using System;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;

namespace Nethereum.DevP2P.Sync.Mempool
{
    public sealed class TransactionSubmissionModule : IDisposable
    {
        public ITxPool TxPool { get; }
        public RelayMempool Mempool { get; }
        public ITransactionSubmissionService Submission { get; }

        private readonly EthBroadcastPeerBridge _broadcastBridge;

        private TransactionSubmissionModule(
            ITxPool txPool, RelayMempool mempool, ITransactionSubmissionService submission,
            EthBroadcastPeerBridge broadcastBridge)
        {
            TxPool = txPool;
            Mempool = mempool;
            Submission = submission;
            _broadcastBridge = broadcastBridge;
        }

        public static TransactionSubmissionModule Create(
            IChainStoreBundle bundle, IPeerPool peers, BigInteger chainId, byte[] genesisHash,
            ILogger<RelayMempool> logger = null)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (peers == null) throw new ArgumentNullException(nameof(peers));
            if (genesisHash == null) throw new ArgumentNullException(nameof(genesisHash));

            var txPool = new TxPool();
            var broadcastPool = new Eth68PeerPool();
            var broadcastBridge = new EthBroadcastPeerBridge(peers, broadcastPool, (ulong)chainId, genesisHash);
            var mempool = new RelayMempool(
                txPool, broadcastPool, bundle, new MempoolAdmissionValidator(chainId), logger);
            return new TransactionSubmissionModule(
                txPool, mempool, new MempoolTransactionSubmissionService(mempool), broadcastBridge);
        }

        public void Dispose() => _broadcastBridge.Dispose();
    }
}
