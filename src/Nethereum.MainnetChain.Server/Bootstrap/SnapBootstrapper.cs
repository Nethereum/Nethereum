using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.RocksDB.Snap;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.MainnetChain.Server.Bootstrap
{
    /// <summary>
    /// Orchestrates the snap-first cold-start sequence:
    /// 1. Skip if the bundle already has committed state (<see cref="IChainMetadataStore.GetLastBlock"/> &gt; 0).
    /// 2. Run <see cref="SnapSyncClient.SyncStateAsync"/> against the provided
    ///    snap peer, streaming account + storage + bytecode into
    ///    <see cref="RocksDbSnapSyncSink"/> (which writes Patricia trie nodes
    ///    + bytecode CF entries inside the bundle).
    /// 3. The client throws on root mismatch — propagated to the caller.
    /// 4. On success, persist the pivot header into <see cref="IChainStoreBundle.Blocks"/>
    ///    and commit the metadata cursor at the pivot so the follower picks up
    ///    at pivot+1.
    ///
    /// <para>
    /// Cold reads against the snap-bootstrapped state must go through a
    /// <see cref="Nethereum.CoreChain.State.TrieFallbackStateStore"/> decorator
    /// over the inner <c>IStateStore</c>; this bootstrapper only writes trie
    /// nodes + bytecode, never flat account/storage rows.
    /// </para>
    /// </summary>
    public static class SnapBootstrapper
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
        }

        /// <summary>
        /// Atomic snapshot of the live pivot. The refresher callback and the
        /// concurrent backfill task race to read/write the pivot identity;
        /// pairing <see cref="BlockHeader"/> and the 32-byte block hash into a
        /// single immutable reference lets <see cref="Interlocked.Exchange{T}"/>
        /// swap both fields together without a lock, so a concurrent reader
        /// can never observe a torn header/hash pair.
        /// </summary>
        private sealed record PivotState(BlockHeader Header, byte[] Hash);

        public static Task<Result> RunAsync(
            IChainStoreBundle bundle,
            ISnapPeer peer,
            BlockHeader pivot,
            byte[] pivotHash,
            ILogger logger,
            CancellationToken ct = default)
            => RunAsync(bundle, peer, pivot, pivotHash, logger, scheduler: null, pivotRefresher: null, ct);

        /// <summary>
        /// Overload that accepts a <see cref="IFetchRequestScheduler"/> so a
        /// root mismatch at the end of the leaf-stream phase triggers the
        /// heal phase via <see cref="TrieHealer"/>. Optionally accepts a
        /// pivot-refresh callback so the live target root tracks the
        /// caller's chain head — the peer snapshot window is bounded,
        /// so a fresh pivot is needed during long syncs.
        /// </summary>
        public static Task<Result> RunAsync(
            IChainStoreBundle bundle,
            ISnapPeer peer,
            BlockHeader pivot,
            byte[] pivotHash,
            ILogger logger,
            IFetchRequestScheduler? scheduler,
            Func<CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            CancellationToken ct = default)
            => RunAsync(bundle, peer, pivot, pivotHash, logger, scheduler, pivotRefresher, runBackfill: true, activations: null, pool: null, ct: ct);

        public static Task<Result> RunAsync(
            IChainStoreBundle bundle,
            ISnapPeer peer,
            BlockHeader pivot,
            byte[] pivotHash,
            ILogger logger,
            IFetchRequestScheduler? scheduler,
            Func<CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            IChainActivations? activations,
            CancellationToken ct = default)
            => RunAsync(bundle, peer, pivot, pivotHash, logger, scheduler, pivotRefresher, runBackfill: true, activations: activations, pool: null, ct: ct);

        public static Task<Result> RunAsync(
            IChainStoreBundle bundle,
            ISnapPeer peer,
            BlockHeader pivot,
            byte[] pivotHash,
            ILogger logger,
            IFetchRequestScheduler? scheduler,
            Func<CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            IChainActivations? activations,
            IPeerPool? pool,
            CancellationToken ct = default)
            => RunAsync(bundle, peer, pivot, pivotHash, logger, scheduler, pivotRefresher, runBackfill: true, activations: activations, pool: pool, ct: ct);

        /// <summary>
        /// Overload that wires Phase 1 historical block + receipts backfill.
        /// When <paramref name="runBackfill"/> is true and a scheduler is
        /// available, <see cref="HistoricalBlockBackfiller"/> runs
        /// concurrently with the snap state stream, populating the bundle's
        /// header/body/receipt stores for [genesis..pivot] so the
        /// snap-bootstrapped node can answer <c>eth_getBlockByNumber</c> /
        /// <c>eth_getTransactionReceipt</c> for any block under the pivot.
        /// On pivot rotation the backfill's end-block extends to the new
        /// pivot.
        /// </summary>
        public static async Task<Result> RunAsync(
            IChainStoreBundle bundle,
            ISnapPeer peer,
            BlockHeader pivot,
            byte[] pivotHash,
            ILogger logger,
            IFetchRequestScheduler? scheduler,
            Func<CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? pivotRefresher,
            bool runBackfill,
            IChainActivations? activations = null,
            IPeerPool? pool = null,
            SnapSyncMetrics? metrics = null,
            bool useBackwardSkeleton = false,
            CancellationToken ct = default)
        {
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

            // Live pivot, mutated by the refresh callback. SnapBootstrapper
            // tracks this so heal phase + metadata commit use the most
            // recent pivot the sync ended on, not the original one. The
            // refresher callback (snap state stream) and the concurrent
            // backfill task both touch it, so the (header, hash) pair is
            // wrapped in an immutable PivotState swapped atomically via
            // Interlocked.Exchange — readers use Volatile.Read for the
            // matching acquire fence.
            var pivotState = new PivotState(pivot, pivotHash);

            // Resume decision — when a prior bootstrap left SnapSyncState
            // pinned to the same pivot, pick the path matching the saved
            // Phase. Unknown SchemaVersion → fresh start (treated as
            // NotStarted). Different pivot → stale-pivot reset (preserves
            // Phase 1 archive but wipes the partial state trie that no
            // longer matches). Phase=Complete with LastBlock=0 is an
            // orphan record (atomic-commit failure) — clear and restart.
            var savedState = bundle.Metadata.GetSnapSyncState();
            SnapSyncState resumeFrom = null;
            bool skipPhase2 = false;
            if (savedState != null && savedState.SchemaVersion == SnapSyncStateRlpEncoder.CurrentSchemaVersion)
            {
                if (savedState.PivotBlockNumber != (ulong)pivot.BlockNumber)
                {
                    // Pivot move: RESUME the account-range cursors against the NEW
                    // pivot root — the
                    // hash-space cursors are root-independent, most accounts are
                    // unchanged across ~128 blocks, and the heal phase reconciles the
                    // diffs to the new root. Do NOT wipe: wiping would re-sync the whole
                    // ~250M-account state from scratch on every rotation and never
                    // converge.
                    logger.LogInformation(
                        "snap.bootstrap.pivot_moved saved={SavedPivot} new={NewPivot} — resuming delta against new root",
                        savedState.PivotBlockNumber, pivot.BlockNumber);
                    resumeFrom = savedState;
                }
                else if (savedState.Phase == SnapPhase.Complete)
                {
                    logger.LogWarning(
                        "snap.bootstrap.state phase=Complete_orphan pivot={Pivot} — clearing and restarting",
                        savedState.PivotBlockNumber);
                    bundle.Metadata.ClearSnapSyncState();
                }
                else if (savedState.Phase == SnapPhase.Phase3Running
                    && savedState.HealTargetRoot != null && savedState.HealTargetRoot.Length == 32)
                {
                    logger.LogInformation(
                        "snap.bootstrap.resume phase=Phase3 pivot={Pivot} heal_target=0x{Root}",
                        savedState.PivotBlockNumber, savedState.HealTargetRoot.ToHex());
                    resumeFrom = savedState;
                    skipPhase2 = true;
                }
                else if (savedState.Phase == SnapPhase.Phase2Running)
                {
                    logger.LogInformation(
                        "snap.bootstrap.resume phase=Phase2 pivot={Pivot} accounts_synced={Acc} bytes_synced={Bytes}",
                        savedState.PivotBlockNumber,
                        savedState.Counters?.AccountsSynced ?? 0,
                        savedState.Counters?.AccountBytes ?? 0);
                    resumeFrom = savedState;
                }
            }
            else if (savedState != null)
            {
                logger.LogWarning(
                    "snap.bootstrap.state schema_mismatch saved_version={Saved} expected={Expected} — treating as fresh",
                    savedState.SchemaVersion, SnapSyncStateRlpEncoder.CurrentSchemaVersion);
                bundle.Metadata.ClearSnapSyncState();
            }

            logger.LogInformation(
                "Snap-bootstrap: starting fetch at pivot block={Block} hash=0x{Hash} stateRoot=0x{Root}",
                pivot.BlockNumber, pivotHash.ToHex(), pivot.StateRoot.ToHex());

            var sink = new RocksDbSnapSyncSink(bundle.TrieNodes, bundle.State);
            var client = new SnapSyncClient(peer, sink, logger: logger, metrics: metrics);

            // No mid-cycle pivot refresh on the client: SyncStateAsync runs a FIXED
            // root. The staleness monitor below cancels it so the sync aborts and the
            // invoker restarts with a fresh pivot when the head moves ~2x the
            // pivot-trail distance ahead.

            // Per-chunk checkpoint sink wired to the bundle batch so resume
            // after a kill picks up at the persisted cursor, batched at the
            // 8 MB threshold. The local copy
            // pattern avoids capturing pivotState by reference outside the
            // initial atomic read.
            Action<SnapSyncState> checkpointSink = state =>
            {
                var live = Volatile.Read(ref pivotState);
                var withPivot = state with
                {
                    PivotBlockNumber = (ulong)live.Header.BlockNumber,
                    PivotBlockHash = live.Hash,
                };
                bundle.Metadata.SaveSnapSyncState(withPivot);
            };

            // Mark Phase 2 entry before the snap stream starts so a kill
            // between this row and the first per-chunk checkpoint still
            // recovers to a known phase. Pivot, schema version, and zero
            // counters seed the row; subsequent checkpoints overwrite it
            // with the running task cursor + counter totals.
            if (!skipPhase2)
            {
                var fromPhase = resumeFrom?.Phase ?? SnapPhase.NotStarted;
                logger.LogInformation(
                    "snap.phase.transition from={From} to={To} pivot={Pivot}",
                    fromPhase, SnapPhase.Phase2Running, pivot.BlockNumber);
                bundle.Metadata.SaveSnapSyncState(new SnapSyncState
                {
                    SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                    Phase = SnapPhase.Phase2Running,
                    PivotBlockNumber = (ulong)pivot.BlockNumber,
                    PivotBlockHash = pivotHash,
                    HealTargetRoot = new byte[32],
                    Tasks = resumeFrom?.Tasks ?? System.Array.Empty<SnapSyncAccountTask>(),
                    Counters = resumeFrom?.Counters ?? SnapSyncCounters.Zero,
                });
                if (resumeFrom != null)
                {
                    metrics?.RecordResume(fromPhase);
                }
            }
            else if (resumeFrom != null)
            {
                metrics?.RecordResume(resumeFrom.Phase);
            }

            // Phase 1: historical block + receipts backfill. Runs
            // concurrently with the snap state stream so the
            // snap-bootstrapped node has the full block + receipt
            // archive at completion, not just state at the pivot. The
            // backfill's target end-block tracks whatever livePivot is at
            // the moment snap finishes, which automatically extends the
            // range when the pivot rotates partway through.
            // Dedicated cancellation for the Phase-1 backfiller, linked to ct. An aborted attempt (e.g.
            // a stale-pivot cancel that restarts the bootstrap) cancels + drains THIS backfiller without
            // tearing down the shared bootstrap token, so a retry never starts a second overlapping one.
            using var backfillCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Task<ParallelBlockBackfiller.BackfillResult>? backfillTask = null;
            if (runBackfill && scheduler != null && pool != null)
            {
                var worker = new MainnetPeerRequestWorker();
                var bfCt = backfillCts.Token;
                backfillTask = Task.Run(async () =>
                {
                    var backfiller = new ParallelBlockBackfiller(
                        scheduler, pool, worker, bundle,
                        rootsProvider: null, logger: logger,
                        activations: activations);

                    if (useBackwardSkeleton)
                    {
                        // Skeleton/filler split: a headers-only skeleton lays headers
                        // BACKWARD from the pivot (parent-hash validated) while a
                        // concurrent filler fills bodies+receipts FORWARD over them.
                        // The filler tracks the body cursor; the skeleton the header
                        // cursor (see ParallelBlockBackfiller headersFromStore).
                        var live = Volatile.Read(ref pivotState);
                        var target = (ulong)live.Header.BlockNumber;
                        var anchorHash = live.Hash;

                        Func<ulong, CancellationToken, Task<(byte[] hash, bool exists)>> lookupLocal =
                            async (n, c) =>
                            {
                                var h = await bundle.Blocks.GetHashByNumberAsync(new BigInteger(n)).ConfigureAwait(false);
                                return (h, h != null);
                            };

                        var fill = backfiller.BackfillAsync(0, target, headersFromStore: true, bfCt);

                        var skeleton = Task.Run(async () =>
                        {
                            var walker = new BackwardBlockWalker(
                                scheduler, bundle,
                                new BackwardBlockWalkerOptions { HeadersOnly = true },
                                NullLogger<BackwardBlockWalker>.Instance);

                            // Phase 1a — finish the tip->0 sweep. On resume, continue
                            // DOWNWARD from the persisted cursor (lowest laid header)
                            // rather than re-walking from the new pivot, which would
                            // exit immediately at the already-laid region.
                            var sweepFrom = target;
                            var sweepFromHash = anchorHash;
                            var cursor = bundle.Metadata.GetLastFetchedHeader();
                            if (cursor > 0 && cursor < target)
                            {
                                var ch = await bundle.Blocks.GetHashByNumberAsync(new BigInteger(cursor)).ConfigureAwait(false);
                                if (ch != null) { sweepFrom = cursor; sweepFromHash = ch; }
                            }
                            // Record the segment headed at the pivot so progress is identifiable as a
                            // [Tail..Head] range (resumable + drives eth_syncing); the walker lays the headers.
                            bundle.Metadata.SaveHeaderSyncState(
                                HeaderSubchains.OpenTip(bundle.Metadata.GetHeaderSyncState(), target));
                            var sweep = await walker.WalkAsync(sweepFrom, sweepFromHash, 0, lookupLocal, bfCt).ConfigureAwait(false);
                            bundle.Metadata.SaveHeaderSyncState(
                                HeaderSubchains.RecordDescent(bundle.Metadata.GetHeaderSyncState(), target, sweep.SkeletonBottomBlock));

                            // Phase 1b — new-tip -> old-tip catchup. The chain advanced
                            // during the (long) sweep; lay the header gap above the
                            // original sweep top up to the current pivot. Exits on
                            // MetExistingStore at the old top.
                            if (pivotRefresher != null)
                            {
                                var fresh = await pivotRefresher(bfCt).ConfigureAwait(false);
                                if (fresh.HasValue && (ulong)fresh.Value.Header.BlockNumber > target)
                                {
                                    var newTip = (ulong)fresh.Value.Header.BlockNumber;
                                    bundle.Metadata.SaveHeaderSyncState(
                                        HeaderSubchains.OpenTip(bundle.Metadata.GetHeaderSyncState(), newTip));
                                    var catchup = await walker.WalkAsync(
                                        newTip, fresh.Value.Hash, target, lookupLocal, bfCt).ConfigureAwait(false);
                                    // Linking the new segment onto the old top merges them into one [Tail..newTip].
                                    bundle.Metadata.SaveHeaderSyncState(
                                        HeaderSubchains.RecordDescent(bundle.Metadata.GetHeaderSyncState(), newTip, catchup.SkeletonBottomBlock));
                                }
                            }
                        }, bfCt);

                        await Task.WhenAll(skeleton, fill).ConfigureAwait(false);
                        return await fill.ConfigureAwait(false);
                    }

                    ParallelBlockBackfiller.BackfillResult last = null!;
                    while (!bfCt.IsCancellationRequested)
                    {
                        var target = (ulong)Volatile.Read(ref pivotState).Header.BlockNumber;
                        var resume = bundle.Metadata.GetLastFetchedHeader();
                        if (resume >= target) break;
                        last = await backfiller.BackfillAsync(0, target, bfCt).ConfigureAwait(false);
                        // Loop: if pivotState rotated during the run, pick
                        // up the new range; otherwise the next iteration's
                        // (resume >= target) check exits.
                    }
                    return last ?? new ParallelBlockBackfiller.BackfillResult { Ran = false };
                }, bfCt);
            }

            SnapSyncClient.SyncResult syncResult = null!;
            bool healPhaseEntered = false;
            try
            {
                if (skipPhase2)
                {
                    // Phase 3 resume: skip leaf-stream entirely and drive heal
                    // directly against the persisted HealTargetRoot when tasks
                    // are complete but the trie is still incomplete.
                    syncResult = new SnapSyncClient.SyncResult
                    {
                        Sink = sink,
                        ComputedRoot = resumeFrom!.HealTargetRoot,
                        RootMatchesTarget = false,
                        AccountCount = sink.AccountCount,
                        FinalTargetRoot = resumeFrom.HealTargetRoot,
                    };
                    throw new InvalidOperationException(
                        $"resume Phase3 — driving heal against persisted target 0x{resumeFrom.HealTargetRoot.ToHex()} (does not match target — sentinel)");
                }
                // Staleness monitor: the sync runs a FIXED root; when the head moves
                // ~2x the pivot-trail distance ahead of the pivot we cancel it so the
                // invoker restarts the bootstrap at a fresh pivot. Snap state persists,
                // so the restart resumes the delta and heals to the new pivot.
                const ulong pivotTrailBlocks = 64;
                const int stalenessCheckMs = 12_000;
                var pivotBlock = (ulong)Volatile.Read(ref pivotState).Header.BlockNumber;
                using var staleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var staleMonitor = pivotRefresher == null
                    ? Task.CompletedTask
                    : Task.Run(async () =>
                    {
                        while (!staleCts.Token.IsCancellationRequested)
                        {
                            try { await Task.Delay(stalenessCheckMs, staleCts.Token).ConfigureAwait(false); }
                            catch (OperationCanceledException) { break; }
                            try
                            {
                                var fresh = await pivotRefresher(staleCts.Token).ConfigureAwait(false);
                                if (fresh.HasValue
                                    && (ulong)fresh.Value.Header.BlockNumber + pivotTrailBlocks
                                       >= pivotBlock + 2UL * pivotTrailBlocks)
                                {
                                    logger.LogWarning(
                                        "snap.pivot.stale old_pivot={Old} head>={Head}; cancelling sync to restart at a fresh pivot",
                                        pivotBlock, (ulong)fresh.Value.Header.BlockNumber + pivotTrailBlocks);
                                    staleCts.Cancel();
                                    break;
                                }
                            }
                            catch (OperationCanceledException) { break; }
                            catch { /* transient head-fetch failure — keep monitoring */ }
                        }
                    }, staleCts.Token);

                try
                {
                    syncResult = await client.SyncStateAsync(
                        pivot.StateRoot, resumeFrom, checkpointSink, staleCts.Token).ConfigureAwait(false);
                }
                finally
                {
                    staleCts.Cancel();
                    try { await staleMonitor.ConfigureAwait(false); } catch { }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Graceful shutdown — SnapSyncClient already flushed its last
                // checkpoint in its own finally block. Nothing extra to do
                // here, the row is already persisted at Phase2Running.
                throw;
            }
            catch (InvalidOperationException ex) when (scheduler != null && ex.Message.Contains("does not match target"))
            {
                // Leaf-stream phase finished but the computed state-root
                // disagrees with the live pivot. Mark Phase3Running with the
                // chosen heal-target root before invoking the healer so a kill
                // during heal resumes against this root instead of re-running
                // Phase 2.
                healPhaseEntered = true;
                logger.LogWarning(ex, "Snap leaf-stream root mismatch — entering heal phase");
                var healPivotEntry = Volatile.Read(ref pivotState);
                var healTargetEntry = skipPhase2
                    ? resumeFrom!.HealTargetRoot
                    : healPivotEntry.Header.StateRoot;
                bundle.Metadata.SaveSnapSyncState(new SnapSyncState
                {
                    SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                    Phase = SnapPhase.Phase3Running,
                    PivotBlockNumber = (ulong)healPivotEntry.Header.BlockNumber,
                    PivotBlockHash = healPivotEntry.Hash,
                    HealTargetRoot = healTargetEntry,
                    Tasks = resumeFrom?.Tasks ?? System.Array.Empty<SnapSyncAccountTask>(),
                    Counters = resumeFrom?.Counters ?? SnapSyncCounters.Zero,
                });
                logger.LogInformation(
                    "snap.phase.transition from=Phase2 to=Phase3 pivot={Pivot} heal_target=0x{Root}",
                    healPivotEntry.Header.BlockNumber, healTargetEntry.ToHex());

                var healer = new TrieHealer(scheduler, bundle.TrieNodes, logger, metrics);
                if (pivotRefresher != null)
                {
                    // Heal also needs to follow the moving pivot — peers'
                    // snapshots advance during the multi-hour heal phase.
                    // Use the same refresher Phase 2 used and update the
                    // tracked livePivot on each rotation.
                    healer.PivotRefresher = async refreshCt =>
                    {
                        var fresh = await pivotRefresher(refreshCt).ConfigureAwait(false);
                        if (fresh.HasValue)
                        {
                            var rotated = new PivotState(fresh.Value.Header, fresh.Value.Hash);
                            Interlocked.Exchange(ref pivotState, rotated);
                            return rotated.Header.StateRoot;
                        }
                        return Volatile.Read(ref pivotState).Header.StateRoot;
                    };
                }
                var healResult = await healer.HealAsync(healTargetEntry, ct).ConfigureAwait(false);
                if (!healResult.Matched)
                    throw new InvalidOperationException(
                        $"Snap-sync heal phase did not converge to target root 0x{healTargetEntry.ToHex()} for pivot block {healPivotEntry.Header.BlockNumber}");

                // Sanity gate: a real mainnet heal pulls millions of trie nodes
                // (state has ~250M leaves at block 23M). If the leaf stream wrote
                // zero AND heal returned "matched" after fetching only a handful
                // of nodes, the healer converged on a tiny rotated-into subtree —
                // NOT the canonical pivot state. Refuse to declare success; let
                // the next start retry against a fresh pivot.
                const int MinPlausibleStateNodes = 100_000;
                if (sink.AccountCount == 0 && healResult.TotalNodesFetched < MinPlausibleStateNodes && !skipPhase2)
                    throw new InvalidOperationException(
                        $"Snap-sync bogus-converge guard: heal claimed matched against root 0x{healResult.FinalTargetRoot.ToHex()} " +
                        $"but only {healResult.TotalNodesFetched} nodes fetched and leaf stream wrote 0 accounts. " +
                        $"Real mainnet state has ~250M nodes — this is a false positive (likely pivot rotated to a stale subtree). " +
                        $"Refusing to proceed.");

                syncResult = new SnapSyncClient.SyncResult
                {
                    Sink = sink,
                    ComputedRoot = healResult.ComputedRoot,
                    RootMatchesTarget = true,
                    AccountCount = sink.AccountCount,
                    FinalTargetRoot = healResult.FinalTargetRoot,
                };
            }
            catch (Exception)
            {
                // Abnormal exit — a stale-pivot cancel (staleCts, not ct) or any Phase-2 failure the
                // invoker will retry — skips the normal drain below. Cancel + await THIS attempt's Phase-1
                // backfiller so the retry's fresh backfiller cannot run overlapping it (the two-jobs leak).
                backfillCts.Cancel();
                if (backfillTask != null)
                {
                    try { await backfillTask.ConfigureAwait(false); } catch { }
                }
                throw;
            }

            logger.LogInformation(
                "Snap-bootstrap: state populated — {Accounts} accounts, {Slots} storage slots, {Codes} bytecodes (computed root matches pivot: {Match}).",
                sink.AccountCount, sink.SlotCount, sink.BytecodeCount, syncResult.RootMatchesTarget);

            if (backfillTask != null)
            {
                try
                {
                    var bf = await backfillTask.ConfigureAwait(false);
                    if (bf.Ran)
                    {
                        logger.LogInformation(
                            "Phase 1 backfill complete: {Blocks} blocks, {Txs} txs, {Rcpts} receipts persisted up to block {End}.",
                            bf.BlocksWritten, bf.TransactionsWritten, bf.ReceiptsWritten, bf.EndBlock);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Phase 1 backfill failed; node will have state at pivot but partial pre-pivot block archive.");
                }
            }

            var finalPivot = Volatile.Read(ref pivotState);
            var finalPivotHash = finalPivot.Hash;
            var pivotBlockNumber = (ulong)finalPivot.Header.BlockNumber;

            // A1: persist the FULL pivot header that the backward walker fetched,
            // hash-verified (keccak == trusted anchor) and laid as the top of its first
            // batch — NOT the canonical-tip anchor object, which carries only
            // BlockNumber + StateRoot. Refuse to finalise on a missing/partial header;
            // a retry re-runs the walker rather than persisting a corrupt pivot block.
            var finalPivotHeader = await bundle.Blocks.GetByHashAsync(finalPivotHash).ConfigureAwait(false);
            if (finalPivotHeader == null
                || finalPivotHeader.ParentHash == null || finalPivotHeader.ParentHash.Length != 32)
                throw new InvalidOperationException(
                    $"Snap-sync: full pivot header for block {pivotBlockNumber} (0x{finalPivotHash.ToHex()}) is not in the store at commit time. " +
                    $"The backward header walker must lay + verify it before finalising — refusing to persist a partial pivot header.");
            var existingHeaderCursor = bundle.Metadata.GetLastFetchedHeader();
            var existingBodyCursor = bundle.Metadata.GetLastFetchedBody();

            using (var batch = bundle.BeginBatch())
            {
                batch.PutHeader(finalPivotHeader, finalPivotHash);
                batch.Commit(pivotBlockNumber, finalPivotHash);
                if (existingHeaderCursor < pivotBlockNumber)
                    batch.SetLastFetchedHeader(pivotBlockNumber);
                if (existingBodyCursor < pivotBlockNumber)
                    batch.SetLastFetchedBody(pivotBlockNumber);
                // Phase=Complete is an in-batch sentinel for the atomic
                // multi-row commit. If the process dies between this batch
                // landing and the ClearSnapSyncState call below, the next
                // start observes Phase=Complete + LastBlock=pivot. The
                // LastBlock>0 guard above short-circuits fresh entry, so
                // the orphan record is harmless until the next snap cycle
                // clears it via the Phase=Complete_orphan branch.
                batch.SaveSnapSyncState(new SnapSyncState
                {
                    SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                    Phase = SnapPhase.Complete,
                    PivotBlockNumber = pivotBlockNumber,
                    PivotBlockHash = finalPivotHash,
                    HealTargetRoot = finalPivotHeader.StateRoot,
                    Tasks = System.Array.Empty<SnapSyncAccountTask>(),
                    Counters = SnapSyncCounters.Zero,
                });
                await batch.CommitAsync(ct).ConfigureAwait(false);
            }
            // Drop the SnapSyncState row now that LastBlock points at the
            // pivot. Steady-state operation should never observe a row
            // here — Phase=Complete is an in-batch transition signal, not
            // a persistent state. The healPhaseEntered flag is informational
            // only; the clear is unconditional once Commit succeeds.
            bundle.Metadata.ClearSnapSyncState();
            logger.LogInformation(
                "snap.phase.transition from={From} to=Complete pivot={Block} root=0x{Root}",
                healPhaseEntered ? "Phase3" : "Phase2",
                finalPivotHeader.BlockNumber, finalPivotHeader.StateRoot.ToHex());

            // Materialise the snap-bootstrapped state as a recoverable
            // checkpoint at the live pivot so the rewind machinery can
            // roll back to here if a later block diverges. Without this,
            // a freshly snap-bootstrapped node has no checkpoint to fall
            // back to and every divergence forces a full re-snap.
            try
            {
                await bundle.SaveCheckpointAsync(
                    (ulong)finalPivotHeader.BlockNumber, finalPivotHeader.StateRoot, finalPivotHash, ct).ConfigureAwait(false);
                logger.LogInformation(
                    "Snap-bootstrap: pivot checkpoint persisted at block {Block}.",
                    finalPivotHeader.BlockNumber);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Snap-bootstrap: checkpoint save failed at pivot {Block}; sync continues without it.",
                    finalPivotHeader.BlockNumber);
            }

            return new Result
            {
                Ran = true,
                PivotBlockNumber = (ulong)finalPivotHeader.BlockNumber,
                PivotStateRoot = finalPivotHeader.StateRoot,
                AccountCount = sink.AccountCount,
                SlotCount = sink.SlotCount,
                BytecodeCount = sink.BytecodeCount,
            };
        }
    }
}
