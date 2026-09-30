using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync.Snap.Peers
{
    public sealed class Eth68SnapPeer : ISnapPeer
    {
        private readonly SyncPeerSession _session;

        public Eth68SnapPeer(SyncPeerSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public Task<AccountRangeMessage> GetAccountRangeAsync(
            GetAccountRangeMessage request, CancellationToken ct = default)
            => _session.GetAccountRangeAsync(
                request.RootHash, request.StartingHash, request.LimitHash, request.ResponseBytes, ct);

        public Task<StorageRangesMessage> GetStorageRangesAsync(
            GetStorageRangesMessage request, CancellationToken ct = default)
            => _session.GetStorageRangesAsync(
                request.RootHash, request.AccountHashes,
                request.StartingHash, request.LimitHash, request.ResponseBytes, ct);

        public Task<ByteCodesMessage> GetByteCodesAsync(
            GetByteCodesMessage request, CancellationToken ct = default)
            => _session.GetByteCodesAsync(request.Hashes, request.ResponseBytes, ct);

        public Task<TrieNodesMessage> GetTrieNodesAsync(
            GetTrieNodesMessage request, CancellationToken ct = default)
            => _session.GetTrieNodesAsync(request.RootHash, request.Paths, request.ResponseBytes, ct);
    }
}
