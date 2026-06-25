using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Phase 1 historical block + receipts backfill, structured as four
    /// concurrent stages sharing one <see cref="BlockTaskQueue"/>.
    /// <para>
    /// The pipeline is intentionally producer/consumer-shaped rather than
    /// batch-serial so a slow peer or a slow stage can never block a faster
    /// one:
    /// </para>
    /// <list type="number">
    /// <item><b>Header producer</b> — keeps a small number of header
    /// requests in flight (one per worker peer), reorders out-of-order
    /// responses by start-block, validates parent-chain integrity, and
    /// enqueues headers into the queue in strict block-number order.</item>
    /// <item><b>Body fetcher loop</b> — wakes on either new work or a
    /// peer-dispatch completion. Iterates the active peer pool; for any
    /// peer that isn't already mid-request and has reservable work, fires
    /// one body request. Hands the response back to the queue for
    /// content-addressed matching.</item>
    /// <item><b>Receipt fetcher loop</b> — symmetric to body fetcher; runs
    /// independently against the same queue and the same peer pool.</item>
    /// <item><b>Persistence drain</b> — pulls the longest fully-ready
    /// prefix off the queue, fans out per-block writes concurrently, and
    /// commits the metadata cursor only over the contiguous prefix it
    /// actually wrote, so a kill mid-batch leaves the cursor consistent
    /// with on-disk content.</item>
    /// </list>
    /// <para>
    /// Failure modes are explicit and bounded. A peer that returns no
    /// usable body or receipt has its reservation reverted to Pending and
    /// the lacking-set entry stops the queue from handing the same block
    /// back to it. A peer that disconnects mid-request triggers
    /// <see cref="BlockTaskQueue.ReleasePeer"/> via the exception path,
    /// freeing any reservations it held. The pipeline never deadlocks
    /// because every wake-up is signalled outside the queue lock.
    /// </para>
    /// </summary>
    public sealed class ParallelBlockBackfiller
    {
        public const ulong DefaultHeaderBatchSize = 192;
        public const int DefaultBodyCapacityPerPeer = 128;
        public const int DefaultReceiptCapacityPerPeer = 256;
        public const int HeaderProducerLookaheadBlocks = 16_384;

        // How many header batches we keep in flight against the peer pool at
        // any moment. The producer is the single upstream feeder for the
        // queue; if it sits idle waiting on one RTT at a time, the body/
        // receipt workers starve. Four concurrent in-flight is enough to
        // saturate the consumers for typical peer-pool RTTs without putting
        // the working set above HeaderProducerLookaheadBlocks.
        public const int HeaderProducerInFlight = 4;

        /// <summary>
        /// Per-request wall-clock cap for one body or receipt dispatch
        /// against a single peer. If a peer accepts the request but never
        /// replies (silent stall — distinct from disconnect), the dispatch
        /// awaits indefinitely on the underlying RPC unless we cancel.
        /// On expiry the dispatch is cancelled, the peer's reservation is
        /// released back to Pending so another peer can pick it up, and
        /// the catch path logs the timeout. The value matches the scheduler's
        /// default of 30s.
        /// </summary>
        public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

        private readonly IFetchRequestScheduler _scheduler;
        private readonly IPeerPool _pool;
        private readonly IPeerRequestWorker _worker;
        private readonly IChainStoreBundle _bundle;
        private readonly IBlockRootsProvider _rootsProvider;
        private readonly IChainActivations? _activations;
        private readonly ILogger _logger;
        private readonly SnapSyncMetrics? _metrics;
        private readonly Sha3Keccack _keccak = new();

        private long _txsWritten;
        private long _receiptsWritten;
        private long _blocksWritten;
        private bool _headersFromStore;

        public ParallelBlockBackfiller(
            IFetchRequestScheduler scheduler,
            IPeerPool pool,
            IPeerRequestWorker worker,
            IChainStoreBundle bundle,
            IBlockRootsProvider? rootsProvider = null,
            ILogger? logger = null,
            SnapSyncMetrics? metrics = null,
            IChainActivations? activations = null)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _worker = worker ?? throw new ArgumentNullException(nameof(worker));
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _rootsProvider = rootsProvider ?? PatriciaBlockRootsProvider.Instance;
            _logger = logger ?? NullLogger.Instance;
            _metrics = metrics;
            _activations = activations;
        }

        public sealed class BackfillResult
        {
            public bool Ran { get; init; }
            public string? SkipReason { get; init; }
            public ulong BlocksWritten { get; init; }
            public ulong TransactionsWritten { get; init; }
            public ulong ReceiptsWritten { get; init; }
            public ulong EndBlock { get; init; }
        }

        public async Task<BackfillResult> BackfillAsync(
            ulong startBlock, ulong endBlock, CancellationToken ct)
            => await BackfillAsync(startBlock, endBlock, headersFromStore: false, ct).ConfigureAwait(false);

        /// <summary>
        /// Run the parallel body+receipt+persist pipeline over a block range.
        /// When <paramref name="headersFromStore"/> is true, headers are LOADED
        /// from <c>_bundle.Blocks</c> (laid down by the backward header skeleton,
        /// e.g. <see cref="BackwardBlockWalker"/>) instead of fetched — this is
        /// the skeleton/filler split: a skeleton lays headers, then a concurrent
        /// backfiller fills bodies/receipts over them. When false it fetches
        /// headers itself (legacy standalone behaviour).
        /// </summary>
        public async Task<BackfillResult> BackfillAsync(
            ulong startBlock, ulong endBlock, bool headersFromStore, CancellationToken ct)
        {
            _headersFromStore = headersFromStore;
            if (endBlock < startBlock)
                return new BackfillResult { Ran = false, SkipReason = "endBlock < startBlock" };

            // Resume cursor: in headersFromStore mode the skeleton owns the header
            // cursor, so the filler tracks its own progress via the body cursor.
            var resume = headersFromStore
                ? _bundle.Metadata.GetLastFetchedBody()
                : _bundle.Metadata.GetLastFetchedHeader();
            ulong cursor;
            if (resume == 0) cursor = startBlock;
            else cursor = resume + 1 > startBlock ? resume + 1 : startBlock;
            if (cursor > endBlock)
                return new BackfillResult { Ran = false, SkipReason = $"already at {resume}" };

            var queue = new BlockTaskQueue(_rootsProvider, cursor, _activations);

            _logger.LogInformation(
                "Phase 1 backfill (parallel): starting at block {Start} → {End} ({Total} blocks)",
                cursor, endBlock, endBlock - cursor + 1);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var stagesCt = cts.Token;

            // Four stages share a single cancellation source. Persistence is
            // the one that knows the terminal state (cursor passes endBlock);
            // when it returns we cancel everything else so the worker loops
            // and the header producer exit cleanly.
            var headerTask = Task.Run(
                () => headersFromStore
                    ? RunHeaderLoaderAsync(queue, cursor, endBlock, stagesCt)
                    : RunHeaderProducerAsync(queue, cursor, endBlock, stagesCt), stagesCt);

            var bodyTask = Task.Run(
                () => RunBodyFetcherAsync(queue, stagesCt), stagesCt);

            var receiptTask = Task.Run(
                () => RunReceiptFetcherAsync(queue, stagesCt), stagesCt);

            var persistTask = Task.Run(
                () => RunPersistenceAsync(queue, endBlock, stagesCt), stagesCt);

            try
            {
                await persistTask.ConfigureAwait(false);
            }
            finally
            {
                cts.Cancel();
                try { await Task.WhenAll(headerTask, bodyTask, receiptTask).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Phase 1 backfill: a stage threw during shutdown");
                }
            }

            return new BackfillResult
            {
                Ran = true,
                BlocksWritten = (ulong)Interlocked.Read(ref _blocksWritten),
                TransactionsWritten = (ulong)Interlocked.Read(ref _txsWritten),
                ReceiptsWritten = (ulong)Interlocked.Read(ref _receiptsWritten),
                EndBlock = endBlock,
            };
        }

        // ---------------------------------------------------------------
        // Stage 1 (alt): Header loader — seed the queue from headers already
        // persisted by the backward skeleton instead of fetching them. Headers
        // were parent-hash-validated on the way in by the skeleton, so this
        // loader trusts the store; bodies/receipts still validate against each
        // header's txRoot/ReceiptHash downstream. Feeds the concurrent
        // body/receipt filler from the skeleton's persisted header chain.
        // ---------------------------------------------------------------

        private async Task RunHeaderLoaderAsync(
            BlockTaskQueue queue, ulong startCursor, ulong endBlock, CancellationToken ct)
        {
            ulong next = startCursor;
            while (!ct.IsCancellationRequested && next <= endBlock)
            {
                // Same memory bound as the fetch producer.
                while (queue.Pending >= HeaderProducerLookaheadBlocks && !ct.IsCancellationRequested)
                {
                    try { await Task.Delay(50, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                }

                ulong remaining = endBlock - next + 1;
                ulong take = remaining < DefaultHeaderBatchSize ? remaining : DefaultHeaderBatchSize;

                int loaded = 0;
                for (ulong n = next; n < next + take && !ct.IsCancellationRequested; n++)
                {
                    var header = await _bundle.Blocks.GetByNumberAsync((BigInteger)n).ConfigureAwait(false);
                    var hash = header == null
                        ? null
                        : await _bundle.Blocks.GetHashByNumberAsync((BigInteger)n).ConfigureAwait(false);

                    // Skeleton hasn't laid this header down yet — stop the batch
                    // here; we retry from `next` after a short wait below.
                    if (header == null || hash == null) break;

                    queue.EnqueueHeader(header, hash);
                    loaded++;
                }

                if (loaded == 0)
                {
                    try { await Task.Delay(100, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                    continue;
                }

                next += (ulong)loaded;
            }
        }

        // ---------------------------------------------------------------
        // Stage 1: Header producer
        // ---------------------------------------------------------------

        private async Task RunHeaderProducerAsync(
            BlockTaskQueue queue, ulong startCursor, ulong endBlock, CancellationToken ct)
        {
            // We dispatch N header batches concurrently so the producer
            // can keep up with the consumers, but the queue downstream
            // requires strict block-number order (it walks parent-hash
            // chains). The "pending" sorted map below is a reorder buffer:
            // batches land here in completion order, but we only drain to
            // the queue in start-block order, retrying the chain anchor
            // when an out-of-order delivery completes ahead of a missing
            // earlier batch.
            ulong nextDispatch = startCursor;
            ulong nextEnqueue = startCursor;
            var lastPersistedHash = await LoadLastPersistedHashAsync(startCursor).ConfigureAwait(false);
            var pending = new SortedDictionary<ulong, (List<BlockHeader> Headers, byte[][] Hashes)>();
            var inFlight = new List<Task<(ulong Start, List<BlockHeader>? Headers)>>();

            while (!ct.IsCancellationRequested && (nextEnqueue <= endBlock || inFlight.Count > 0 || pending.Count > 0))
            {
                // Backpressure: pause the producer when the queue is full
                // enough. This keeps memory bounded — without it a fast
                // peer pool would let us pull in millions of headers
                // before persistence catches up.
                while (queue.Pending >= HeaderProducerLookaheadBlocks && !ct.IsCancellationRequested)
                {
                    try { await Task.Delay(50, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                }

                // Dispatch new batches up to the in-flight cap. The
                // scheduler picks a peer per batch; concurrent batches
                // naturally land on different peers.
                while (inFlight.Count < HeaderProducerInFlight
                       && nextDispatch <= endBlock
                       && !ct.IsCancellationRequested)
                {
                    var remaining = endBlock - nextDispatch + 1;
                    var take = remaining < DefaultHeaderBatchSize ? remaining : DefaultHeaderBatchSize;
                    var start = nextDispatch;
                    inFlight.Add(FetchHeaderBatchAsync(start, take, ct));
                    nextDispatch += take;
                }

                if (inFlight.Count == 0 && pending.Count == 0) break;

                // Block until any one dispatched batch finishes. The body
                // and receipt fetchers are independently making progress
                // on whatever is already in the queue while we wait here.
                if (inFlight.Count > 0)
                {
                    var winner = await Task.WhenAny(inFlight).ConfigureAwait(false);
                    inFlight.Remove(winner);
                    (ulong Start, List<BlockHeader>? Headers) result;
                    try { result = await winner.ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Phase 1 backfill: header dispatch failed; retrying");
                        await DelayWithCancel(500, ct).ConfigureAwait(false);
                        continue;
                    }

                    if (result.Headers == null || result.Headers.Count == 0)
                    {
                        // Empty / failed response — rewind nextDispatch so
                        // we re-issue that same window to (probably) a
                        // different peer next iteration.
                        await DelayWithCancel(200, ct).ConfigureAwait(false);
                        nextDispatch = Math.Min(nextDispatch, result.Start);
                        continue;
                    }

                    if (!HeadersAreContiguous(result.Headers, result.Start))
                    {
                        // Peer returned a partial / gappy slice; treat the
                        // whole batch as a retry rather than gluing pieces
                        // together. Cheap to ask again, expensive to debug
                        // a quietly-poisoned chain.
                        _logger.LogWarning(
                            "Phase 1 backfill: non-contiguous header batch at {Block} — retrying",
                            result.Start);
                        nextDispatch = Math.Min(nextDispatch, result.Start);
                        continue;
                    }

                    var hashes = new byte[result.Headers.Count][];
                    for (int i = 0; i < result.Headers.Count; i++)
                        hashes[i] = HashHeader(result.Headers[i]);

                    // Drop into the reorder buffer; the drain below will
                    // pull it out when the chain reaches its start-block.
                    pending[result.Start] = (result.Headers, hashes);
                }

                // Drain any contiguous prefix of completed batches into
                // the queue. Stops at the first gap so the consumers only
                // ever see headers whose parent-chain has been verified.
                while (pending.TryGetValue(nextEnqueue, out var entry))
                {
                    pending.Remove(nextEnqueue);
                    if (!BlockBatchValidator.ValidateParentChain(entry.Headers, entry.Hashes, lastPersistedHash, out var brokenAt))
                    {
                        // Parent-hash didn't match — peer served a fork or
                        // a stale window. Rewind so the same batch gets
                        // re-fetched. Anything we'd already enqueued for
                        // earlier blocks stays in the queue; the gap will
                        // self-heal on retry.
                        _logger.LogWarning(
                            "Phase 1 backfill: parent-hash chain break at index {Index} of batch starting block {Block} — retrying",
                            brokenAt, nextEnqueue);
                        nextDispatch = Math.Min(nextDispatch, nextEnqueue);
                        break;
                    }

                    for (int i = 0; i < entry.Headers.Count; i++)
                        queue.EnqueueHeader(entry.Headers[i], entry.Hashes[i]);

                    lastPersistedHash = entry.Hashes[entry.Hashes.Length - 1];
                    nextEnqueue += (ulong)entry.Headers.Count;
                }
            }
        }

        private async Task<(ulong Start, List<BlockHeader>? Headers)> FetchHeaderBatchAsync(
            ulong start, ulong take, CancellationToken ct)
        {
            try
            {
                var headers = await _scheduler.FetchHeadersAsync(start, take, ct).ConfigureAwait(false);
                return (start, headers);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Header batch {Start}+{Take} fetch failed", start, take);
                return (start, null);
            }
        }

        // ---------------------------------------------------------------
        // Stage 2: Body fetcher loop — per-peer pull workers
        // ---------------------------------------------------------------

        private async Task RunBodyFetcherAsync(BlockTaskQueue queue, CancellationToken ct)
        {
            // inFlight tracks one dispatch task per peer-id. The queue's
            // one-in-flight invariant means a peer present here has an
            // open body reservation; we must NOT call ReserveBodies on
            // them again until the task drains.
            var inFlight = new Dictionary<Guid, Task>();
            while (!ct.IsCancellationRequested)
            {
                // Try to reserve work for every peer that isn't already busy.
                // Reservation may legitimately return empty — either no work
                // available, or the peer is in this block's lacking-set.
                foreach (var peer in _pool.ActivePeers)
                {
                    if (ct.IsCancellationRequested) return;
                    if (inFlight.ContainsKey(peer.Id)) continue;
                    // Re-confirm pool membership: ActivePeers is a snapshot
                    // and the peer may have disconnected before we get to
                    // reserve work against its id.
                    if (!_pool.IsPeerActive(peer.Id)) continue;

                    var reservation = queue.ReserveBodies(peer.Id, DefaultBodyCapacityPerPeer);
                    if (reservation.Count == 0) continue;

                    // Fire-and-forget the dispatch. The task hands the
                    // response back to the queue via DeliverBodies; we
                    // reap it from inFlight after it completes.
                    var peerCopy = peer;
                    var task = DispatchBodyFetchAsync(queue, peerCopy, reservation, ct);
                    inFlight[peer.Id] = task;
                }

                // Block on whichever of these happens first:
                //   - the queue signals new work,
                //   - one of our in-flight dispatches finishes,
                //   - 100ms tick so a peer-pool change is picked up promptly.
                var workWait = queue.BodyWorkAvailable.ReadAsync(ct).AsTask();
                var inFlightAny = inFlight.Count > 0
                    ? Task.WhenAny(inFlight.Values)
                    : Task.Delay(100, ct);

                var completed = await Task.WhenAny(workWait, inFlightAny).ConfigureAwait(false);
                _ = completed;

                // Reap completed dispatches so those peers become eligible
                // for new reservations on the next outer iteration.
                var done = inFlight.Where(kv => kv.Value.IsCompleted).Select(kv => kv.Key).ToList();
                foreach (var id in done) inFlight.Remove(id);
            }

            // Drain in-flight on shutdown so we don't strand outstanding
            // reservations across cancellation.
            try { await Task.WhenAll(inFlight.Values).ConfigureAwait(false); } catch { }
        }

        private async Task DispatchBodyFetchAsync(
            BlockTaskQueue queue, IEthPeer peer, BlockTaskQueue.BodyReservation reservation, CancellationToken ct)
        {
            using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            requestCts.CancelAfter(DefaultRequestTimeout);
            try
            {
                var bodies = await _worker.GetBodiesAsync(peer, reservation.Hashes.ToList(), requestCts.Token).ConfigureAwait(false);
                var result = queue.DeliverBodies(reservation, bodies);
                if (result.Unmatched > 0)
                {
                    _logger.LogDebug(
                        "Body batch x{N} from peer {Peer}: matched {M}, unmatched {U}",
                        reservation.Count, peer.Id.ToString().Substring(0, 8),
                        result.Matched, result.Unmatched);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Per-request TTL fired. The peer accepted the request but
                // didn't reply within the deadline; release its reservation
                // so another peer can take the same blocks.
                _logger.LogDebug(
                    "Body fetch timeout: peer={Peer} x{N} after {Timeout}s",
                    peer.Id.ToString().Substring(0, 8), reservation.Count, DefaultRequestTimeout.TotalSeconds);
                queue.ReleasePeer(peer.Id);
                _metrics?.RecordFetchFailed("phase1-bodies", "Timeout");
            }
            catch (OperationCanceledException) { queue.ReleasePeer(peer.Id); }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    "Body fetch error: peer={Peer} x{N}: {Err}",
                    peer.Id.ToString().Substring(0, 8), reservation.Count, ex.GetType().Name);
                queue.ReleasePeer(peer.Id);
                _metrics?.RecordFetchFailed("phase1-bodies", ex.GetType().Name);
            }
        }

        // ---------------------------------------------------------------
        // Stage 3: Receipt fetcher loop — symmetric per-peer pull
        // ---------------------------------------------------------------

        private async Task RunReceiptFetcherAsync(BlockTaskQueue queue, CancellationToken ct)
        {
            var inFlight = new Dictionary<Guid, Task>();
            while (!ct.IsCancellationRequested)
            {
                foreach (var peer in _pool.ActivePeers)
                {
                    if (ct.IsCancellationRequested) return;
                    if (inFlight.ContainsKey(peer.Id)) continue;
                    // Re-confirm pool membership: ActivePeers is a snapshot
                    // and the peer may have disconnected before we get to
                    // reserve work against its id.
                    if (!_pool.IsPeerActive(peer.Id)) continue;

                    var reservation = queue.ReserveReceipts(peer.Id, DefaultReceiptCapacityPerPeer);
                    if (reservation.Count == 0) continue;

                    var peerCopy = peer;
                    var task = DispatchReceiptFetchAsync(queue, peerCopy, reservation, ct);
                    inFlight[peer.Id] = task;
                }

                var workWait = queue.ReceiptWorkAvailable.ReadAsync(ct).AsTask();
                var inFlightAny = inFlight.Count > 0
                    ? Task.WhenAny(inFlight.Values)
                    : Task.Delay(100, ct);

                var completed = await Task.WhenAny(workWait, inFlightAny).ConfigureAwait(false);
                _ = completed;

                var done = inFlight.Where(kv => kv.Value.IsCompleted).Select(kv => kv.Key).ToList();
                foreach (var id in done) inFlight.Remove(id);
            }

            try { await Task.WhenAll(inFlight.Values).ConfigureAwait(false); } catch { }
        }

        private async Task DispatchReceiptFetchAsync(
            BlockTaskQueue queue, IEthPeer peer, BlockTaskQueue.ReceiptReservation reservation, CancellationToken ct)
        {
            using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            requestCts.CancelAfter(DefaultRequestTimeout);
            try
            {
                var receipts = await _worker.GetReceiptsAsync(peer, reservation.Hashes.ToList(), requestCts.Token).ConfigureAwait(false);
                var result = queue.DeliverReceipts(reservation, receipts);
                if (result.Unmatched > 0)
                {
                    _logger.LogDebug(
                        "Receipt batch x{N} from peer {Peer}: matched {M}, unmatched {U}",
                        reservation.Count, peer.Id.ToString().Substring(0, 8),
                        result.Matched, result.Unmatched);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Per-request TTL fired — see DispatchBodyFetchAsync.
                _logger.LogDebug(
                    "Receipt fetch timeout: peer={Peer} x{N} after {Timeout}s",
                    peer.Id.ToString().Substring(0, 8), reservation.Count, DefaultRequestTimeout.TotalSeconds);
                queue.ReleasePeer(peer.Id);
                _metrics?.RecordFetchFailed("phase1-receipts", "Timeout");
            }
            catch (OperationCanceledException) { queue.ReleasePeer(peer.Id); }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    "Receipt fetch error: peer={Peer} x{N}: {Err}",
                    peer.Id.ToString().Substring(0, 8), reservation.Count, ex.GetType().Name);
                queue.ReleasePeer(peer.Id);
                _metrics?.RecordFetchFailed("phase1-receipts", ex.GetType().Name);
            }
        }

        // ---------------------------------------------------------------
        // Stage 4: Persistence drain — strict cursor order
        // ---------------------------------------------------------------

        private async Task RunPersistenceAsync(
            BlockTaskQueue queue, ulong endBlock, CancellationToken ct)
        {
            var swProgressLog = Stopwatch.StartNew();
            ulong lastLoggedCursor = queue.PersistCursor;

            while (!ct.IsCancellationRequested && queue.PersistCursor <= endBlock)
            {
                var ready = queue.DequeuePersistable(maxCount: 64);
                if (ready.Count == 0)
                {
                    try { await queue.PersistableAvailable.ReadAsync(ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                    continue;
                }

                // Fan out per-block persistence. Each block's saves are
                // independent of the others — we don't gain ordering by
                // serialising them. Letting the storage layer batch its
                // own writes internally while we await Task.WhenAll keeps
                // CPU and storage I/O overlapping with the validation
                // work happening in the worker loops.
                var persistTasks = new List<Task>(ready.Count);
                foreach (var task in ready)
                    persistTasks.Add(PersistBlockAsync(task, ct));
                await Task.WhenAll(persistTasks).ConfigureAwait(false);

                // Cursor advance lands in one atomic bundle batch. Crash
                // mid-loop here previously left some cursor advances
                // applied and the rest dropped, producing a cursor that
                // didn't match the data on disk. One batch means either
                // every block's cursor moves or none does — and the cursor
                // always trails the data because the data is already
                // durable via the prior Task.WhenAll.
                ulong highestPersisted = 0;
                foreach (var task in ready)
                    if (task.BlockNumber > highestPersisted) highestPersisted = task.BlockNumber;

                using (var cursorBatch = _bundle.BeginBatch())
                {
                    // headersFromStore: the skeleton owns LastFetchedHeader; the filler
                    // only advances the body cursor so it never clobbers the skeleton's
                    // descending header progress (they run concurrently).
                    if (_headersFromStore)
                        cursorBatch.SetLastFetchedBody(highestPersisted);
                    else
                        cursorBatch.SetLastFetchedHeaderAndBody(highestPersisted, highestPersisted);
                    await cursorBatch.CommitAsync(ct).ConfigureAwait(false);
                }

                if (swProgressLog.ElapsedMilliseconds > 5000)
                {
                    var cursor = queue.PersistCursor;
                    var dt = swProgressLog.Elapsed.TotalSeconds;
                    var dblocks = cursor - lastLoggedCursor;
                    var rate = dblocks / Math.Max(dt, 0.001);
                    _logger.LogInformation(
                        "Phase 1 backfill (parallel): cursor={Cursor} blocks={Blocks} txs={Txs} receipts={Rcpts} rate={Rate:F1} blk/s pending={Pending}",
                        cursor, _blocksWritten, _txsWritten, _receiptsWritten, rate, queue.Pending);
                    lastLoggedCursor = cursor;
                    swProgressLog.Restart();
                }

                if (queue.PersistCursor > endBlock) return;
                continue;
            }
        }

        private async Task PersistBlockAsync(BlockTaskQueue.BlockTask task, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            var saveBlock = _bundle.Blocks.SaveAsync(task.Header, task.Hash);
            var saveUncles = _bundle.Uncles.SaveAsync(task.Hash, task.Body?.Uncles ?? new List<BlockHeader>());
            await Task.WhenAll(saveBlock, saveUncles).ConfigureAwait(false);

            if (task.Body?.Withdrawals != null)
            {
                await _bundle.Withdrawals
                    .SaveAsync(task.Hash, task.Body.Withdrawals)
                    .ConfigureAwait(false);
            }

            if (task.Body?.Transactions != null)
            {
                BigInteger prevCumulativeGas = 0;
                for (int j = 0; j < task.Body.Transactions.Count; j++)
                {
                    var tx = task.Body.Transactions[j];
                    var blockNumber = task.Header.BlockNumber.ToBigInteger();
                    await _bundle.Transactions.SaveAsync(tx, task.Hash, j, blockNumber)
                        .ConfigureAwait(false);
                    Interlocked.Increment(ref _txsWritten);

                    if (task.Receipts != null && j < task.Receipts.Count)
                    {
                        var rcpt = task.Receipts[j];
                        var cumulative = rcpt.CumulativeGasUsed.ToBigInteger();
                        var gasUsed = cumulative - prevCumulativeGas;
                        if (gasUsed < 0) gasUsed = 0;
                        prevCumulativeGas = cumulative;

                        var txHash = _keccak.CalculateHash(tx.GetRLPEncoded());
                        var baseFee = task.Header.BaseFee ?? EvmUInt256.Zero;
                        var effectiveGasPrice = (BigInteger)tx.GetEffectiveGasPrice(baseFee);

                        string? contractAddress = null;
                        if (tx.IsContractCreation())
                        {
                            var sender = tx.GetSenderAddress();
                            var nonce = (BigInteger)tx.GetNonce();
                            contractAddress = ContractUtils.CalculateContractAddress(sender, nonce);
                        }

                        await _bundle.Receipts.SaveAsync(
                            rcpt, txHash, task.Hash, blockNumber, j,
                            gasUsed, contractAddress, effectiveGasPrice).ConfigureAwait(false);
                        Interlocked.Increment(ref _receiptsWritten);
                    }
                }
            }

            if (_bundle.Logs != null && task.Header.LogsBloom != null && task.Header.LogsBloom.Length == 256)
            {
                await _bundle.Logs.SaveBlockBloomAsync(
                    task.Header.BlockNumber.ToBigInteger(), task.Header.LogsBloom).ConfigureAwait(false);
            }

            Interlocked.Increment(ref _blocksWritten);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private async Task<byte[]?> LoadLastPersistedHashAsync(ulong cursor)
        {
            if (cursor == 0) return null;
            try
            {
                return await _bundle.Blocks.GetHashByNumberAsync(new BigInteger(cursor - 1))
                    .ConfigureAwait(false);
            }
            catch { return null; }
        }

        private static bool HeadersAreContiguous(IList<BlockHeader> headers, ulong startBlock)
        {
            if (headers[0].BlockNumber != (long)startBlock) return false;
            for (int i = 1; i < headers.Count; i++)
            {
                if (headers[i].BlockNumber != headers[i - 1].BlockNumber + 1) return false;
            }
            return true;
        }

        private byte[] HashHeader(BlockHeader header)
        {
            var encoded = BlockHeaderEncoder.Current.Encode(header);
            return _keccak.CalculateHash(encoded);
        }

        private static async Task DelayWithCancel(int ms, CancellationToken ct)
        {
            try { await Task.Delay(ms, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }
}
