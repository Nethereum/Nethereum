using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.Documentation;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    public enum SnapResumeMode
    {
        Fresh,
        Phase2,
        Phase3Heal,
        ClearOrphan,
    }

    public static partial class SnapBootstrapper
    {
        public sealed class Result
        {
            public bool Ran { get; init; }
            public string SkipReason { get; init; }
            public ulong PivotBlockNumber { get; init; }
            public byte[] PivotStateRoot { get; init; }
            public int AccountCount { get; init; }
            public int SlotCount { get; init; }
            public int BytecodeCount { get; init; }

            public Task HistoryBackfill { get; init; } = Task.CompletedTask;

            public Task StateCompaction { get; init; } = Task.CompletedTask;
        }

        public sealed record PivotState(BlockHeader Header, byte[] Hash);

        public sealed class RollingPivot
        {
            private PivotState _current;
            public RollingPivot(BlockHeader header, byte[] hash) => _current = new PivotState(header, hash);
            public PivotState Current => System.Threading.Volatile.Read(ref _current);
            public void Adopt(PivotState next)
            {
                while (true)
                {
                    var cur = System.Threading.Volatile.Read(ref _current);
                    if (next.Header.BlockNumber <= cur.Header.BlockNumber) return;
                    if (System.Threading.Interlocked.CompareExchange(ref _current, next, cur) == cur) return;
                }
            }
        }

        private sealed record Phase1Start(
            Task<ParallelBlockBackfiller.BackfillResult>? BackfillTask,
            CancellationTokenSource BackfillCts,
            Task? FirstCompletion,
            Result? BackfillOnlyResult);

        private static SnapSyncClient BuildSnapClient(
            ISnapPeer peer, ISnapSyncSink sink, ILogger logger, SnapSyncMetrics? metrics,
            int rootRefreshIntervalMs, IFetchRequestScheduler? scheduler, IChainStoreBundle bundle, IBulkFlatStateSink? bulkFlat,
            int? accountConcurrency = null, int? largeContractConcurrency = null)
        {
            var client = new SnapSyncClient(peer, sink, logger: logger, metrics: metrics)
            {
                RootRefreshIntervalMs = rootRefreshIntervalMs,
                OnPivotRolled = () => scheduler?.OnTargetRootChanged(),
            };
            if (accountConcurrency.HasValue)
                client.AccountConcurrency = accountConcurrency.Value;
            if (largeContractConcurrency.HasValue)
                client.LargeContractConcurrency = largeContractConcurrency.Value;
            if (bulkFlat != null)
            {
                client.CheckpointBytesThreshold = 256UL * 1024 * 1024;
                client.FlushBulkFlatBeforeCheckpoint = bulkFlat.Flush;
                logger.LogInformation(
                    "snap.flat.sst enabled — flat rows via sorted SST ingestion; checkpoint cadence raised to 256MB");
            }
            if (bundle is IStateWriteBackpressure stateValve)
                client.StateWriteBackpressure =
                    () => stateValve.ShouldPauseStateWrites() ? stateValve.DescribeStateBackpressure() : null;
            return client;
        }

        private static TrieHealer CreateHealer(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, ILogger logger, SnapSyncMetrics? metrics)
        {
            var sink = (bundle as Nethereum.CoreChain.Storage.IHealNodeSinkProvider)?.CreateHealSink()
                ?? new Nethereum.CoreChain.Storage.HashHealNodeSink((INodeBlobStore)bundle.TrieNodes);
            return new TrieHealer(scheduler, sink, bundle.StateTrieNodes, logger, metrics,
                flatWriter: bundle.State as Nethereum.CoreChain.Storage.ISnapFlatStateWriter,
                codeStore: bundle.State);
        }

        public static bool ShouldBalHeal(IChainActivations activations, BlockHeader header, bool balHealEnabled)
            => balHealEnabled
               && activations != null
               && header != null
               && activations.ResolveAt((long)header.BlockNumber, (ulong)header.Timestamp) >= HardforkName.Amsterdam;

        private static Action<SnapSyncClient.SnapSyncCheckpoint> BuildCheckpointSink(
            IChainStoreBundle bundle, Func<PivotState> checkpointPivot)
        {
            return checkpoint =>
            {
                var live = checkpointPivot();
                var withPivot = checkpoint.State with
                {
                    PivotBlockNumber = (ulong)live.Header.BlockNumber,
                    PivotBlockHash = live.Hash,
                };
                using var batch = bundle.BeginBatch();
                foreach (var debt in checkpoint.DeferredStorageDebts)
                {
                    var withPivotBlock = debt.Clone();
                    withPivotBlock.FetchPivotBlock ??= (ulong)live.Header.BlockNumber;
                    batch.UpsertDeferredStorageDebt(withPivotBlock);
                }
                if (checkpoint.DeferredCodeHashes != null && checkpoint.DeferredCodeHashes.Count > 0)
                {
                    var union = new System.Collections.Generic.HashSet<byte[]>(Nethereum.Util.ByteArrayComparer.Current);
                    foreach (var h in DeferredHealCodeCodec.Decode(bundle.Metadata.GetDeferredHealCodeBlob())) union.Add(h);
                    foreach (var h in checkpoint.DeferredCodeHashes) union.Add(h);
                    batch.SaveDeferredHealCodeBlob(
                        DeferredHealCodeCodec.Encode(new System.Collections.Generic.List<byte[]>(union)));
                }
                batch.SaveSnapSyncState(withPivot);
                batch.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
            };
        }

        private static Task HandOffHistoryBackfillToBackground(
            Task<ParallelBlockBackfiller.BackfillResult>? backfillTask,
            CancellationTokenSource backfillCts,
            IChainStoreBundle bundle,
            IFetchRequestScheduler? scheduler,
            IPeerPool? pool,
            IChainActivations? activations,
            bool externalHeaderFollow,
            RollingPivot rollingPivot,
            ILogger logger,
            CancellationToken ct)
        {
            if (backfillTask == null)
            {
                backfillCts.Dispose();
                return Task.CompletedTask;
            }

            logger.LogInformation(
                "snap.history.backfill continuing in background (cursor={Cursor}, target~pivot) — finalize and forward execution do NOT wait on it",
                bundle.Metadata.GetLastFetchedBody());
            return Task.Run(async () =>
            {
                try
                {
                    var bf = await backfillTask.ConfigureAwait(false);
                    if (backfillCts.IsCancellationRequested) return;
                    if (bf.Ran)
                    {
                        logger.LogInformation(
                            "Phase 1 backfill complete: {Blocks} blocks, {Txs} txs, {Rcpts} receipts persisted up to block {End}.",
                            bf.BlocksWritten, bf.TransactionsWritten, bf.ReceiptsWritten, bf.EndBlock);
                    }

                    if (externalHeaderFollow && scheduler != null && pool != null)
                    {
                        var settledPivot = (ulong)rollingPivot.Current.Header.BlockNumber;
                        if (bundle.Metadata.GetLastFetchedBody() < settledPivot)
                        {
                            logger.LogInformation(
                                "snap.phase1.settled_catchup filling bodies to the settled pivot {Pivot} (cursor={Cursor})",
                                settledPivot, bundle.Metadata.GetLastFetchedBody());
                            await RunPhase1BackfillWithStallRetryAsync(
                                attemptCt =>
                                {
                                    var catchup = new ParallelBlockBackfiller(
                                        scheduler, pool, new PeerRequestWorker(), bundle,
                                        rootsProvider: null, logger: logger, activations: activations, role: "history");
                                    return catchup.BackfillAsync(0, settledPivot, headersFromStore: true, attemptCt);
                                },
                                logger, backfillCts.Token).ConfigureAwait(false);
                        }
                    }
                    if (backfillCts.IsCancellationRequested) return;

                    (bundle as IBulkDurabilityBoundary)?.CheckpointBulk();
                    (bundle as IBulkDurabilityBoundary)?.FinishBulkIndexing(ct);
                    logger.LogInformation("snap.history.backfill drained — pre-pivot archive complete");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Phase 1 backfill failed; node has verified state at the pivot but a partial pre-pivot block archive (resumes from its cursor on restart).");
                }
            }, CancellationToken.None);
        }

        private static Task StartStateCompactionInBackground(IChainStoreBundle bundle, ILogger logger, CancellationToken ct)
        {
            if (bundle is not IStateCompaction stateCompaction) return Task.CompletedTask;

            Func<Task> compactionRun = async () =>
            {
                try
                {
                    logger.LogInformation("snap.state.compact starting in background — collapsing state-CF compaction debt behind Phase 4");
                    var compactSw = System.Diagnostics.Stopwatch.StartNew();
                    await stateCompaction.CompactStateAsync(
                            msg => logger.LogInformation("snap.state.compact {Progress}", msg), ct)
                        .ConfigureAwait(false);
                    logger.LogInformation("snap.state.compact done in {Elapsed}", compactSw.Elapsed);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "snap.state.compact background pass failed; store serves uncompacted until the next maintenance pass");
                }
            };
            return Task.Run(compactionRun, CancellationToken.None);
        }

        private static async Task<Phase1Start> StartPhase1Async(
            IChainStoreBundle bundle, SnapRunOptions options, RollingPivot rollingPivot, ILogger logger, CancellationToken ct)
        {
            var scheduler = options.Scheduler;
            var pool = options.Pool;
            var backfillCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Task<ParallelBlockBackfiller.BackfillResult>? backfillTask = null;
            if (options.RunBackfill && scheduler != null && pool != null)
            {
                var bfCt = backfillCts.Token;
                backfillTask = Task.Run(() => RunPhase1BackfillWithStallRetryAsync(
                    attemptCt => RunPhase1BackfillAsync(
                        scheduler, pool, bundle, logger, options.Activations,
                        options.UseBackwardSkeleton, options.ExternalHeaderFollow, options.HeaderSweepOverride,
                        options.PivotRefresher, rollingPivot, attemptCt),
                    logger, bfCt), bfCt);
            }

            if (options.BackfillOnly)
            {
                var bfPivot = (ulong)rollingPivot.Current.Header.BlockNumber;
                logger.LogInformation(
                    "snap.bootstrap.backfill_only running Phase 1 archive to pivot {Pivot}; state sync + heal skipped", bfPivot);
                if (backfillTask != null)
                {
                    try { await backfillTask.ConfigureAwait(false); }
                    catch (OperationCanceledException) { }
                }
                backfillCts.Dispose();
                return new Phase1Start(backfillTask, backfillCts, null,
                    new Result { Ran = true, PivotBlockNumber = bfPivot, SkipReason = "backfill-only (Phase 1)" });
            }

            Task phase1FirstCompletion = null;
            if (options.Phase1First && backfillTask != null)
            {
                logger.LogInformation("snap.bootstrap.phase1_first completing + compacting Phase 1 archive before Phase 2 state sync");
                phase1FirstCompletion = HandOffHistoryBackfillToBackground(
                    backfillTask, backfillCts, bundle, scheduler, pool, options.Activations,
                    options.ExternalHeaderFollow, rollingPivot, logger, ct);
                try { await phase1FirstCompletion.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }

            return new Phase1Start(backfillTask, backfillCts, phase1FirstCompletion, null);
        }

        public static readonly TimeSpan BackfillStopTimeout = ParallelBlockBackfiller.StageDrainTimeout + TimeSpan.FromSeconds(5);

        private static async Task AbandonPhase2AndStopBackfillAsync(IBulkFlatStateSink? bulkFlat, Phase1Start phase1, ILogger logger)
        {
            if (bulkFlat != null && bulkFlat.HasBufferedRows)
                logger.LogWarning(
                    "snap.flat.sst.abandon_with_unflushed_rows this attempt's buffered flat rows are being " +
                    "discarded; the durable resume watermark (SnapTaskSet.DurableNext) was not advanced past " +
                    "the last confirmed flush, so the affected range is safely re-fetched by the next attempt.");
            bulkFlat?.Abandon();
            await StopBackfillAsync(phase1.BackfillCts, phase1.BackfillTask, logger).ConfigureAwait(false);
            phase1.BackfillCts.Dispose();
        }

        public static async Task StopBackfillAsync(CancellationTokenSource backfillCts, Task backfill, ILogger logger)
        {
            try { backfillCts.Cancel(); }
            catch (ObjectDisposedException) { }
            if (backfill == null) return;
            if (await Task.WhenAny(backfill, Task.Delay(BackfillStopTimeout)).ConfigureAwait(false) != backfill)
            {
                logger.LogWarning(
                    "snap.phase1.backfill.stop_timeout timeout_sec={TimeoutSec} — the Phase-1 backfill did not observe cancellation in time; returning without it",
                    (int)BackfillStopTimeout.TotalSeconds);
                return;
            }
            if (backfill.IsFaulted && backfill.Exception.GetBaseException() is not OperationCanceledException)
                logger.LogWarning(backfill.Exception.GetBaseException(), "snap.phase1.backfill.stopped_faulted");
        }

        private static async Task<Result> FinishWithHistoryHandOffAsync(
            IChainStoreBundle bundle, Phase1Start phase1,
            IFetchRequestScheduler? scheduler, IPeerPool? pool, IChainActivations? activations,
            bool externalHeaderFollow, RollingPivot rollingPivot,
            Func<Task<BlockHeader>> finalizeCore,
            Func<(int Accounts, int Slots, int Bytecodes)> counts,
            ILogger logger, CancellationToken ct)
        {
            Task historyBackfill = phase1.FirstCompletion ?? HandOffHistoryBackfillToBackground(
                phase1.BackfillTask, phase1.BackfillCts, bundle, scheduler, pool, activations, externalHeaderFollow,
                rollingPivot, logger, ct);

            BlockHeader finalPivotHeader;
            try
            {
                finalPivotHeader = await finalizeCore().ConfigureAwait(false);
            }
            catch (Exception)
            {
                await StopBackfillAsync(phase1.BackfillCts, historyBackfill, logger).ConfigureAwait(false);
                throw;
            }

            Task stateCompactionTask = StartStateCompactionInBackground(bundle, logger, ct);

            var (accounts, slots, bytecodes) = counts();
            return new Result
            {
                Ran = true,
                PivotBlockNumber = (ulong)finalPivotHeader.BlockNumber,
                PivotStateRoot = finalPivotHeader.StateRoot,
                AccountCount = accounts,
                SlotCount = slots,
                BytecodeCount = bytecodes,
                HistoryBackfill = historyBackfill,
                StateCompaction = stateCompactionTask,
            };
        }

        [NethereumDocExample(DocSection.DevP2P, "devp2p-sync", "SnapBootstrapper.RunAsync — single-pivot cold-start")]
        public static async Task<Result> RunAsync(
            IChainStoreBundle bundle,
            ISnapPeer peer,
            BlockHeader pivot,
            byte[] pivotHash,
            ILogger logger,
            SnapRunOptions? options = null,
            CancellationToken ct = default)
        {
            options ??= new SnapRunOptions();
            var scheduler = options.Scheduler;
            var pivotRefresher = options.PivotRefresher;
            var activations = options.Activations;
            var pool = options.Pool;
            var metrics = options.Metrics;
            var rootRefreshIntervalMs = options.RootRefreshIntervalMs;
            var finalizeVerify = options.FinalizeVerify;
            var enableFlatReconcile = options.EnableFlatReconcile;
            var backfillOnly = options.BackfillOnly;
            var accountConcurrency = options.AccountConcurrency;
            var largeContractConcurrency = options.LargeContractConcurrency;
            var externalHeaderFollow = options.ExternalHeaderFollow;
            var balHealEnabled = options.BalHealEnabled;
            if (bundle is null) throw new ArgumentNullException(nameof(bundle));
            if (peer is null) throw new ArgumentNullException(nameof(peer));
            if (pivot is null) throw new ArgumentNullException(nameof(pivot));
            if (pivotHash is null || pivotHash.Length != 32)
                throw new ArgumentException("pivotHash must be 32 bytes", nameof(pivotHash));
            if (pivot.StateRoot is null || pivot.StateRoot.Length != 32)
                throw new ArgumentException("pivot.StateRoot must be 32 bytes", nameof(pivot));
            logger ??= Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

            var existing = bundle.Metadata.GetLastBlock();
            if (existing > 0)
            {
                logger.LogInformation(
                    "snap.bootstrap.skip reason=committed_state block={Block}",
                    existing);
                return new Result { Ran = false, SkipReason = $"existing state at block {existing}" };
            }

            var rollingPivot = options.RollingPivot ?? new RollingPivot(pivot, pivotHash);

            if (!backfillOnly && ShouldBalHeal(activations, pivot, balHealEnabled))
                return await RunSnap2Async(bundle, peer, pivot, pivotHash, rollingPivot, options, logger, ct).ConfigureAwait(false);

            var (resumeFrom, skipPhase2) = RouteResume(bundle, pivot, pivotHash, logger);

            logger.LogInformation(
                "Snap-bootstrap: starting fetch at pivot block={Block} hash=0x{Hash} stateRoot=0x{Root}",
                pivot.BlockNumber, pivotHash.ToHex(), pivot.StateRoot.ToHex());

            using var bulkFlat = (bundle as IBulkFlatStateSinkProvider)?.CreateBulkFlatSink();
            var sink = bulkFlat != null
                ? new TrieSnapSyncSink(bundle.StateTrieNodes, bundle.State, bulkFlat)
                : new TrieSnapSyncSink(bundle.StateTrieNodes, bundle.State);
            var client = BuildSnapClient(peer, sink, logger, metrics, rootRefreshIntervalMs, scheduler, bundle, bulkFlat, accountConcurrency, largeContractConcurrency);


            var checkpointSink = BuildCheckpointSink(bundle, () => rollingPivot.Current);

            StampPhase2Entry(bundle, pivot, pivotHash, resumeFrom, skipPhase2, backfillOnly, metrics, logger);

            var phase1 = await StartPhase1Async(bundle, options, rollingPivot, logger, ct).ConfigureAwait(false);
            if (phase1.BackfillOnlyResult != null)
                return phase1.BackfillOnlyResult;

            SnapSyncClient.SyncResult syncResult;
            bool healPhaseEntered;
            try
            {
                (syncResult, healPhaseEntered) = await RunPhase2OrHealAsync(
                        client, pivot, resumeFrom, checkpointSink, skipPhase2, sink,
                        rollingPivot, pivotRefresher, scheduler, bundle, metrics, logger, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                await AbandonPhase2AndStopBackfillAsync(bulkFlat, phase1, logger).ConfigureAwait(false);
                throw;
            }

            logger.LogInformation(
                "Snap-bootstrap: state populated — {Accounts} accounts, {Slots} storage slots, {Codes} bytecodes (computed root matches pivot: {Match}).",
                sink.AccountCount, sink.SlotCount, sink.BytecodeCount, syncResult.RootMatchesTarget);

            return await FinishWithHistoryHandOffAsync(
                bundle, phase1, scheduler, pool, activations, externalHeaderFollow, rollingPivot,
                async () =>
                {
                    bulkFlat?.Flush();
                    (bundle as IBulkDurabilityBoundary)?.CheckpointBulk();

                    var finalPivot = await ReconcileFlatStateAsync(bundle, scheduler, rollingPivot, pivotRefresher, metrics, finalizeVerify, enableFlatReconcile, logger, ct).ConfigureAwait(false);
                    var finalPivotHash = finalPivot.Hash;
                    var pivotBlockNumber = (ulong)finalPivot.Header.BlockNumber;

                    await FetchMissingBytecodeAsync(bundle, scheduler, resumeFrom, finalPivot.Header.StateRoot, logger, ct,
                        syncResult?.CodeHashesNeedingHeal).ConfigureAwait(false);

                    return await PersistPivotAndCheckpointAsync(
                        bundle, finalPivotHash, pivotBlockNumber, phase1.BackfillTask != null, healPhaseEntered, logger, ct)
                        .ConfigureAwait(false);
                },
                () => (sink.AccountCount, sink.SlotCount, sink.BytecodeCount),
                logger, ct).ConfigureAwait(false);
        }
    }
}
