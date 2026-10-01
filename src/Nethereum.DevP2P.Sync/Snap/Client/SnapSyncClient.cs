using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.Documentation;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.ProofVerification;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    public partial class SnapSyncClient
    {
        private readonly ISnapPeer _peer;
        private readonly ISnapSyncSink _sink;
        private readonly int _accountsPerRequest;
        private readonly ulong _responseBytesBudget;
        private readonly ILogger _logger;
        private readonly SnapSyncMetrics _metrics;

        public SnapSyncClient(ISnapPeer peer, int accountsPerRequest = 256, ulong responseBytesBudget = 524_288UL)
            : this(peer, sink: null, accountsPerRequest, responseBytesBudget) { }

        public SnapSyncClient(
            ISnapPeer peer,
            ISnapSyncSink sink,
            int accountsPerRequest = 256,
            ulong responseBytesBudget = 524_288UL,
            ILogger logger = null,
            SnapSyncMetrics metrics = null)
        {
            _peer = peer ?? throw new ArgumentNullException(nameof(peer));
            _sink = sink ?? new InMemorySnapSyncSink();
            _accountsPerRequest = accountsPerRequest;
            _responseBytesBudget = responseBytesBudget;
            _logger = logger ?? NullLogger.Instance;
            _metrics = metrics;
        }

        private Action<int, int> _undersizedResumeWarning => (count, conc) =>
            _logger.LogWarning(
                "snap.resume.degraded tasks={Count} expected_concurrency={Conc}",
                count, conc);

        public Func<CancellationToken, Task<byte[]>> PivotRefresher { get; set; }

        public Action OnPivotRolled { get; set; }

        public Action<byte[]> OnPhase2CycleFrozen { get; set; }

        public Func<IReadOnlyList<SnapSyncAccountTask>, CancellationToken, Task<byte[]>> PivotCatchUp { get; set; }

        public Func<string> StateWriteBackpressure { get; set; }

        private const int SnapAccountRangeRetryDelayMs = 1_000;
        public int BytecodeDeadEndNoProgressRounds { get; set; } = 600;
        private const int MaxEmptyDispatchBackoffMs = 30_000;

        private const int SnapConsumerIdleDelayMs = 20;
        private const int StorageSubtaskRetryBackoffMs = 100;
        public int StateBackpressurePollMs { get; set; } = 5_000;

        public TimeSpan StateBackpressureWarnAfter { get; set; } = TimeSpan.FromMinutes(10);

        private long _stateBackpressurePausedSinceMs;

        public TimeSpan Phase2DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public int RootRefreshIntervalMs { get; set; } = 12_000;

        public int AccountConcurrency { get; set; } = 16;

        public TimeSpan TaskSetStallTimeout { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan ActiveLeaseStallTimeout { get; set; } = TimeSpan.FromMinutes(30);
        public int ActiveLeaseSameStageFailureBudget { get; set; } = 16;

        public Task<SyncResult> SyncStateAsync(byte[] targetRoot, CancellationToken ct = default)
            => SyncStateAsync(targetRoot, resumeFrom: null, checkpointSink: null, ct);

        public ulong CheckpointBytesThreshold { get; set; } = 8UL * 1024 * 1024;

        public Action FlushBulkFlatBeforeCheckpoint { get; set; }

        private const int MaxCodeRequestCount = 84;

        public async Task<SyncResult> SyncStateAsync(
            byte[] targetRoot,
            SnapSyncState resumeFrom,
            Action<SnapSyncState> checkpointSink,
            CancellationToken ct = default)
            => await SyncStateWithCheckpointAsync(
                targetRoot,
                resumeFrom,
                checkpointSink == null ? null : cp => checkpointSink(cp.State),
                ct).ConfigureAwait(false);

        private List<SnapSyncAccountTask> BuildSeedTasks(SnapSyncState resumeFrom, int concurrency)
        {
            List<SnapSyncAccountTask> seedTasks;
            if (resumeFrom != null && resumeFrom.Tasks != null && resumeFrom.Tasks.Count > 0)
            {
                seedTasks = new List<SnapSyncAccountTask>(resumeFrom.Tasks);
                if (seedTasks.Count < concurrency)
                {
                    _undersizedResumeWarning?.Invoke(seedTasks.Count, concurrency);
                }
            }
            else
            {
                seedTasks = new List<SnapSyncAccountTask>(concurrency);
                var ranges = SnapHashRanges.SplitHashRange(new byte[32], SnapHashRanges.FilledHash(0xff), concurrency);
                foreach (var range in ranges)
                {
                    seedTasks.Add(new SnapSyncAccountTask
                    {
                        Next = range.Start,
                        Last = range.End,
                        StorageCompleted = Array.Empty<byte[]>(),
                        SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(),
                    });
                }
            }
            return seedTasks;
        }

        [NethereumDocExample(DocSection.DevP2P, "devp2p-sync", "SnapSyncClient.SyncStateWithCheckpointAsync — resumable Phase-2 stream")]
        public async Task<SyncResult> SyncStateWithCheckpointAsync(
            byte[] targetRoot,
            SnapSyncState resumeFrom,
            Action<SnapSyncCheckpoint> checkpointSink,
            CancellationToken ct = default)
        {
            if (targetRoot == null || targetRoot.Length != 32)
                throw new ArgumentException("targetRoot must be 32 bytes", nameof(targetRoot));
            if (PivotCatchUp != null && checkpointSink == null)
                throw new InvalidOperationException(
                    "A snap/2 pivot catch-up persists the durable task frontier before it runs; Phase 2 needs a checkpoint sink.");

            await _sink.BeginAsync(targetRoot, ct).ConfigureAwait(false);

            var concurrency = Math.Max(1, AccountConcurrency);
            var largeContractConcurrency = Math.Max(1, LargeContractConcurrency);

            var seedTasks = BuildSeedTasks(resumeFrom, concurrency);

            _logger.LogInformation(
                "snap.phase2.concurrency account_workers={AccountWorkers} whale_partitions={WhalePartitions}",
                concurrency, largeContractConcurrency);

            var state = new SnapPhase2State(resumeFrom?.Counters, _metrics);

            var taskSet = new SnapTaskSet(seedTasks)
            {
                MaxRequestSizeBytes = _responseBytesBudget,
                LargeContractConcurrency = largeContractConcurrency,
            };

            var checkpointer = new SnapPhase2Checkpointer(
                state, taskSet, resumeFrom, checkpointSink, CheckpointBytesThreshold, FlushBulkFlatBeforeCheckpoint);

            var run = new SnapPhase2Run(this, targetRoot, resumeFrom, checkpointSink, seedTasks, state, taskSet, checkpointer);
            return await run.RunAsync(ct);
        }

        private async Task RunFrozenRootDriverAsync(
            CancellationTokenSource rootRollCts,
            Func<CancellationToken, Task<byte[]>> rootRefresher,
            int[] pivotRefresherLock,
            byte[][] frozenRootHolder,
            byte[][] pendingFrozenRootHolder,
            object attemptCtsLock,
            CancellationTokenSource[] currentAttemptCtsHolder)
        {
            while (!rootRollCts.Token.IsCancellationRequested)
            {
                try { await Task.Delay(RootRefreshIntervalMs, rootRollCts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                if (Interlocked.Exchange(ref pivotRefresherLock[0], 1) != 0) continue;
                try
                {
                    using var refreshCts = CancellationTokenSource.CreateLinkedTokenSource(rootRollCts.Token);
                    refreshCts.CancelAfter(RootRefreshIntervalMs);
                    byte[] fresh;
                    try
                    {
                        fresh = await rootRefresher(refreshCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!rootRollCts.Token.IsCancellationRequested)
                    {
                        _logger.LogWarning("snap.pivot.refresh_timeout after {Ms}ms — retrying next tick", RootRefreshIntervalMs);
                        continue;
                    }
                    if (fresh != null && fresh.Length == 32
                        && !ByteUtil.AreEqual(Volatile.Read(ref frozenRootHolder[0]), fresh))
                    {
                        lock (attemptCtsLock)
                        {
                            Volatile.Write(ref pendingFrozenRootHolder[0], fresh);
                            currentAttemptCtsHolder[0]?.Cancel();
                        }
                        _logger.LogInformation("snap.pivot.move_detected new_root=0x{Root}", fresh.ToHex());
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "snap.pivot.refresh_error after_ms={Ms} error_type={ErrorType}", RootRefreshIntervalMs, ex.GetType().Name);
                }
                finally { Volatile.Write(ref pivotRefresherLock[0], 0); }
            }
        }

        public static async Task<Task> WaitForFirstFaultedConsumerAsync(IReadOnlyList<Task> consumers)
        {
            var pending = new List<Task>(consumers);
            while (pending.Count > 0)
            {
                var completed = await Task.WhenAny(pending).ConfigureAwait(false);
                pending.Remove(completed);
                if (completed.IsFaulted || completed.IsCanceled)
                    return completed;
            }

            return null;
        }
        public async Task RunPhase2LivenessSupervisorAsync(
            SnapTaskSet taskSet,
            ConcurrentDictionary<int, ActiveSnapLeaseInfo> activeAccountRangeLeases,
            Func<long> getProgressSnapshot,
            CancellationToken ct)
        {
            var poll = GetPhase2LivenessPollInterval();
            long lastProgress = getProgressSnapshot();
            long ownerlessSince = Environment.TickCount64;

            while (true)
            {
                await Task.Delay(poll, ct).ConfigureAwait(false);
                var now = Environment.TickCount64;
                var diagnostics = taskSet.GetDiagnostics();
                if (diagnostics.AllDone) return;

                var activeLeases = SnapshotActiveLeases(activeAccountRangeLeases);
                var progress = getProgressSnapshot();
                var ownerlessNoWork = !diagnostics.HasLeasableWork
                    && activeLeases.Count == 0;

                if (!ownerlessNoWork || progress != lastProgress)
                {
                    ownerlessSince = now;
                    lastProgress = progress;
                }
                else if (TaskSetStallTimeout > TimeSpan.Zero
                    && TimeSpan.FromMilliseconds(Math.Max(0, now - ownerlessSince)) >= TaskSetStallTimeout)
                {
                    var idleFor = TimeSpan.FromMilliseconds(Math.Max(0, now - ownerlessSince));
                    _logger.LogError(
                        "snap.phase2.supervisor.taskset_stalled ownerless_idle_sec={IdleSec} progress={Progress} all_done={AllDone} has_leasable={HasLeasable} range_inflight={RangeInFlight} range_pending={RangePending} state_tasks={StateTasks} large_pending={LargePending} large_inflight={LargeInFlight} code_queue={CodeQueue}",
                        (long)idleFor.TotalSeconds, progress, diagnostics.AllDone, diagnostics.HasLeasableWork,
                        diagnostics.RangeInFlightCount, diagnostics.RangePendingCount, diagnostics.StateTaskCount,
                        diagnostics.LargeSubtaskPendingCount, diagnostics.LargeSubtaskInFlightCount, diagnostics.CodeQueueCount);
                    throw new SnapTaskSetStalledException(-1, idleFor, progress, diagnostics);
                }

                if (ActiveLeaseStallTimeout > TimeSpan.Zero && activeLeases.Count > 0)
                {
                    var activeIdle = TimeSpan.FromSeconds(activeLeases.OldestAgeSeconds);
                    if (activeIdle >= ActiveLeaseStallTimeout)
                    {
                        _logger.LogError(
                            "snap.phase2.supervisor.active_lease_stalled active_idle_sec={IdleSec} active_leases={ActiveLeases} oldest_owner={OldestOwner} oldest_task={OldestTask} oldest_origin=0x{OldestOrigin} oldest_limit=0x{OldestLimit} progress={Progress} all_done={AllDone} has_leasable={HasLeasable} range_inflight={RangeInFlight} range_pending={RangePending}",
                            (long)activeIdle.TotalSeconds, activeLeases.Count, activeLeases.OldestConsumer,
                            activeLeases.OldestTaskIndex, activeLeases.OldestOrigin, activeLeases.OldestLimit,
                            progress, diagnostics.AllDone, diagnostics.HasLeasableWork,
                            diagnostics.RangeInFlightCount, diagnostics.RangePendingCount);
                        throw new SnapTaskLeaseStalledException(activeIdle, activeLeases, diagnostics);
                    }
                }
            }
        }

        private TimeSpan GetPhase2LivenessPollInterval()
        {
            var timeout = ActiveLeaseStallTimeout > TimeSpan.Zero ? ActiveLeaseStallTimeout : TaskSetStallTimeout;
            if (TaskSetStallTimeout > TimeSpan.Zero && (timeout <= TimeSpan.Zero || TaskSetStallTimeout < timeout))
                timeout = TaskSetStallTimeout;
            if (timeout <= TimeSpan.Zero) return TimeSpan.FromSeconds(5);
            var ms = Math.Max(50, Math.Min(30_000, timeout.TotalMilliseconds / 6));
            return TimeSpan.FromMilliseconds(ms);
        }

        public async Task WaitWhileStateBackpressuredAsync(int consumerIdx, CancellationToken ct)
        {
            var backpressure = StateWriteBackpressure;
            if (backpressure == null) return;

            string pressure; int pausePolls = 0;
            while ((pressure = backpressure()) != null)
            {
                ct.ThrowIfCancellationRequested();
                var pausedFor = MarkPausedAndPublishBackpressureGauge();
                if (consumerIdx == 0 && pausePolls % 6 == 0)
                    LogStateBackpressurePause(pausedFor, pressure);
                pausePolls++;
                await Task.Delay(StateBackpressurePollMs, ct).ConfigureAwait(false);
            }

            var pausedSince = Interlocked.Exchange(ref _stateBackpressurePausedSinceMs, 0);
            if (pausedSince != 0 || pausePolls > 0)
                _metrics?.SetPhase2BackpressurePausedFor(TimeSpan.Zero);
            if (pausedSince == 0) return;
            _logger.LogInformation(
                "Phase 2 leaf stream: resumed after {PausedSec}s of storage backpressure",
                (long)ElapsedSince(pausedSince).TotalSeconds);
        }

        private TimeSpan MarkPausedAndPublishBackpressureGauge()
        {
            var now = Environment.TickCount64;
            var since = Interlocked.CompareExchange(ref _stateBackpressurePausedSinceMs, now, 0);
            var pausedFor = since == 0 ? TimeSpan.Zero : ElapsedSince(since);
            _metrics?.SetPhase2BackpressurePausedFor(pausedFor);
            return pausedFor;
        }

        private static TimeSpan ElapsedSince(long sinceMs)
            => TimeSpan.FromMilliseconds(Math.Max(0, Environment.TickCount64 - sinceMs));

        private void LogStateBackpressurePause(TimeSpan pausedFor, string pressure)
        {
            if (pausedFor >= StateBackpressureWarnAfter)
                _logger.LogWarning(
                    "Phase 2 leaf stream: paused for {PausedSec}s — storage backpressure has not released for over {WarnAfterSec}s: {Pressure}. " +
                    "State download stays paused until the store's write valve releases; check the store's compaction and the pressure figures.",
                    (long)pausedFor.TotalSeconds, (long)StateBackpressureWarnAfter.TotalSeconds, pressure);
            else
                _logger.LogInformation(
                    "Phase 2 leaf stream: paused for {PausedSec}s — storage backpressure: {Pressure}",
                    (long)pausedFor.TotalSeconds, pressure);
        }

        public async Task DrainPhase2AttemptAsync(Task consumersTask, Exception supervisorFailure, CancellationToken outerCt)
        {
            var drainTask = await Task.WhenAny(consumersTask, Task.Delay(Phase2DrainTimeout, outerCt)).ConfigureAwait(false);
            if (drainTask == consumersTask)
            {
                try { await consumersTask.ConfigureAwait(false); }
                catch { }
                return;
            }

            int drainSec = (int)Phase2DrainTimeout.TotalSeconds;
            _logger.LogError(supervisorFailure,
                "snap.phase2.recycle_failed reason=undrained_attempt drain_timeout_sec={DrainTimeoutSec} — " +
                "consumers did not drain (blocked in uncancellable native calls); ABANDONING this attempt and " +
                "recycling with a fresh sink/client", drainSec);
            ObserveOrphanedConsumers(consumersTask);
            throw new SnapPhase2UndrainedException(
                "Snap Phase 2 liveness supervisor cancelled the attempt, but consumers did not drain within the bounded timeout.",
                Phase2DrainTimeout, supervisorFailure);
        }

        private static void ThrowConsumersFault(Task consumersTask)
        {
            var flattened = consumersTask.Exception!.Flatten();
            if (flattened.InnerExceptions.Count == 1)
                throw flattened.InnerExceptions[0];
            throw flattened;
        }

        private void ObserveOrphanedConsumers(Task consumersTask)
        {
            _ = consumersTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _logger.LogWarning(t.Exception?.Flatten(),
                        "snap.phase2.orphaned_consumers.completed_faulted — an abandoned Phase 2 consumer finally unblocked and faulted (attempt already recycled)");
                else
                    _logger.LogInformation(
                        "snap.phase2.orphaned_consumers.completed outcome={Outcome} — an abandoned Phase 2 consumer finally unblocked (attempt already recycled)",
                        t.Status);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private void FlushAccountTrieForCheckpoint()
        {
            if (_sink is TrieSnapSyncSink trieSink)
            {
                trieSink.FlushAccountTrieForCheckpoint();
                _logger.LogDebug("snap.phase2.account_obligation.completed state=account_trie_nodes_written");
            }
        }

        public int LargeContractConcurrency { get; set; } = 16;

        private static ActiveLeaseSnapshot SnapshotActiveLeases(ConcurrentDictionary<int, ActiveSnapLeaseInfo> activeLeases)
        {
            var now = Environment.TickCount64;
            ActiveSnapLeaseInfo oldest = null;
            int count = 0;
            foreach (var lease in activeLeases.Values)
            {
                count++;
                if (oldest == null || lease.LastProductiveTick < oldest.LastProductiveTick)
                    oldest = lease;
            }

            if (oldest == null)
                return new ActiveLeaseSnapshot(0, -1, -1, string.Empty, string.Empty, 0);

            var ageSeconds = Math.Max(0, (now - oldest.LastProductiveTick) / 1000);
            return new ActiveLeaseSnapshot(
                count,
                oldest.Consumer,
                oldest.TaskIndex,
                oldest.Origin,
                oldest.Limit,
                ageSeconds);
        }

        private static string ShortHash(byte[] value)
        {
            if (value == null || value.Length == 0) return string.Empty;
            var hex = value.ToHex();
            return hex.Length <= 16 ? hex : hex.Substring(0, 16);
        }

    }
}



