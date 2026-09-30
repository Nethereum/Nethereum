using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;

namespace Nethereum.DevP2P.Sync.Snap.CatchUp
{
    public sealed class PeerPoolBlockAccessListPeerSource : IBlockAccessListPeerSource
    {
        private readonly IPeerPool _pool;

        public PeerPoolBlockAccessListPeerSource(IPeerPool pool)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        }

        public IReadOnlyList<IBlockAccessListPeer> GetServiceablePeers()
            => _pool.ActivePeers
                .OfType<SyncPeerSession>()
                .Where(session => session.SupportsSnap2)
                .Select(session => (IBlockAccessListPeer)new SyncPeerSessionBlockAccessListPeer(session))
                .ToList();

        private sealed class SyncPeerSessionBlockAccessListPeer : IBlockAccessListPeer
        {
            private readonly SyncPeerSession _session;

            public SyncPeerSessionBlockAccessListPeer(SyncPeerSession session)
            {
                _session = session ?? throw new ArgumentNullException(nameof(session));
            }

            public string Id => _session.Id.ToString();

            public async Task<IReadOnlyList<byte[]>> RequestBlockAccessListsAsync(
                IReadOnlyList<byte[]> blockHashes, ulong responseBytes, CancellationToken ct)
            {
                var response = await _session.GetBlockAccessListsAsync(
                    blockHashes.ToList(), responseBytes, ct).ConfigureAwait(false);
                return response.BlockAccessListsByBlock;
            }
        }
    }
}
