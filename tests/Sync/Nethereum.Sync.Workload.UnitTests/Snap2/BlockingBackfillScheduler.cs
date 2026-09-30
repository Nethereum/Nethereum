using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.Chain.TestData.UnitTests
{
    internal sealed class BlockingBackfillScheduler : IFetchRequestScheduler
    {
        private readonly ISnapRequestHandler _snap;
        private int _liveRequests;
        private int _maxLiveRequests;

        public BlockingBackfillScheduler(ISnapRequestHandler snap)
        {
            _snap = snap ?? throw new ArgumentNullException(nameof(snap));
        }

        public int LiveRequests => Volatile.Read(ref _liveRequests);

        public int MaxLiveRequests => Volatile.Read(ref _maxLiveRequests);

        public TimeSpan DrainDelay { get; set; } = TimeSpan.Zero;

        public Action OnTrieNodes { get; set; }

        public int TrieNodeRequests => Volatile.Read(ref _trieNodeRequests);

        private int _trieNodeRequests;

        public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
            => BlockUntilCancelledAsync<List<BlockHeader>>(ct);

        public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            => BlockUntilCancelledAsync<List<BlockBody>>(ct);

        public Task<BodyFetchResult> FetchBodiesAsync(
            IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct)
            => BlockUntilCancelledAsync<BodyFetchResult>(ct);

        public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            => BlockUntilCancelledAsync<List<List<Receipt>>>(ct);

        public Task<AccountRangeMessage> FetchAccountRangeAsync(
            byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
            => _snap.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RootHash = stateRoot,
                StartingHash = startingHash,
                LimitHash = limitHash,
                ResponseBytes = responseBytes,
            }, ct);

        public Task<StorageRangesMessage> FetchStorageRangesAsync(
            byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash,
            ulong responseBytes, CancellationToken ct)
            => _snap.GetStorageRangesAsync(new GetStorageRangesMessage
            {
                RootHash = stateRoot,
                AccountHashes = accountHashes,
                StartingHash = startingHash ?? Array.Empty<byte>(),
                LimitHash = limitHash ?? Array.Empty<byte>(),
                ResponseBytes = responseBytes,
            }, ct);

        public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
            => _snap.GetByteCodesAsync(new GetByteCodesMessage { Hashes = codeHashes, ResponseBytes = responseBytes }, ct);

        public Task<TrieNodesMessage> FetchTrieNodesAsync(
            byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
        {
            Interlocked.Increment(ref _trieNodeRequests);
            OnTrieNodes?.Invoke();
            return _snap.GetTrieNodesAsync(new GetTrieNodesMessage { RootHash = stateRoot, Paths = paths, ResponseBytes = responseBytes }, ct);
        }

        private async Task<T> BlockUntilCancelledAsync<T>(CancellationToken ct)
        {
            RecordLive(Interlocked.Increment(ref _liveRequests));
            try
            {
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return default;
            }
            catch (OperationCanceledException) when (DrainDelay > TimeSpan.Zero)
            {
                await Task.Delay(DrainDelay, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _liveRequests);
            }
        }

        private void RecordLive(int live)
        {
            int max;
            while (live > (max = Volatile.Read(ref _maxLiveRequests)))
                if (Interlocked.CompareExchange(ref _maxLiveRequests, live, max) == max) return;
        }
    }
}
