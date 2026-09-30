using System;
using Nethereum.CoreChain;
using Nethereum.DevP2P.Sync.Mempool;

namespace Nethereum.ChainNode.Hosting.Configuration
{
    public sealed class ChainNodeMempoolConfig
    {
        public int MaxPoolSize { get; set; } = 5_000;

        public int MaxTxsPerSender { get; set; } = 64;

        public MempoolRetention Retention { get; set; } = MempoolRetention.Full;

        public MempoolRelay Relay { get; set; } = MempoolRelay.Ours;

        public bool EnableTrustedPeerAdmission { get; set; }

        public Func<ChainNodeMempoolConfig, ITxPool>? PoolFactory { get; set; }

        public ITxPool CreatePool() =>
            PoolFactory != null
                ? PoolFactory(this)
                : new TxPool(maxPoolSize: MaxPoolSize, maxTxsPerSender: MaxTxsPerSender);
    }
}
