using System;
using System.Collections.Concurrent;
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
using Nethereum.Documentation;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.FullSync
{
    public sealed class BackfillStalledException : Exception
    {
        public ulong Cursor { get; }
        public TimeSpan Elapsed { get; }

        public BackfillStalledException(ulong cursor, TimeSpan elapsed)
            : base($"Phase 1 backfill: persist cursor stuck at block {cursor} for {elapsed.TotalSeconds:F0}s " +
                   $"(>= hard stall threshold {ParallelBlockBackfiller.HardStallThreshold.TotalSeconds:F0}s) — no peer is completing this block")
        {
            Cursor = cursor;
            Elapsed = elapsed;
        }
    }

    public sealed class ParallelBlockBackfiller
    {
        public const ulong DefaultHeaderBatchSize = 192;
        public const int DefaultBodyCapacityPerPeer = 128;
        public const int DefaultReceiptCapacityPerPeer = 96;
        public const int MinReceiptCapacityPerPeer = 4;
        public const int InitialReceiptCapacityPerPeer = 32;
        public const int ReceiptCapacityGrowStep = 4;
        private int _receiptCapacity = InitialReceiptCapacityPerPeer;
        public int CurrentReceiptCapacity => Volatile.Read(ref _receiptCapacity);
        public void SetReceiptCapacityForTest(int cap) => Volatile.Write(ref _receiptCapacity, cap);
        public const int HeaderProducerLookaheadBlocks = 4_096;

        public const int HeaderProducerInFlight = 4;

        public const int MaxInFlightRequestsPerPeer = 4;

        private static readonly ParallelOptions PersistParallelOptions =
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 2) };

        public const int PeerFailureDropThreshold = 5;
        public static readonly TimeSpan PeerFailureCooldown = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan PeerTimeoutBenchDuration = TimeSpan.FromSeconds(30);

        public const int TrustedRepeatedBenchDropThreshold = 3;

        public static readonly TimeSpan HardStallThreshold = TimeSpan.FromMinutes(5);

        private readonly PeerFailureTracker _peerFailures =
            new PeerFailureTracker(PeerFailureDropThreshold, PeerFailureCooldown, PeerTimeoutBenchDuration);

        private readonly ConcurrentDictionary<Guid, int> _trustedBenchDropCounts = new();

        private bool IsPeerOnCooldown(Guid peerId) => _peerFailures.IsOnCooldown(peerId);

        private void RecordPeerRequestSuccess(IEthPeer peer)
        {
            _peerFailures.RecordSuccess(peer.Id);
            _trustedBenchDropCounts.TryRemove(peer.Id, out _);
            _pool.ReportSuccess(peer.Id);
        }

        private void RecordPeerRequestFailure(IEthPeer peer, string stage, bool wasTimeout)
        {
            switch (_peerFailures.RecordFailure(peer.Id, wasTimeout))
            {
                case PeerFailureOutcome.Dispose:
                    _logger.LogWarning(
                        "Phase 1 backfill: peer {Peer} hit {N} consecutive {Stage} transport failures",
                        peer.Id.ToString().Substring(0, 8), PeerFailureDropThreshold, stage);
                    DropOnRepeatedBench(peer, stage, wasTimeout: false);
                    break;
                case PeerFailureOutcome.Bench:
                    _logger.LogDebug(
                        "Phase 1 backfill: benching slow peer {Peer} for {Secs}s after {N} consecutive {Stage} timeouts",
                        peer.Id.ToString().Substring(0, 8), (int)PeerTimeoutBenchDuration.TotalSeconds, PeerFailureDropThreshold, stage);
                    DropOnRepeatedBench(peer, stage, wasTimeout: true);
                    break;
            }
        }

        private void DropOnRepeatedBench(IEthPeer peer, string stage, bool wasTimeout)
        {
            if (peer.IsTrusted)
            {
                var count = _trustedBenchDropCounts.AddOrUpdate(peer.Id, 1, (_, prev) => prev + 1);
                if (count < TrustedRepeatedBenchDropThreshold) return;
                _trustedBenchDropCounts.TryRemove(peer.Id, out _);
            }

            _logger.LogWarning(
                "Phase 1 backfill: dropping unresponsive peer {Peer} for redial after repeated {Stage} {Kind}",
                peer.Id.ToString().Substring(0, 8), stage, wasTimeout ? "timeouts" : "transport failures");
            _ = _pool.DropAsync(peer.Id, wasTimeout ? "repeated eth timeout" : "repeated eth transport failure", CancellationToken.None);
        }

        private void RecordReceiptDeliveryOutcome(IEthPeer peer, int matched)
        {
            if (matched > 0) RecordPeerRequestSuccess(peer);
        }

        public void RecordPeerRequestFailureForTest(IEthPeer peer, string stage, bool wasTimeout)
            => RecordPeerRequestFailure(peer, stage, wasTimeout);

        public void RecordPeerRequestSuccessForTest(IEthPeer peer)
            => RecordPeerRequestSuccess(peer);

        public void RecordReceiptDeliveryOutcomeForTest(IEthPeer peer, int matched)
            => RecordReceiptDeliveryOutcome(peer, matched);

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

        private int _bodiesInFlight;
        private int _receiptsInFlight;

        private double _drainWaitMsWindow;
        private double _drainPersistMsWindow;
        private long _drainReadySumWindow;
        private long _drainItersWindow;
        private long _bodyReqDone;
        private long _receiptReqDone;
        private long _bodyUnmatched;
        private long _receiptUnmatched;
        private double _bodyLatMsEma;
        private double _receiptLatMsEma;
        private double _persistMsEma;
        private double _buildMsEma;
        private double _writeMsEma;

        private static double Ema(double prev, double sample) => prev <= 0 ? sample : 0.2 * sample + 0.8 * prev;
        private bool _headersFromStore;

        private readonly string _role;

        private ulong _fillStartCursor;
        private ulong _fillEndBlock;

        private IBodyFillCursor? _bodyFillCursor;

        public ParallelBlockBackfiller(
            IFetchRequestScheduler scheduler,
            IPeerPool pool,
            IPeerRequestWorker worker,
            IChainStoreBundle bundle,
            IBlockRootsProvider? rootsProvider = null,
            ILogger? logger = null,
            SnapSyncMetrics? metrics = null,
            IChainActivations? activations = null,
            string role = "history",
            Func<DateTimeOffset>? utcNow = null)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _worker = worker ?? throw new ArgumentNullException(nameof(worker));
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _backpressure = bundle as Nethereum.CoreChain.Storage.IHistoryWriteBackpressure;
            _pauseControl = bundle as Nethereum.CoreChain.Storage.IBackfillPauseControl;
            _exemptPersister = bundle as Nethereum.CoreChain.Storage.IBackpressureExemptPersister;
            _rootsProvider = rootsProvider ?? PatriciaBlockRootsProvider.Instance;
            _logger = logger ?? NullLogger.Instance;
            _metrics = metrics;
            _activations = activations;
            _role = string.IsNullOrEmpty(role) ? "history" : role;
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        }

        private readonly Func<DateTimeOffset> _utcNow;

        private readonly Nethereum.CoreChain.Storage.IHistoryWriteBackpressure? _backpressure;
        private readonly Nethereum.CoreChain.Storage.IBackfillPauseControl? _pauseControl;
        private readonly Nethereum.CoreChain.Storage.IBackpressureExemptPersister? _exemptPersister;

        private static readonly TimeSpan BackpressurePollInterval = TimeSpan.FromSeconds(10);

        private static readonly TimeSpan DefaultPersistWaitLogInterval = TimeSpan.FromSeconds(30);
        private TimeSpan _persistWaitLogInterval = DefaultPersistWaitLogInterval;
        public void SetPersistWaitLogIntervalForTest(TimeSpan interval) => _persistWaitLogInterval = interval;

        private static readonly TimeSpan PausePollInterval = TimeSpan.FromSeconds(2);

        private bool BackfillPaused => _pauseControl?.ShouldPauseBackfill() ?? false;

        private bool ShouldPausePersist()
        {
            bool debt = _backpressure?.ShouldPauseHistoryWrites() ?? false;
            return debt || BackfillPaused;
        }

        private string DescribePersistPause()
            => BackfillPaused
                ? _pauseControl!.DescribeBackfillPause()
                : _backpressure?.DescribeHistoryBackpressure() ?? "backpressure";

        private async Task<bool> WaitWhilePersistPausedAsync(CancellationToken ct)
        {
            if (!ShouldPausePersist()) return true;

            _logger.LogInformation(
                "Phase 1 backfill: persist paused — {Status}", DescribePersistPause());
            int polls = 0;
            do
            {
                try { await Task.Delay(BackpressurePollInterval, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return false; }
                if (++polls % 6 == 0)
                    _logger.LogInformation(
                        "Phase 1 backfill: persist still paused — {Status}", DescribePersistPause());
            }
            while (!ct.IsCancellationRequested && ShouldPausePersist());
            _logger.LogInformation(
                "Phase 1 backfill: persist resumed — {Status}", DescribePersistPause());
            return true;
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

        [NethereumDocExample(DocSection.DevP2P, "devp2p-sync", "ParallelBlockBackfiller.BackfillAsync — Phase-1 fetch pipeline")]
        public async Task<BackfillResult> BackfillAsync(
            ulong startBlock, ulong endBlock, CancellationToken ct)
            => await BackfillAsync(startBlock, endBlock, headersFromStore: false, ct).ConfigureAwait(false);

        public async Task<BackfillResult> BackfillAsync(
            ulong startBlock, ulong endBlock, IBodyFillCursor cursor, CancellationToken ct)
        {
            _bodyFillCursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            return await BackfillAsync(startBlock, endBlock, headersFromStore: true, ct).ConfigureAwait(false);
        }

        [NethereumDocExample(DocSection.DevP2P, "devp2p-sync", "ParallelBlockBackfiller.BackfillAsync — Phase-1 over a stored header skeleton")]
        public async Task<BackfillResult> BackfillAsync(
            ulong startBlock, ulong endBlock, bool headersFromStore, CancellationToken ct)
        {
            _headersFromStore = headersFromStore;
            if (endBlock < startBlock)
                return new BackfillResult { Ran = false, SkipReason = "endBlock < startBlock" };

            var resume = _bodyFillCursor != null
                ? _bodyFillCursor.Get()
                : headersFromStore
                    ? _bundle.Metadata.GetLastFetchedBody()
                    : _bundle.Metadata.GetLastFetchedHeader();
            ulong cursor;
            if (resume == 0) cursor = startBlock;
            else cursor = resume + 1 > startBlock ? resume + 1 : startBlock;
            if (cursor > endBlock)
                return new BackfillResult { Ran = false, SkipReason = $"already at {resume}" };

            var queue = new BlockTaskQueue(_rootsProvider, cursor, _activations, MaxInFlightRequestsPerPeer);
            queue.DiagnosticLog = msg => _logger.LogWarning("Phase 1 backfill: {Diag}", msg);

            void ReleaseReservationsOnDisconnect(object? sender, IEthPeer removed) => queue.ReleasePeer(removed.Id);

            _fillStartCursor = cursor;
            _fillEndBlock = endBlock;

            _logger.LogInformation(
                "Phase 1 backfill (parallel) role={Role}: starting at block {Start} → {End} ({Total} blocks)",
                _role, cursor, endBlock, endBlock - cursor + 1);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var stagesCt = cts.Token;

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
                _pool.PeerRemoved += ReleaseReservationsOnDisconnect;
                await persistTask.ConfigureAwait(false);
            }
            finally
            {
                _pool.PeerRemoved -= ReleaseReservationsOnDisconnect;
                cts.Cancel();
                await DrainStagesOrAbandonAsync(headerTask, bodyTask, receiptTask).ConfigureAwait(false);
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

        internal static readonly TimeSpan StageDrainTimeout = TimeSpan.FromSeconds(10);
        private TimeSpan _stageDrainTimeout = StageDrainTimeout;
        public void SetStageDrainTimeoutForTest(TimeSpan timeout) => _stageDrainTimeout = timeout;

        internal static readonly TimeSpan StaleReservationTtl = TimeSpan.FromSeconds(90);
        private TimeSpan _staleReservationTtl = StaleReservationTtl;
        internal void SetStaleReservationTtlForTest(TimeSpan ttl) => _staleReservationTtl = ttl;

        public async Task DrainStagesOrAbandonAsync(params Task[] stages)
        {
            var drain = Task.WhenAll(stages);
            using var delayCts = new CancellationTokenSource();
            var timeout = Task.Delay(_stageDrainTimeout, delayCts.Token);
            var settled = await Task.WhenAny(drain, timeout).ConfigureAwait(false);
            if (!ReferenceEquals(settled, drain))
            {
                _logger.LogWarning(
                    "Phase 1 backfill: stages did not drain within {Timeout:F0}s after cancel; abandoning them so the stall surfaces to the retry loop",
                    _stageDrainTimeout.TotalSeconds);
                ObserveAbandonedStages(drain);
                return;
            }
            delayCts.Cancel();
            try { await drain.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Phase 1 backfill: a stage threw during shutdown");
            }
        }

        private void ObserveAbandonedStages(Task drain)
        {
            _ = drain.ContinueWith(
                t =>
                {
                    if (t.Exception != null)
                        _logger.LogWarning(t.Exception, "Phase 1 backfill: an abandoned stage faulted after shutdown");
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public void AdaptReceiptCapacity(int requested, int served)
        {
            if (requested <= 0 || served <= 0) return;
            while (true)
            {
                int current = Volatile.Read(ref _receiptCapacity);
                int next = served < requested
                    ? Math.Max(MinReceiptCapacityPerPeer, served)
                    : Math.Min(DefaultReceiptCapacityPerPeer, current + ReceiptCapacityGrowStep);
                if (next == current) return;
                if (Interlocked.CompareExchange(ref _receiptCapacity, next, current) == current) return;
            }
        }


        private const int MaxEmptyHeaderPolls = 3_000;
        private int _maxEmptyHeaderPolls = MaxEmptyHeaderPolls;
        public void SetMaxEmptyHeaderPollsForTest(int polls) => _maxEmptyHeaderPolls = polls < 1 ? 1 : polls;

        private async Task RunHeaderLoaderAsync(
            BlockTaskQueue queue, ulong startCursor, ulong endBlock, CancellationToken ct)
        {
            ulong next = startCursor;
            int emptyHeaderPolls = 0;
            ulong lastFrontier = ulong.MaxValue;
            while (!ct.IsCancellationRequested && next <= endBlock)
            {
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

                    if (header == null || hash == null) break;

                    queue.EnqueueHeader(header, hash);
                    loaded++;
                }

                if (loaded == 0)
                {
                    var frontier = _bundle.Metadata.GetLastFetchedHeader();
                    bool skeletonDescending = frontier != 0 && frontier < lastFrontier;
                    lastFrontier = frontier;
                    if (skeletonDescending)
                        emptyHeaderPolls = 0;
                    else if (++emptyHeaderPolls >= _maxEmptyHeaderPolls)
                        throw new InvalidOperationException(
                            $"Backfill header loader starved at block {next:N0}: header not in the store after " +
                            $"{_maxEmptyHeaderPolls} polls (~{_maxEmptyHeaderPolls / 10}s) with the skeleton frontier " +
                            $"frozen at {frontier:N0} — nothing is laying it.");
                    try { await Task.Delay(100, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                    continue;
                }
                emptyHeaderPolls = 0;

                next += (ulong)loaded;
            }
        }


        private async Task RunHeaderProducerAsync(
            BlockTaskQueue queue, ulong startCursor, ulong endBlock, CancellationToken ct)
        {
            ulong nextDispatch = startCursor;
            ulong nextEnqueue = startCursor;
            var lastPersistedHash = await LoadLastPersistedHashAsync(startCursor).ConfigureAwait(false);
            var pending = new SortedDictionary<ulong, (List<BlockHeader> Headers, byte[][] Hashes)>();
            var inFlight = new List<Task<(ulong Start, List<BlockHeader>? Headers)>>();

            while (!ct.IsCancellationRequested && (nextEnqueue <= endBlock || inFlight.Count > 0 || pending.Count > 0))
            {
                while (queue.Pending >= HeaderProducerLookaheadBlocks && !ct.IsCancellationRequested)
                {
                    try { await Task.Delay(50, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                }

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
                        await DelayWithCancel(200, ct).ConfigureAwait(false);
                        nextDispatch = Math.Min(nextDispatch, result.Start);
                        continue;
                    }

                    if (!HeadersAreContiguous(result.Headers, result.Start))
                    {
                        _logger.LogWarning(
                            "Phase 1 backfill: non-contiguous header batch at {Block} — retrying",
                            result.Start);
                        nextDispatch = Math.Min(nextDispatch, result.Start);
                        continue;
                    }

                    var hashes = new byte[result.Headers.Count][];
                    for (int i = 0; i < result.Headers.Count; i++)
                        hashes[i] = HashHeader(result.Headers[i]);

                    pending[result.Start] = (result.Headers, hashes);
                }

                while (pending.TryGetValue(nextEnqueue, out var entry))
                {
                    pending.Remove(nextEnqueue);
                    if (!BlockBatchValidator.ValidateParentChain(entry.Headers, entry.Hashes, lastPersistedHash, out var brokenAt))
                    {
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


        private async Task RunBodyFetcherAsync(BlockTaskQueue queue, CancellationToken ct)
        {
            var inFlight = new List<Task>();
            bool wasPaused = false;
            while (!ct.IsCancellationRequested)
            {
                if (BackfillPaused)
                {
                    if (!wasPaused) { _logger.LogInformation("Phase 1 backfill: body fetch idle — {Status}", _pauseControl!.DescribeBackfillPause()); wasPaused = true; }
                    try { await Task.Delay(PausePollInterval, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                    continue;
                }
                if (wasPaused) { _logger.LogInformation("Phase 1 backfill: body fetch resumed"); wasPaused = false; }

                foreach (var peer in _pool.ActivePeersByPreference)
                {
                    if (ct.IsCancellationRequested) return;
                    if (!_pool.IsPeerActive(peer.Id) || IsPeerOnCooldown(peer.Id)) continue;

                    while (!IsPeerOnCooldown(peer.Id))
                    {
                        var reservation = queue.ReserveBodies(peer.Id, DefaultBodyCapacityPerPeer);
                        if (reservation.Count == 0) break;

                        var peerCopy = peer;
                        inFlight.Add(DispatchBodyFetchAsync(queue, peerCopy, reservation, ct));
                    }
                }

                var workWait = queue.BodyWorkAvailable.ReadAsync(ct).AsTask();
                var inFlightAny = inFlight.Count > 0
                    ? Task.WhenAny(inFlight)
                    : Task.Delay(100, ct);

                var completed = await Task.WhenAny(workWait, inFlightAny).ConfigureAwait(false);
                _ = completed;

                inFlight.RemoveAll(t => t.IsCompleted);
            }

            try { await Task.WhenAll(inFlight).ConfigureAwait(false); } catch { }
        }

        private async Task DispatchBodyFetchAsync(
            BlockTaskQueue queue, IEthPeer peer, BlockTaskQueue.BodyReservation reservation, CancellationToken ct)
        {
            using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            requestCts.CancelAfter(DefaultRequestTimeout);
            var sw = Stopwatch.StartNew();
            Interlocked.Increment(ref _bodiesInFlight);
            var delivered = false;
            try
            {
                var bodies = await _worker.GetBodiesAsync(peer, reservation.Hashes.ToList(), requestCts.Token).ConfigureAwait(false);
                _bodyLatMsEma = Ema(_bodyLatMsEma, sw.Elapsed.TotalMilliseconds);
                Interlocked.Increment(ref _bodyReqDone);
                RecordPeerRequestSuccess(peer);
                var result = queue.DeliverBodies(reservation, bodies);
                delivered = true;
                if (result.Unmatched > 0) Interlocked.Add(ref _bodyUnmatched, result.Unmatched);
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
                _logger.LogDebug(
                    "Body fetch timeout: peer={Peer} x{N} after {Timeout}s",
                    peer.Id.ToString().Substring(0, 8), reservation.Count, DefaultRequestTimeout.TotalSeconds);
                if (!delivered) queue.ReleaseBodyReservation(reservation);
                RecordPeerRequestFailure(peer, "body", wasTimeout: true);
                _metrics?.RecordFetchFailed("phase1-bodies", "Timeout");
            }
            catch (OperationCanceledException) { if (!delivered) queue.ReleaseBodyReservation(reservation); }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    "Body fetch error: peer={Peer} x{N}: {Err}",
                    peer.Id.ToString().Substring(0, 8), reservation.Count, ex.GetType().Name);
                if (!delivered) queue.ReleaseBodyReservation(reservation);
                RecordPeerRequestFailure(peer, "body", wasTimeout: false);
                _metrics?.RecordFetchFailed("phase1-bodies", ex.GetType().Name);
            }
            finally { Interlocked.Decrement(ref _bodiesInFlight); }
        }


        private async Task RunReceiptFetcherAsync(BlockTaskQueue queue, CancellationToken ct)
        {
            var inFlight = new List<Task>();
            bool wasPaused = false;
            while (!ct.IsCancellationRequested)
            {
                if (BackfillPaused)
                {
                    if (!wasPaused) { _logger.LogInformation("Phase 1 backfill: receipt fetch idle — {Status}", _pauseControl!.DescribeBackfillPause()); wasPaused = true; }
                    try { await Task.Delay(PausePollInterval, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                    continue;
                }
                if (wasPaused) { _logger.LogInformation("Phase 1 backfill: receipt fetch resumed"); wasPaused = false; }

                foreach (var peer in _pool.ActivePeersByPreference.OrderByDescending(p => p.EthVersion >= 70))
                {
                    if (ct.IsCancellationRequested) return;
                    if (!_pool.IsPeerActive(peer.Id) || IsPeerOnCooldown(peer.Id)) continue;

                    while (!IsPeerOnCooldown(peer.Id))
                    {
                        var reservation = queue.ReserveReceipts(peer.Id, Volatile.Read(ref _receiptCapacity));
                        if (reservation.Count == 0) break;

                        var peerCopy = peer;
                        inFlight.Add(DispatchReceiptFetchAsync(queue, peerCopy, reservation, ct));
                    }
                }

                var workWait = queue.ReceiptWorkAvailable.ReadAsync(ct).AsTask();
                var inFlightAny = inFlight.Count > 0
                    ? Task.WhenAny(inFlight)
                    : Task.Delay(100, ct);

                var completed = await Task.WhenAny(workWait, inFlightAny).ConfigureAwait(false);
                _ = completed;

                inFlight.RemoveAll(t => t.IsCompleted);
            }

            try { await Task.WhenAll(inFlight).ConfigureAwait(false); } catch { }
        }

        private async Task DispatchReceiptFetchAsync(
            BlockTaskQueue queue, IEthPeer peer, BlockTaskQueue.ReceiptReservation reservation, CancellationToken ct)
        {
            using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            requestCts.CancelAfter(DefaultRequestTimeout);
            var sw = Stopwatch.StartNew();
            Interlocked.Increment(ref _receiptsInFlight);
            var delivered = false;
            try
            {
                var receipts = await _worker.GetReceiptsAsync(peer, reservation.Hashes.ToList(), requestCts.Token).ConfigureAwait(false);
                _receiptLatMsEma = Ema(_receiptLatMsEma, sw.Elapsed.TotalMilliseconds);
                Interlocked.Increment(ref _receiptReqDone);
                var result = queue.DeliverReceipts(reservation, receipts);
                delivered = true;
                AdaptReceiptCapacity(reservation.Count, receipts?.Count ?? 0);
                if (result.Unmatched > 0) Interlocked.Add(ref _receiptUnmatched, result.Unmatched);

                RecordReceiptDeliveryOutcome(peer, result.Matched);

                if (result.Unmatched > 0)
                {
                    _logger.LogInformation(
                        "Receipt batch x{N} from peer {Peer} eth/{Ver}: served {S}, matched {M}, unmatched {U}",
                        reservation.Count, peer.Id.ToString().Substring(0, 8), peer.EthVersion,
                        receipts?.Count ?? 0, result.Matched, result.Unmatched);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug(
                    "Receipt fetch timeout: peer={Peer} x{N} after {Timeout}s",
                    peer.Id.ToString().Substring(0, 8), reservation.Count, DefaultRequestTimeout.TotalSeconds);
                if (!delivered) queue.ReleaseReceiptReservation(reservation);
                RecordPeerRequestFailure(peer, "receipt", wasTimeout: true);
                _metrics?.RecordFetchFailed("phase1-receipts", "Timeout");
            }
            catch (OperationCanceledException) { if (!delivered) queue.ReleaseReceiptReservation(reservation); }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    "Receipt fetch error: peer={Peer} x{N}: {Err}",
                    peer.Id.ToString().Substring(0, 8), reservation.Count, ex.GetType().Name);
                if (!delivered) queue.ReleaseReceiptReservation(reservation);
                RecordPeerRequestFailure(peer, "receipt", wasTimeout: false);
                _metrics?.RecordFetchFailed("phase1-receipts", ex.GetType().Name);
            }
            finally { Interlocked.Decrement(ref _receiptsInFlight); }
        }


        private async Task RunPersistenceAsync(
            BlockTaskQueue queue, ulong endBlock, CancellationToken ct)
        {
            var swProgressLog = Stopwatch.StartNew();
            ulong lastLoggedCursor = queue.PersistCursor;
            long lastBodyReqDone = 0, lastReceiptReqDone = 0;
            var stallDetector = new ProgressStallDetector(HardStallThreshold);

            while (!ct.IsCancellationRequested && queue.PersistCursor <= endBlock)
            {
                var ready = queue.DequeuePersistable(maxCount: 256);
                if (ready.Count == 0)
                {
                    var swWait = Stopwatch.StartNew();
                    while (true)
                    {
                        using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        waitCts.CancelAfter(_persistWaitLogInterval);
                        try { await queue.PersistableAvailable.ReadAsync(waitCts.Token).ConfigureAwait(false); break; }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                        catch (OperationCanceledException)
                        {
                            var stallNow = _utcNow();
                            var stuckCursor = queue.PersistCursor;
                            int reclaimed = queue.ReclaimStaleReservations(_staleReservationTtl);
                            if (reclaimed > 0)
                                _logger.LogWarning(
                                    "Phase 1 backfill: reclaimed {N} stale reservations at cursor {Cursor} (orphaned by a dropped/hung peer)",
                                    reclaimed, stuckCursor);
                            if (queue.Pending == 0 || BackfillPaused)
                            {
                                stallDetector = new ProgressStallDetector(HardStallThreshold);
                            }
                            else if (stallDetector.Observe(stuckCursor, stallNow))
                            {
                                throw new BackfillStalledException(stuckCursor, stallDetector.StalledFor(stallNow));
                            }

                            _logger.LogWarning(
                                "Phase 1 backfill: persist drain blocked {Elapsed:F0}s at cursor {Next} — that block's " +
                                "body/receipts are not fetched+matched yet, so nothing is persistable. Not a backpressure pause.",
                                swWait.Elapsed.TotalSeconds, queue.PersistCursor);
                        }
                    }
                    _drainWaitMsWindow += swWait.Elapsed.TotalMilliseconds;
                    continue;
                }

                var swDrain = Stopwatch.StartNew();
                if (_bundle is IBatchedBlockPersister batched)
                {
                    var swBuild = Stopwatch.StartNew();
                    var freezeBoundary = _exemptPersister?.FreezeBoundary ?? BigInteger.MinusOne;
                    var built = new PersistableBlock[ready.Count];
                    Parallel.For(0, ready.Count, PersistParallelOptions, i => built[i] = BuildPersistableBlock(ready[i], freezeBoundary));
                    var pbs = new List<PersistableBlock>(built);
                    swBuild.Stop();

                    var swWrite = Stopwatch.StartNew();

                    var exemptCount = _exemptPersister == null
                        ? 0
                        : await _exemptPersister.PersistBackpressureExemptPrefixAsync(pbs, freezeBoundary, ct).ConfigureAwait(false);

                    if (exemptCount < pbs.Count)
                    {
                        if (!await WaitWhilePersistPausedAsync(ct).ConfigureAwait(false)) return;
                        var pausable = exemptCount == 0 ? pbs : pbs.GetRange(exemptCount, pbs.Count - exemptCount);
                        await batched.PersistBlocksAsync(pausable, ct).ConfigureAwait(false);
                    }
                    swWrite.Stop();

                    var n = Math.Max(ready.Count, 1);
                    _buildMsEma = Ema(_buildMsEma, swBuild.Elapsed.TotalMilliseconds / n);
                    _writeMsEma = Ema(_writeMsEma, swWrite.Elapsed.TotalMilliseconds / n);
                    _persistMsEma = _buildMsEma + _writeMsEma;
                }
                else
                {
                    var swPersist = Stopwatch.StartNew();
                    var persistTasks = new List<Task>(ready.Count);
                    foreach (var task in ready)
                        persistTasks.Add(PersistBlockAsync(task, ct));
                    await Task.WhenAll(persistTasks).ConfigureAwait(false);
                    _persistMsEma = Ema(_persistMsEma, swPersist.Elapsed.TotalMilliseconds / Math.Max(ready.Count, 1));
                }

                ulong highestPersisted = 0;
                foreach (var task in ready)
                    if (task.BlockNumber > highestPersisted) highestPersisted = task.BlockNumber;

                if (_bodyFillCursor != null)
                {
                    await _bodyFillCursor.AdvanceAsync(highestPersisted, ct).ConfigureAwait(false);
                }
                else
                {
                    using (var cursorBatch = _bundle.BeginBatch())
                    {
                        if (_headersFromStore)
                            cursorBatch.SetLastFetchedBody(highestPersisted);
                        else
                            cursorBatch.SetLastFetchedHeaderAndBody(highestPersisted, highestPersisted);
                        await cursorBatch.CommitAsync(ct).ConfigureAwait(false);
                    }
                }

                _drainPersistMsWindow += swDrain.Elapsed.TotalMilliseconds;
                _drainReadySumWindow += ready.Count;
                _drainItersWindow++;

                if (swProgressLog.ElapsedMilliseconds > 5000)
                {
                    var cursor = queue.PersistCursor;
                    var dt = swProgressLog.Elapsed.TotalSeconds;
                    var dblocks = cursor - lastLoggedCursor;
                    var rate = dblocks / Math.Max(dt, 0.001);
                    var fillDone = cursor >= _fillStartCursor ? cursor - _fillStartCursor : 0;
                    var fillTotal = _fillEndBlock >= _fillStartCursor ? _fillEndBlock - _fillStartCursor + 1 : 1;
                    var fillPct = Math.Min(100.0, (double)fillDone / fillTotal * 100.0);
                    _logger.LogInformation(
                        "Phase 1 backfill (parallel) role={Role}: cursor={Cursor}/{Target} ({Pct:F1}%) blocks={Blocks} txs={Txs} receipts={Rcpts} rate={Rate:F1} blk/s pending={Pending}",
                        _role, cursor, _fillEndBlock, fillPct, _blocksWritten, _txsWritten, _receiptsWritten, rate, queue.Pending);

                    _logger.LogInformation(
                        "snap.phase1.drain role={Role} avg_chunk={AvgChunk:F0}/256 drain_iters={Iters} " +
                        "persist_ms_window={PMs:F0} wait_ms_window={WMs:F0} window_ms={Win:F0}",
                        _role, _drainItersWindow > 0 ? (double)_drainReadySumWindow / _drainItersWindow : 0.0,
                        _drainItersWindow, _drainPersistMsWindow, _drainWaitMsWindow, dt * 1000.0);

                    var bodyReqs = _bodyReqDone - lastBodyReqDone;
                    var rcptReqs = _receiptReqDone - lastReceiptReqDone;
                    _logger.LogDebug(
                        "snap.phase1.backfill.diag role={Role} peers={Peers} bodies_inflight={BIF} receipts_inflight={RIF} " +
                        "body_req_ms={BLat:F0} receipt_req_ms={RLat:F0} body_reqs_per_s={BRps:F1} receipt_reqs_per_s={RRps:F1} " +
                        "body_unmatched={BUn} receipt_unmatched={RUn} persist_ms_per_blk={PMs:F1} " +
                        "build_ms_per_blk={BuildMs:F1} write_ms_per_blk={WriteMs:F1}",
                        _role, _pool.ActivePeers.Count, _bodiesInFlight, _receiptsInFlight,
                        _bodyLatMsEma, _receiptLatMsEma, bodyReqs / Math.Max(dt, 0.001), rcptReqs / Math.Max(dt, 0.001),
                        _bodyUnmatched, _receiptUnmatched, _persistMsEma, _buildMsEma, _writeMsEma);

                    lastLoggedCursor = cursor;
                    lastBodyReqDone = _bodyReqDone;
                    lastReceiptReqDone = _receiptReqDone;
                    _drainWaitMsWindow = 0;
                    _drainPersistMsWindow = 0;
                    _drainReadySumWindow = 0;
                    _drainItersWindow = 0;
                    swProgressLog.Restart();
                }

                if (queue.PersistCursor > endBlock) return;
                continue;
            }
        }

        private async Task PersistBlockAsync(BlockTaskQueue.BlockTask task, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            var pb = BuildPersistableBlock(task, BigInteger.MinusOne);
            var blockNumber = task.Header.BlockNumber.ToBigInteger();

            var saveBlock = _bundle.Blocks.SaveAsync(pb.Header, pb.Hash);
            var saveUncles = _bundle.Uncles.SaveAsync(pb.Hash, pb.Uncles);
            await Task.WhenAll(saveBlock, saveUncles).ConfigureAwait(false);

            if (pb.Withdrawals != null)
                await _bundle.Withdrawals.SaveAsync(pb.Hash, pb.Withdrawals).ConfigureAwait(false);

            if (pb.Transactions != null && pb.Transactions.Count > 0)
                await _bundle.Transactions.SaveManyAsync(pb.Hash, blockNumber, pb.Transactions).ConfigureAwait(false);

            if (pb.Receipts != null && pb.Receipts.Count > 0)
                await _bundle.Receipts.SaveManyAsync(pb.Hash, blockNumber, pb.Receipts).ConfigureAwait(false);

            if (_bundle.Logs != null && pb.Logs != null && pb.Logs.Count > 0)
                await _bundle.Logs.SaveManyLogsAsync(pb.Logs, pb.Hash, blockNumber).ConfigureAwait(false);

            if (_bundle.Logs != null && pb.Bloom != null)
                await _bundle.Logs.SaveBlockBloomAsync(blockNumber, pb.Bloom).ConfigureAwait(false);
        }

        public PersistableBlock BuildPersistableBlock(BlockTaskQueue.BlockTask task, BigInteger freezeBoundary)
        {
            var uncles = task.Body?.Uncles ?? new List<BlockHeader>();
            var withdrawals = task.Body?.Withdrawals;
            var txs = task.Body?.Transactions;

            var freezeOnly = task.Header.BlockNumber.ToBigInteger() <= freezeBoundary;

            IReadOnlyList<ReceiptSaveItem> receiptItems = null;
            List<(List<Log> Logs, byte[] TxHash, int TxIndex)> blockLogs = null;

            if (txs != null)
            {
                Interlocked.Add(ref _txsWritten, txs.Count);

                if (task.Receipts != null && task.Receipts.Count > 0)
                {
                    if (freezeOnly)
                        receiptItems = StripReceiptsForFreezer(task.Receipts);
                    else
                        (receiptItems, blockLogs) = BuildReceiptSaveItems(task, txs);
                    Interlocked.Add(ref _receiptsWritten, receiptItems.Count);
                }
            }

            byte[] bloom = null;
            if (!freezeOnly && _bundle.Logs != null && task.Header.LogsBloom != null && task.Header.LogsBloom.Length == 256)
                bloom = task.Header.LogsBloom;

            Interlocked.Increment(ref _blocksWritten);

            return new PersistableBlock(
                task.Header, task.Hash, uncles, withdrawals,
                (IReadOnlyList<ISignedTransaction>)txs,
                receiptItems, blockLogs, bloom);
        }

        private static IReadOnlyList<ReceiptSaveItem> StripReceiptsForFreezer(IReadOnlyList<Receipt> receipts)
        {
            var items = new List<ReceiptSaveItem>(receipts.Count);
            for (var j = 0; j < receipts.Count; j++)
                items.Add(new ReceiptSaveItem(receipts[j], null, j, default, null, default));
            return items;
        }

        private (IReadOnlyList<ReceiptSaveItem> Items, List<(List<Log> Logs, byte[] TxHash, int TxIndex)> Logs)
            BuildReceiptSaveItems(BlockTaskQueue.BlockTask task, List<ISignedTransaction> txs)
        {
            var baseFee = task.Header.BaseFee ?? EvmUInt256.Zero;
            var gasUsedPerTransaction = ReceiptGasAccounting.PerTransactionGasUsed(
                task.Receipts, task.Header.BlockNumber);

            var items = new List<ReceiptSaveItem>(Math.Min(txs.Count, task.Receipts.Count));
            var blockLogs = new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>();

            for (int j = 0; j < txs.Count && j < task.Receipts.Count; j++)
            {
                var tx = txs[j];
                var rcpt = task.Receipts[j];
                var txHash = _keccak.CalculateHash(tx.GetRLPEncoded());

                items.Add(new ReceiptSaveItem(
                    rcpt, txHash, j,
                    gasUsedPerTransaction[j].ToBigInteger(),
                    DeployedContractAddress(tx),
                    (BigInteger)tx.GetEffectiveGasPrice(baseFee)));

                if (_bundle.Logs != null && rcpt.Logs != null && rcpt.Logs.Count > 0)
                    blockLogs.Add((rcpt.Logs, txHash, j));
            }

            return (items, blockLogs);
        }

        private static string DeployedContractAddress(ISignedTransaction tx) =>
            tx.IsContractCreation()
                ? ContractUtils.CalculateContractAddress(tx.GetSenderAddress(), (BigInteger)tx.GetNonce())
                : null;


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
