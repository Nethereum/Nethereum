using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Signer;

namespace Nethereum.DevP2P.Sync.Peering
{
    public sealed class ChainPeerHandshakeWorker : IPeerHandshakeWorker
    {
        private readonly byte[] _genesisHash;
        private readonly ulong _networkId;
        private readonly (ulong[] BlockHeights, ulong[] Timestamps) _forkThresholds;
        private readonly Func<Task<(ulong HeadBlock, ulong HeadTime)>> _ourHead;
        public Func<Task<(ulong HeadBlock, ulong HeadTime)>> OurHead => _ourHead;
        private readonly ILogger _logger;
        private readonly EthECKey _localKey;
        private readonly bool _advertiseSnap2;

        /// <param name="forkThresholds">
        /// EIP-2124 config thresholds (block heights and timestamps), the same shape as
        /// <c>IChainProfile.ForkThresholds</c>. Empty means the caller has no opinion on fork identity —
        /// the session falls back to echoing the peer's forkHash, mirroring <see cref="MainnetPeerHandshakeWorker"/>.
        /// </param>
        /// <param name="ourHead">
        /// Resolves OUR OWN chain head (block number + its timestamp) fresh on every dial — FORK_NEXT
        /// changes as the head crosses a boundary, so this must never be cached. Null keeps today's
        /// honest (0, 0).
        /// </param>
        /// <param name="localKey">
        /// This node's wire identity for OUTBOUND dials. When null every dial presents a fresh random
        /// node id, so a peer cannot recognise the dialler as the same node its enode names — which is
        /// what trusted-peer configuration matches on.
        /// </param>
        public ChainPeerHandshakeWorker(
            byte[] genesisHash, ulong networkId,
            (ulong[] BlockHeights, ulong[] Timestamps) forkThresholds,
            Func<Task<(ulong HeadBlock, ulong HeadTime)>> ourHead,
            ILogger logger = null, EthECKey localKey = null, bool advertiseSnap2 = false)
        {
            _genesisHash = genesisHash ?? throw new ArgumentNullException(nameof(genesisHash));
            _networkId = networkId;
            _forkThresholds = forkThresholds;
            _ourHead = ourHead;
            _logger = logger;
            _localKey = localKey;
            _advertiseSnap2 = advertiseSnap2;
        }

        public async Task<IEthPeer> HandshakeAsync(string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
        {
            var (ourHeadBlock, ourHeadTime) = _ourHead != null
                ? await _ourHead().ConfigureAwait(false)
                : (0UL, 0UL);

            return await SyncPeerSession.ConnectAsync(
                    enode, timeout, ct, _genesisHash, _networkId, minPeerLatestBlock,
                    logger: _logger, localKey: _localKey,
                    forkBlocks: _forkThresholds.BlockHeights, forkTimestamps: _forkThresholds.Timestamps,
                    ourHeadBlockNumber: ourHeadBlock, ourHeadTimestamp: ourHeadTime,
                    advertiseSnap2: _advertiseSnap2)
                .ConfigureAwait(false);
        }
    }
}
