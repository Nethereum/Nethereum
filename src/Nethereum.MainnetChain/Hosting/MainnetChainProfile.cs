using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;

namespace Nethereum.MainnetChain.Hosting
{
    public sealed class MainnetChainProfile : IChainProfile
    {
        private readonly EthECKey _localKey;
        private readonly IChainStoreBundle _bundle;

        public MainnetChainProfile(EthECKey localKey, IChainStoreBundle bundle)
        {
            _localKey = localKey;
            _bundle = bundle;
        }

        public ulong NetworkId => (ulong)MainnetGenesisConstants.ChainId;

        public byte[] GenesisHash { get; } = MainnetGenesisConstants.BlockHashHex.HexToByteArray();

        public (ulong[] BlockHeights, ulong[] Timestamps) ForkThresholds =>
            (MainnetChainSchedule.ForkIdentity.BlockHeights, MainnetChainSchedule.ForkIdentity.Timestamps);

        public IReadOnlyList<string> Bootnodes => SyncPeerSession.MainnetBootnodes;

        public IPeerHandshakeWorker CreateHandshakeWorker(ILoggerFactory loggerFactory, bool advertiseSnap2) =>
            new MainnetPeerHandshakeWorker(
                loggerFactory?.CreateLogger("Nethereum.DevP2P.Sync.SyncPeerSession"),
                _localKey,
                _bundle == null
                    ? (Func<Task<(ulong HeadBlock, ulong HeadTime)>>)null
                    : () => ChainHeadResolver.ResolveOurHeadAsync(_bundle));
    }
}
