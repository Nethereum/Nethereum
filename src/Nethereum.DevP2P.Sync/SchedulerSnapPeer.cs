using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// <see cref="ISnapPeer"/> adapter over the multi-peer
    /// <see cref="IFetchRequestScheduler"/>. Each call dispatches through the
    /// scheduler, so snap traffic inherits the same per-request peer rotation,
    /// retry-on-disconnect, and snap-capability filtering already used by
    /// header / body fetches. There is no notion of "one bound peer" — the
    /// adapter is stateless.
    ///
    /// <para>
    /// This is what <see cref="SnapSyncClient"/> uses in production. The
    /// single-peer <see cref="Eth68SnapPeer"/> stays as the test / AppChain
    /// adapter where a specific session is known up front.
    /// </para>
    /// </summary>
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

        public Task<StorageRangesMessage> GetStorageRangesAsync(
            GetStorageRangesMessage request, CancellationToken ct = default)
            => _scheduler.FetchStorageRangesAsync(
                request.RootHash, request.AccountHashes,
                request.StartingHash, request.LimitHash, request.ResponseBytes, ct);

        public Task<ByteCodesMessage> GetByteCodesAsync(
            GetByteCodesMessage request, CancellationToken ct = default)
            => _scheduler.FetchByteCodesAsync(request.Hashes, request.ResponseBytes, ct);

        public Task<TrieNodesMessage> GetTrieNodesAsync(
            GetTrieNodesMessage request, CancellationToken ct = default)
            => _scheduler.FetchTrieNodesAsync(request.RootHash, request.Paths, request.ResponseBytes, ct);
    }
}
