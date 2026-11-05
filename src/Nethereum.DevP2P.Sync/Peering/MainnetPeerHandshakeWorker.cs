using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;

namespace Nethereum.DevP2P.Sync.Peering
{
    public sealed class MainnetPeerHandshakeWorker : IPeerHandshakeWorker
    {
        private readonly ChainPeerHandshakeWorker _inner;
        private readonly Func<Task<(ulong HeadBlock, ulong HeadTime)>> _ourHeadProvider;
        public Func<Task<(ulong HeadBlock, ulong HeadTime)>> OurHeadProvider => _ourHeadProvider;

        public MainnetPeerHandshakeWorker(
            ILogger logger = null, EthECKey localKey = null,
            Func<Task<(ulong HeadBlock, ulong HeadTime)>> ourHeadProvider = null)
        {
            _ourHeadProvider = ourHeadProvider;
            _inner = new ChainPeerHandshakeWorker(
                MainnetGenesisConstants.BlockHashHex.HexToByteArray(),
                (ulong)MainnetGenesisConstants.ChainId,
                (MainnetChainSchedule.ForkIdentity.BlockHeights, MainnetChainSchedule.ForkIdentity.Timestamps),
                ourHeadProvider,
                logger,
                localKey,
                advertiseSnap2: false);
        }

        public Task<IEthPeer> HandshakeAsync(
            string enode,
            TimeSpan timeout,
            ulong minPeerLatestBlock,
            CancellationToken ct)
            => _inner.HandshakeAsync(enode, timeout, minPeerLatestBlock, ct);
    }
}
