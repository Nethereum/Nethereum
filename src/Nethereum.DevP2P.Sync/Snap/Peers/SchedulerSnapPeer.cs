using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync.Snap.Peers
{
    public sealed class SchedulerSnapPeer : ISnapPeer
    {
        private readonly IFetchRequestScheduler _scheduler;

        public SchedulerSnapPeer(IFetchRequestScheduler scheduler)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        public Task<AccountRangeMessage> GetAccountRangeAsync(
            GetAccountRangeMessage request, CancellationToken ct = default)
            => _scheduler.FetchAccountRangeAsync(
                request.RootHash, request.StartingHash, request.LimitHash, request.ResponseBytes, ct);

        public Task<AccountRangeMessage> GetAccountRangeAsync(
            GetAccountRangeMessage request, Func<AccountRangeMessage, bool> verifyResponse, CancellationToken ct = default)
            => _scheduler.FetchAccountRangeAsync(
                request.RootHash, request.StartingHash, request.LimitHash, request.ResponseBytes, verifyResponse, ct);

        public Task<StorageRangesMessage> GetStorageRangesAsync(
            GetStorageRangesMessage request, CancellationToken ct = default)
            => _scheduler.FetchStorageRangesAsync(
                request.RootHash, request.AccountHashes,
                request.StartingHash, request.LimitHash, request.ResponseBytes, ct);

        public Task<StorageRangesMessage> GetStorageRangesAsync(
            GetStorageRangesMessage request, Func<StorageRangesMessage, bool> verifyResponse, CancellationToken ct = default)
            => _scheduler.FetchStorageRangesAsync(
                request.RootHash, request.AccountHashes,
                request.StartingHash, request.LimitHash, request.ResponseBytes, verifyResponse, ct);

        public Task<ByteCodesMessage> GetByteCodesAsync(
            GetByteCodesMessage request, CancellationToken ct = default)
            => _scheduler.FetchByteCodesAsync(request.Hashes, request.ResponseBytes, ct);

        public Task<ByteCodesMessage> GetByteCodesAsync(
            GetByteCodesMessage request, Func<ByteCodesMessage, bool> verifyResponse, CancellationToken ct = default)
            => _scheduler.FetchByteCodesAsync(request.Hashes, request.ResponseBytes, verifyResponse, ct);

        public Task<TrieNodesMessage> GetTrieNodesAsync(
            GetTrieNodesMessage request, CancellationToken ct = default)
            => _scheduler.FetchTrieNodesAsync(request.RootHash, request.Paths, request.ResponseBytes, ct);
    }
}
