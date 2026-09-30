using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Nethereum.CoreChain;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.CoreChain.Validation;

namespace Nethereum.DevP2P.Sync.Peering
{
    public sealed class PeerHeadCanonicalSource : ICanonicalStateRootSource
    {
        private readonly PeerPoolManager _pool;
        private readonly bool _trustedPeersOnly;

        public PeerHeadCanonicalSource(PeerPoolManager pool, bool trustedPeersOnly = false)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _trustedPeersOnly = trustedPeersOnly;
        }

        public string Name => _trustedPeersOnly ? "TrustedPeerHead" : "PeerHead";

        public async Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
        {
            var peer = BestPeer();
            if (peer == null) return null;

            var headers = await peer.GetHeadersAsync(peer.PeerLatestBlock, 1, ct).ConfigureAwait(false);
            if (headers == null || headers.Count == 0) return null;

            return new CanonicalTip
            {
                BlockNumber = peer.PeerLatestBlock,
                BlockHash = peer.PeerLatestBlockHash ?? Array.Empty<byte>(),
                StateRoot = headers[0].StateRoot ?? Array.Empty<byte>()
            };
        }

        public async Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(
            ulong blockNumber,
            CancellationToken ct)
        {
            var peer = BestPeer();
            if (peer == null) return (null, null);

            var headers = await peer.GetHeadersAsync(blockNumber, 1, ct).ConfigureAwait(false);
            if (headers == null || headers.Count == 0) return (null, null);

            return (headers[0].StateRoot, BlockHashCalculator.ForHeader(headers[0]));
        }

        private SyncPeerSession BestPeer() =>
            SelectHeadPeer(_pool.ActivePeers, _trustedPeersOnly) as SyncPeerSession;

        internal static IEthPeer SelectHeadPeer(IEnumerable<IEthPeer> peers, bool trustedPeersOnly)
        {
            if (peers == null) return null;
            var candidates = trustedPeersOnly ? peers.Where(peer => peer.IsTrusted) : peers;

            return candidates
                .OrderByDescending(peer => peer.PeerLatestBlock)
                .FirstOrDefault();
        }
    }
}
