using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;
using static Nethereum.DevP2P.Sync.Snap.Client.SnapSyncClient;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class SnapPhase2Run
    {
        private readonly SnapSyncClient _client;
        private readonly Phase2Watchdog _watchdog;
        private readonly byte[] _targetRoot;
        private readonly SnapSyncState _resumeFrom;
        private readonly Action<SnapSyncCheckpoint> _checkpointSink;
        private readonly List<SnapSyncAccountTask> _seedTasks;
        private readonly SnapPhase2State _state;
        private readonly SnapTaskSet _taskSet;
        private readonly SnapPhase2Checkpointer _checkpointer;
        private FrozenRoot _frozen;
        private ITrieNodeStore _storageNodeStore;
        private Task _liveConsumers = Task.CompletedTask;
        private int _totalAccountCount;
        private int _reentryCount;

        public SnapPhase2Run(
            SnapSyncClient client,
            byte[] targetRoot,
            SnapSyncState resumeFrom,
            Action<SnapSyncCheckpoint> checkpointSink,
            List<SnapSyncAccountTask> seedTasks,
            SnapPhase2State state,
            SnapTaskSet taskSet,
            SnapPhase2Checkpointer checkpointer)
        {
            _client = client;
            _watchdog = new Phase2Watchdog(client);
            _targetRoot = targetRoot;
            _resumeFrom = resumeFrom;
            _checkpointSink = checkpointSink;
            _seedTasks = seedTasks;
            _state = state;
            _taskSet = taskSet;
            _checkpointer = checkpointer;
        }

        public async Task<SyncResult> RunAsync(CancellationToken ct)
        {
            var rootRefresher = _client.PivotRefresher;

            var frozen = new FrozenRoot(_targetRoot);
            _frozen = frozen;

            using var rootRollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var rootRollTask = rootRefresher == null ? Task.CompletedTask : Task.Run(() => frozen.RunDriverAsync(_client, rootRollCts, rootRefresher), rootRollCts.Token);

            bool completedSuccessfully = false;
            try
            {
                var (computedRoot, finalRoot) = await RunAttemptsAsync(ct).ConfigureAwait(false);

                var result = BuildSyncResult(computedRoot, finalRoot, _totalAccountCount);

                completedSuccessfully = result.RootMatchesTarget || _client.PivotCatchUp != null;
                return result;
            }
            finally
            {
                await ShutDownAsync(rootRollCts, rootRollTask, completedSuccessfully).ConfigureAwait(false);
            }
        }

        private async Task<(byte[] ComputedRoot, byte[] FinalRoot)> RunAttemptsAsync(CancellationToken ct)
        {
            _storageNodeStore = _client.RequireCursoredStorage();

            while (true)
            {
                var (attempt, firstFaultTask) = StartAttempt(ct);
                var completedTask = await Task.WhenAny(attempt.ConsumersTask, attempt.SupervisorTask, firstFaultTask).ConfigureAwait(false);

                var movedRoot = _frozen.Pending;
                if (movedRoot != null)
                {
                    await ReenterOnMoveAsync(attempt, movedRoot, ct).ConfigureAwait(false);
                    _reentryCount++;
                    continue;
                }

                if (attempt.AttemptCt.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    await ReenterOnSpuriousCancelAsync(attempt, ct).ConfigureAwait(false);
                    _reentryCount++;
                    continue;
                }

                if (completedTask == attempt.SupervisorTask)
                    await attempt.DrainAndThrowSupervisorFailureAsync(_client, ct).ConfigureAwait(false);

                if (completedTask == firstFaultTask)
                    await attempt.DrainAndThrowIfConsumerFaultedAsync(_client, firstFaultTask, ct).ConfigureAwait(false);

                var computedRoot = await CompleteAttemptAsync(attempt, ct).ConfigureAwait(false);
                return (computedRoot, attempt.AttemptRoot);
            }
        }

        private (Phase2Attempt Attempt, Task<Task> FirstFaultTask) StartAttempt(CancellationToken ct)
        {
            var attemptFrozenRoot = _frozen.Current;
            _client.OnPhase2CycleFrozen?.Invoke(attemptFrozenRoot);
            if (_reentryCount > 0)
                _client.Logger.LogInformation(
                    "snap.phase2.reenter new_root=0x{Root} attempt={Attempt}",
                    attemptFrozenRoot.ToHex(), _reentryCount + 1);

            var activeAccountRangeLeases = new ConcurrentDictionary<int, ActiveSnapLeaseInfo>();
            var phase2AttemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _frozen.BeginAttempt(phase2AttemptCts);
            var phase2Ct = phase2AttemptCts.Token;

            var consumers = StartConsumers(attemptFrozenRoot, activeAccountRangeLeases, phase2Ct);

            var consumersTask = Task.WhenAll(consumers);
            _liveConsumers = consumersTask;
            var supervisorTask = _client.RunPhase2LivenessSupervisorAsync(
                _taskSet, activeAccountRangeLeases, _state.ProgressSnapshot, phase2Ct);
            var firstFaultTask = SnapSyncClient.WaitForFirstFaultedConsumerAsync(consumers);
            var attempt = new Phase2Attempt(phase2AttemptCts, consumersTask, supervisorTask, attemptFrozenRoot);
            return (attempt, firstFaultTask);
        }

        private List<Task> StartConsumers(
            byte[] attemptFrozenRoot,
            ConcurrentDictionary<int, ActiveSnapLeaseInfo> activeAccountRangeLeases,
            CancellationToken phase2Ct)
        {
            var consumerCount = Math.Max(1, Math.Min(_seedTasks.Count, _client.AccountConcurrency));
            var consumers = new List<Task>(consumerCount);
            for (int i = 0; i < consumerCount; i++)
            {
                var consumerIdx = i;
                consumers.Add(Task.Run(() => new SnapAccountRangeConsumer(new ConsumerContext
                {
                    Client = _client,
                    TaskSet = _taskSet,
                    AttemptRoot = attemptFrozenRoot,
                    Frozen = _frozen,
                    Leases = activeAccountRangeLeases,
                    StorageNodeStore = _storageNodeStore,
                    State = _state,
                    Checkpointer = _checkpointer,
                    AddAccount = () => Interlocked.Increment(ref _totalAccountCount),
                }, consumerIdx).RunAsync(phase2Ct), phase2Ct));
            }

            return consumers;
        }

        private async Task ReenterOnMoveAsync(Phase2Attempt attempt, byte[] movedRoot, CancellationToken ct)
        {
            await attempt.DrainAndRevertAttemptAsync(
                _client, _taskSet,
                new OperationCanceledException($"snap.phase2.pivot_move new_root=0x{movedRoot.ToHex()}"),
                ct).ConfigureAwait(false);

            if (_client.PivotCatchUp != null)
            {
                var caughtUpRoot = await CatchUpToMovedPivotAsync(ct).ConfigureAwait(false);
                _frozen.Current = caughtUpRoot;
            }
            else
            {
                _client.Logger.LogInformation(
                    "snap.phase2.pivot_move new_root=0x{Root}", movedRoot.ToHex());
                _frozen.Current = movedRoot;
            }

            _client.OnPivotRolled?.Invoke();
        }

        private async Task ReenterOnSpuriousCancelAsync(Phase2Attempt attempt, CancellationToken ct)
        {
            await attempt.DrainAndRevertAttemptAsync(
                _client, _taskSet,
                new OperationCanceledException("snap.phase2.spurious_self_cancel"),
                ct).ConfigureAwait(false);

            _client.Logger.LogWarning("snap.phase2.reenter_spurious_self_cancel — driver-detected move signal was lost to a timing race; re-entering at the current frozen root and awaiting the driver's next tick");
        }

        private async Task<byte[]> CompleteAttemptAsync(Phase2Attempt attempt, CancellationToken ct)
        {
            byte[] computedRoot = null;
            attempt.Cts.Cancel();
            try { await attempt.SupervisorTask.ConfigureAwait(false); }
            catch (OperationCanceledException) when (attempt.AttemptCt.IsCancellationRequested) { }
            await attempt.ConsumersTask.ConfigureAwait(false);
            if (_client.PivotCatchUp == null)
                computedRoot = await _client.Sink.FinaliseRootAsync(ct).ConfigureAwait(false);
            return computedRoot;
        }

        private async Task ShutDownAsync(CancellationTokenSource rootRollCts, Task rootRollTask, bool completedSuccessfully)
        {
            rootRollCts.Cancel();
            try { await rootRollTask.ConfigureAwait(false); } catch { }
            _frozen.CancelCurrent();
            await DrainConsumersBeforeFinalCheckpointAsync(_liveConsumers).ConfigureAwait(false);
            _frozen.DisposeCurrent();

            EmitFinalCheckpoint(completedSuccessfully);
        }

        private async Task DrainConsumersBeforeFinalCheckpointAsync(Task consumers)
        {
            var drained = await Task.WhenAny(consumers, Task.Delay(_client.Phase2DrainTimeout)).ConfigureAwait(false);
            if (drained == consumers)
            {
                _ = consumers.Exception;
                return;
            }

            _client.Logger.LogError(
                "snap.phase2.final_checkpoint.undrained drain_timeout_sec={DrainTimeoutSec} — consumers did not stop before the final checkpoint; the checkpointer is sealed so they cannot persist progress after it",
                (int)_client.Phase2DrainTimeout.TotalSeconds);
            _watchdog.ObserveOrphanedConsumers(consumers);
        }

        private async Task<byte[]> CatchUpToMovedPivotAsync(CancellationToken ct)
        {
            _taskSet.DiscardParkedRangeAdvances();
            _checkpointer.Emit(SnapPhase.Phase2Running, healTargetRoot: null);
            var caughtUpRoot = await _client.PivotCatchUp(_taskSet.GetUnfinishedCheckpointSnapshot(), ct).ConfigureAwait(false);
            _client.Logger.LogInformation(
                "snap.phase2.pivot_move.bal_healed new_root=0x{Root}", caughtUpRoot.ToHex());
            return caughtUpRoot;
        }

        private SyncResult BuildSyncResult(byte[] computedRoot, byte[] finalRoot, int totalAccountCount)
        {
            var result = new SyncResult
            {
                Sink = _client.Sink,
                ComputedRoot = computedRoot,
                RootMatchesTarget = ByteUtil.AreEqual(computedRoot, finalRoot),
                AccountCount = totalAccountCount,
                FinalTargetRoot = finalRoot,
                AccountsNeedingHeal = _taskSet.NeedHealSnapshot(),
                CodeHashesNeedingHeal = new List<byte[]>(_state.CodeHashesNeedingHeal.Keys),
            };

            if (_client.Sink is InMemorySnapSyncSink mem)
            {
                result.StateTrie = mem.StateTrie;
                result.TrieStorage = mem.TrieStorage;
                foreach (var kv in mem.BytecodeByHash)
                    result.BytecodeByHash[kv.Key] = kv.Value;
            }

            return result;
        }

        private void EmitFinalCheckpoint(bool completedSuccessfully)
        {
            if (_checkpointSink == null)
                return;

            try
            {
                _checkpointer.EmitFinal(SnapPhase.Phase2Running, healTargetRoot: null);
            }
            catch (Exception ex)
            {
                if (completedSuccessfully)
                {
                    _client.Logger.LogError(ex,
                        "snap.phase2.checkpoint.blocked reason=final_checkpoint_failed");
                    throw new InvalidOperationException(
                        "Snap Phase 2 completed, but the final checkpoint could not be persisted.",
                        ex);
                }

                _client.Logger.LogCritical(ex,
                    "snap.phase2.checkpoint.final_failed_suppressed reason=preserve_primary_failure — " +
                    "the durable resume watermark was NOT promoted this attempt; any buffered-but-unflushed " +
                    "flat rows about to be abandoned are safely re-fetched by the next attempt, but this is " +
                    "the checkpoint pipeline's own safety net failing and should be investigated");
            }
        }
    }
}
