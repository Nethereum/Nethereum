using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.FullSync
{
    public interface IBackwardBlockWalker
    {
        Task<WalkResult> WalkAsync(
            ulong fromBlockNumber,
            byte[] fromHash,
            ulong toBlockNumber,
            Func<ulong, CancellationToken, Task<(byte[]? hash, bool exists)>> lookupLocalBlock,
            CancellationToken ct,
            ulong noShortCircuitAboveBlock = ulong.MaxValue);
    }

    public sealed class BackwardBlockWalker : IBackwardBlockWalker
    {
        private readonly IFetchRequestScheduler _scheduler;
        private readonly IChainStoreBundle _bundle;
        private readonly BackwardBlockWalkerOptions _options;
        private readonly ILogger _logger;
        private readonly IBlockRootsProvider _rootsProvider;

        public BackwardBlockWalker(
            IFetchRequestScheduler scheduler,
            IChainStoreBundle bundle,
            BackwardBlockWalkerOptions options,
            ILogger logger)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? NullLogger.Instance;
            _rootsProvider = PatriciaBlockRootsProvider.Instance;
        }

        public async Task<WalkResult> WalkAsync(
            ulong fromBlockNumber,
            byte[] fromHash,
            ulong toBlockNumber,
            Func<ulong, CancellationToken, Task<(byte[]? hash, bool exists)>> lookupLocalBlock,
            CancellationToken ct,
            ulong noShortCircuitAboveBlock = ulong.MaxValue)
        {
            if (fromHash == null || fromHash.Length == 0) throw new ArgumentException("fromHash required", nameof(fromHash));
            if (lookupLocalBlock == null) throw new ArgumentNullException(nameof(lookupLocalBlock));
            if (fromBlockNumber < toBlockNumber)
                throw new ArgumentException(
                    $"fromBlockNumber ({fromBlockNumber}) must be >= toBlockNumber ({toBlockNumber})",
                    nameof(fromBlockNumber));

            if (noShortCircuitAboveBlock != ulong.MaxValue)
            {
                var innerLookup = lookupLocalBlock;
                lookupLocalBlock = (bn, c) => bn > noShortCircuitAboveBlock
                    ? Task.FromResult<(byte[]? hash, bool exists)>((null, false))
                    : innerLookup(bn, c);
            }

            ulong currentTopBlock = fromBlockNumber;
            byte[] anchorHash = fromHash;
            ulong headersWritten = 0;
            ulong lastLoggedWritten = 0;
            long bodiesWrittenAtomic = 0;
            ulong bottomReached = fromBlockNumber;
            ulong topReached = fromBlockNumber;

            var queueCapacity = _options.MaxQueuedBodyBatches > 0 ? _options.MaxQueuedBodyBatches : 4;
            var bodyQueue = Channel.CreateBounded<BodyBatch>(new BoundedChannelOptions(queueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
            });

            using var bodyCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var bodyWorker = _options.HeadersOnly
                ? Task.CompletedTask
                : Task.Run(() => RunBodyWorkerAsync(bodyQueue.Reader, bodyCts.Token, c =>
                    Interlocked.Add(ref bodiesWrittenAtomic, (long)c)), bodyCts.Token);

            async Task<WalkResult> DrainAndFinaliseAsync(WalkerExitReason reason, ulong? divergence, bool success)
            {
                bodyQueue.Writer.TryComplete();
                try { await bodyWorker.ConfigureAwait(false); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch { }
                return Finalise(
                    reason,
                    bottomReached: bottomReached,
                    topReached: topReached,
                    headersWritten: headersWritten,
                    bodiesWritten: (ulong)Interlocked.Read(ref bodiesWrittenAtomic),
                    divergenceBlock: divergence,
                    success: success);
            }

            try
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    ulong remaining = currentTopBlock - toBlockNumber + 1;
                    ulong batchSize = (ulong)_options.HeaderBatchSize;
                    ulong limit = remaining < batchSize ? remaining : batchSize;

                    var fetched = await FetchHeaderBatchWithRetriesAsync(
                        currentTopBlock, limit, anchorHash, ct).ConfigureAwait(false);

                    if (fetched.ExitReason.HasValue)
                    {
                        return await DrainAndFinaliseAsync(fetched.ExitReason.Value, divergence: null, success: false)
                            .ConfigureAwait(false);
                    }

                    var headers = fetched.Headers!;
                    var hashes = fetched.Hashes!;
                    int last = headers.Count - 1;
                    ulong batchBottomBlock = (ulong)headers[last].BlockNumber.ToBigInteger();

                    var (localHash, localExists) = await lookupLocalBlock(batchBottomBlock, ct).ConfigureAwait(false);
                    if (localExists)
                    {
                        if (localHash != null && ByteUtil.AreEqual(localHash, hashes[last]))
                        {
                            await PersistHeadersAtomicAsync(headers, hashes, batchBottomBlock).ConfigureAwait(false);
                            headersWritten += (ulong)headers.Count;
                            bottomReached = batchBottomBlock;

                            if (!_options.HeadersOnly)
                        await bodyQueue.Writer.WriteAsync(new BodyBatch(headers, hashes), ct).ConfigureAwait(false);

                            return await DrainAndFinaliseAsync(WalkerExitReason.MetExistingStore, divergence: null, success: true)
                                .ConfigureAwait(false);
                        }

                        return await DrainAndFinaliseAsync(WalkerExitReason.LastKnownGoodDivergence,
                            divergence: batchBottomBlock, success: false).ConfigureAwait(false);
                    }

                    await PersistHeadersAtomicAsync(headers, hashes, batchBottomBlock).ConfigureAwait(false);
                    headersWritten += (ulong)headers.Count;
                    bottomReached = batchBottomBlock;

                    if (headersWritten - lastLoggedWritten >= 50_000)
                    {
                        lastLoggedWritten = headersWritten;
                        _logger.LogInformation(
                            "backward-walk: laid {Written} headers, frontier=block {Block} (top {Top}, target {Target})",
                            headersWritten, batchBottomBlock, fromBlockNumber, toBlockNumber);
                    }

                    if (!_options.HeadersOnly)
                        await bodyQueue.Writer.WriteAsync(new BodyBatch(headers, hashes), ct).ConfigureAwait(false);

                    if (batchBottomBlock == 0)
                    {
                        return await DrainAndFinaliseAsync(WalkerExitReason.StructuralGenesis, divergence: null, success: true)
                            .ConfigureAwait(false);
                    }

                    if (batchBottomBlock <= toBlockNumber)
                    {
                        return await DrainAndFinaliseAsync(WalkerExitReason.ReachedTarget, divergence: null, success: true)
                            .ConfigureAwait(false);
                    }

                    currentTopBlock = batchBottomBlock - 1;
                    anchorHash = headers[last].ParentHash;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                bodyCts.Cancel();
                bodyQueue.Writer.TryComplete();
                try { await bodyWorker.ConfigureAwait(false); }
                catch { }
                throw;
            }
            catch
            {
                bodyQueue.Writer.TryComplete();
                try { await bodyWorker.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception drainEx)
                {
                    _logger.LogWarning(drainEx, "snap.walker.body_drain_failed");
                }
                bodyCts.Cancel();
                throw;
            }
        }

        private async Task RunBodyWorkerAsync(
            ChannelReader<BodyBatch> reader,
            CancellationToken ct,
            Action<ulong> reportPersisted)
        {
            try
            {
                while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var batch))
                    {
                        var persisted = await PhaseB_FetchBodiesAndPersistAsync(batch.Headers, batch.Hashes, ct)
                            .ConfigureAwait(false);
                        if (persisted > 0) reportPersisted(persisted);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        }

        private readonly struct BodyBatch
        {
            public BodyBatch(IList<BlockHeader> headers, IList<byte[]> hashes)
            {
                Headers = headers;
                Hashes = hashes;
            }
            public IList<BlockHeader> Headers { get; }
            public IList<byte[]> Hashes { get; }
        }

        private async Task<HeaderFetchOutcome> FetchHeaderBatchWithRetriesAsync(
            ulong currentTopBlock, ulong limit, byte[] anchorHash, CancellationToken ct)
        {
            for (int attempt = 0; attempt < _options.MaxAnchorRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                List<BlockHeader> headers;
                Guid servingPeerId;
                try
                {
                    (headers, servingPeerId) = await _scheduler
                        .FetchHeadersWithPeerAsync(currentTopBlock, limit, ct, reverse: true)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "BackwardBlockWalker: header fetch at top {Top} limit {Limit} failed on attempt {Attempt}",
                        currentTopBlock, limit, attempt + 1);
                    await DelayWithCancel(_options.PeerRetryDelay, ct).ConfigureAwait(false);
                    continue;
                }

                if (headers == null || headers.Count == 0)
                {
                    _logger.LogWarning(
                        "BackwardBlockWalker: empty header batch at top {Top} (attempt {Attempt})",
                        currentTopBlock, attempt + 1);
                    if (servingPeerId != Guid.Empty) _scheduler.QuarantineHeaderPeer(servingPeerId);
                    await DelayWithCancel(_options.PeerRetryDelay, ct).ConfigureAwait(false);
                    continue;
                }

                if (!HeadersAreDescendingContiguous(headers, currentTopBlock))
                {
                    _logger.LogWarning(
                        "BackwardBlockWalker: non-descending-contiguous header batch at top {Top} (attempt {Attempt})",
                        currentTopBlock, attempt + 1);
                    if (servingPeerId != Guid.Empty) _scheduler.QuarantineHeaderPeer(servingPeerId);
                    await DelayWithCancel(_options.PeerRetryDelay, ct).ConfigureAwait(false);
                    continue;
                }

                var hashes = new byte[headers.Count][];
                for (int i = 0; i < headers.Count; i++)
                    hashes[i] = HashHeader(headers[i]);

                if (!ByteUtil.AreEqual(hashes[0], anchorHash))
                {
                    _logger.LogWarning(
                        "BackwardBlockWalker: anchor mismatch at top {Top} (attempt {Attempt})",
                        currentTopBlock, attempt + 1);
                    if (servingPeerId != Guid.Empty) _scheduler.QuarantineHeaderPeer(servingPeerId);
                    await DelayWithCancel(_options.PeerRetryDelay, ct).ConfigureAwait(false);
                    continue;
                }

                if (!ValidateBackwardParentChain(headers, hashes, out var brokenAt))
                {
                    _logger.LogWarning(
                        "BackwardBlockWalker: backward parent-hash chain break at index {Index} of batch top {Top} (attempt {Attempt})",
                        brokenAt, currentTopBlock, attempt + 1);
                    if (servingPeerId != Guid.Empty) _scheduler.QuarantineHeaderPeer(servingPeerId);
                    await DelayWithCancel(_options.PeerRetryDelay, ct).ConfigureAwait(false);
                    continue;
                }

                return new HeaderFetchOutcome { Headers = headers, Hashes = hashes };
            }

            return new HeaderFetchOutcome { ExitReason = WalkerExitReason.PeerPoolEmpty };
        }

        private async Task PersistHeadersAtomicAsync(
            IList<BlockHeader> headers, IList<byte[]> hashes, ulong batchBottomBlock)
        {
            var current = _bundle.Metadata.GetLastFetchedHeader();
            var advanceCursor = current == 0 || batchBottomBlock < current;

            using var batch = _bundle.BeginBatch();
            for (int i = 0; i < headers.Count; i++)
            {
                batch.PutHeader(headers[i], hashes[i]);
            }
            if (advanceCursor)
                batch.SetLastFetchedHeader(batchBottomBlock);
            await batch.CommitAsync().ConfigureAwait(false);
        }

        private async Task<ulong> PhaseB_FetchBodiesAndPersistAsync(
            IList<BlockHeader> headers, IList<byte[]> hashes, CancellationToken ct)
        {
            var hashList = new List<byte[]>(hashes.Count);
            for (int i = 0; i < hashes.Count; i++) hashList.Add(hashes[i]);

            BodyFetchResult result;
            try
            {
                result = await _scheduler.FetchBodiesAsync(hashList, excludePeers: null, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "BackwardBlockWalker: body fetch failed for batch bottom={Bottom} top={Top} — skipping bodies, headers progress",
                    headers[headers.Count - 1].BlockNumber.ToBigInteger(),
                    headers[0].BlockNumber.ToBigInteger());
                return 0;
            }

            if (result?.Bodies == null || result.Bodies.Count == 0) return 0;

            var realigned = BlockBatchValidator.RealignBodies(headers, result.Bodies, _rootsProvider, out var unmatchedAt);
            var paired = realigned.Count;
            if (paired == 0) return 0;

            if (!BlockBatchValidator.ValidateBodies(headers, realigned, paired, _rootsProvider))
            {
                _logger.LogWarning(
                    "BackwardBlockWalker: body root mismatch after realign in batch bottom={Bottom} top={Top}",
                    headers[headers.Count - 1].BlockNumber.ToBigInteger(),
                    headers[0].BlockNumber.ToBigInteger());
                return 0;
            }

            ulong persisted = 0;
            ulong lowestPersistedBlock = ulong.MaxValue;
            for (int i = 0; i < paired; i++)
            {
                var header = headers[i];
                var hash = hashes[i];
                var body = realigned[i];

                await _bundle.Uncles
                    .SaveAsync(hash, body?.Uncles ?? new List<BlockHeader>())
                    .ConfigureAwait(false);

                if (body?.Withdrawals != null)
                {
                    await _bundle.Withdrawals
                        .SaveAsync(hash, body.Withdrawals)
                        .ConfigureAwait(false);
                }

                if (body?.Transactions != null)
                {
                    var blockNumber = header.BlockNumber.ToBigInteger();
                    for (int j = 0; j < body.Transactions.Count; j++)
                    {
                        await _bundle.Transactions
                            .SaveAsync(body.Transactions[j], hash, j, blockNumber)
                            .ConfigureAwait(false);
                    }
                }

                var blkNum = (ulong)header.BlockNumber.ToBigInteger();
                if (blkNum < lowestPersistedBlock) lowestPersistedBlock = blkNum;
                persisted++;
            }

            if (persisted > 0)
            {
                var currentBodyCursor = _bundle.Metadata.GetLastFetchedBody();
                if (currentBodyCursor == 0 || lowestPersistedBlock < currentBodyCursor)
                    _bundle.Metadata.SetLastFetchedBody(lowestPersistedBlock);
            }

            return persisted;
        }

        private static bool ValidateBackwardParentChain(
            IList<BlockHeader> headers, IList<byte[]> hashes, out int brokenAt)
        {
            for (int i = 0; i < headers.Count - 1; i++)
            {
                if (!ByteUtil.AreEqual(headers[i].ParentHash, hashes[i + 1]))
                {
                    brokenAt = i;
                    return false;
                }
            }
            brokenAt = -1;
            return true;
        }

        private static bool HeadersAreDescendingContiguous(IList<BlockHeader> headers, ulong topBlock)
        {
            if ((ulong)headers[0].BlockNumber.ToBigInteger() != topBlock) return false;
            for (int i = 1; i < headers.Count; i++)
            {
                var prev = (long)headers[i - 1].BlockNumber.ToBigInteger();
                var cur = (long)headers[i].BlockNumber.ToBigInteger();
                if (cur != prev - 1) return false;
            }
            return true;
        }

        private byte[] HashHeader(BlockHeader header)
        {
            var encoded = BlockHeaderEncoder.Current.Encode(header);
            return Sha3Keccack.Current.CalculateHash(encoded);
        }

        private static WalkResult Finalise(
            WalkerExitReason reason,
            ulong bottomReached,
            ulong topReached,
            ulong headersWritten,
            ulong bodiesWritten,
            ulong? divergenceBlock,
            bool success)
            => new WalkResult(
                Success: success,
                SkeletonTopBlock: topReached,
                SkeletonBottomBlock: bottomReached,
                HeadersWritten: headersWritten,
                BodiesWritten: bodiesWritten,
                MetExistingStore: reason == WalkerExitReason.MetExistingStore,
                ExitReason: reason,
                DivergenceBlock: divergenceBlock);

        private static async Task DelayWithCancel(TimeSpan delay, CancellationToken ct)
        {
            if (delay <= TimeSpan.Zero) return;
            try { await Task.Delay(delay, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        }

        private struct HeaderFetchOutcome
        {
            public List<BlockHeader>? Headers;
            public byte[][]? Hashes;
            public WalkerExitReason? ExitReason;
        }
    }

    public sealed record WalkResult(
        bool             Success,
        ulong            SkeletonTopBlock,
        ulong            SkeletonBottomBlock,
        ulong            HeadersWritten,
        ulong            BodiesWritten,
        bool             MetExistingStore,
        WalkerExitReason ExitReason,
        ulong?           DivergenceBlock);

    public sealed class BackwardBlockWalkerOptions
    {
        public int HeaderBatchSize { get; init; } = 192;

        public int MaxAnchorRetries { get; init; } = 5;

        public TimeSpan PeerRetryDelay { get; init; } = TimeSpan.FromSeconds(2);

        public int MaxQueuedBodyBatches { get; init; } = 4;

        public bool HeadersOnly { get; init; } = false;
    }
}
