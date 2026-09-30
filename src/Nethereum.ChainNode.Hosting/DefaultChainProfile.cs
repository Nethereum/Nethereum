using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.EVM;
using Nethereum.Signer;

namespace Nethereum.ChainNode.Hosting
{
    public sealed class DefaultChainProfile : IChainProfile
    {
        private readonly EthECKey _localKey;
        private readonly IChainStoreBundle _bundle;

        public DefaultChainProfile(
            ulong networkId,
            byte[] genesisHash,
            ChainForkSchedule forkSchedule,
            IReadOnlyList<string> bootnodes,
            EthECKey localKey = null,
            IChainStoreBundle bundle = null)
        {
            if (genesisHash == null || genesisHash.Length != 32)
                throw new ArgumentException("A chain node must assert a 32-byte genesis hash.", nameof(genesisHash));
            if (forkSchedule == null) throw new ArgumentNullException(nameof(forkSchedule));

            NetworkId = networkId;
            GenesisHash = genesisHash;
            var thresholds = forkSchedule.ForkThresholds();
            ForkThresholds = (thresholds.BlockHeights, thresholds.Timestamps);
            Bootnodes = bootnodes ?? Array.Empty<string>();
            _localKey = localKey;
            _bundle = bundle;
        }

        public ulong NetworkId { get; }

        public byte[] GenesisHash { get; }

        public (ulong[] BlockHeights, ulong[] Timestamps) ForkThresholds { get; }

        public IReadOnlyList<string> Bootnodes { get; }

        public IPeerHandshakeWorker CreateHandshakeWorker(ILoggerFactory loggerFactory, bool advertiseSnap2) =>
            new ChainPeerHandshakeWorker(
                GenesisHash,
                NetworkId,
                ForkThresholds,
                _bundle == null
                    ? (Func<Task<(ulong HeadBlock, ulong HeadTime)>>)null
                    : () => ChainHeadResolver.ResolveOurHeadAsync(_bundle),
                loggerFactory?.CreateLogger("Nethereum.DevP2P.Sync.SyncPeerSession"),
                localKey: _localKey,
                advertiseSnap2: advertiseSnap2);
    }
}
