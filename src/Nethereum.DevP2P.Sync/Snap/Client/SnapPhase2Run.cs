using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    public partial class SnapSyncClient
    {
        private sealed class SnapPhase2Run
        {
            private readonly SnapSyncClient _client;
            private readonly byte[] _targetRoot;
            private readonly SnapSyncState _resumeFrom;
            private readonly Action<SnapSyncCheckpoint> _checkpointSink;
            private readonly List<SnapSyncAccountTask> _seedTasks;
            private readonly SnapPhase2State _state;
            private readonly SnapTaskSet _taskSet;
            private readonly SnapPhase2Checkpointer _checkpointer;

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
                var pivotRefresherLock = new int[1];
                var rootRefresher = _client.PivotRefresher;

                var frozenRootHolder = new byte[][] { _targetRoot };
                var pendingFrozenRootHolder = new byte[][] { null };
                var attemptCtsLock = new object();
                var currentAttemptCtsHolder = new CancellationTokenSource[] { null };

                using var rootRollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var rootRollTask = rootRefresher == null ? Task.CompletedTask : Task.Run(() => _client.RunFrozenRootDriverAsync(rootRollCts, rootRefresher, pivotRefresherLock, frozenRootHolder, pendingFrozenRootHolder, attemptCtsLock, currentAttemptCtsHolder), rootRollCts.Token);

                bool completedSuccessfully = false;
                Task liveConsumers = Task.CompletedTask;
                try
                {
                    int totalAccountCount = 0;
                    var accountsNeedingHeal = new ConcurrentBag<AccountNeedingHeal>();

                    var storageNodeStore = (_client._sink as TrieSnapSyncSink)?.NodeStore;
                    var cursoredWhalesSupported = storageNodeStore != null || _client._sink is FlatSnapSyncSink;

                    byte[] finalRoot = null;
                    byte[] computedRoot = null;
                    int reentryCount = 0;

                    while (true)
                    {
                        var attemptFrozenRoot = Volatile.Read(ref frozenRootHolder[0]);
                        var liveTargetRootHolder = new byte[][] { attemptFrozenRoot };
                        _client.OnPhase2CycleFrozen?.Invoke(attemptFrozenRoot);
                        if (reentryCount > 0)
                            _client._logger.LogInformation(
                                "snap.phase2.reenter new_root=0x{Root} attempt={Attempt}",
                                attemptFrozenRoot.ToHex(), reentryCount + 1);

                        var activeAccountRangeLeases = new ConcurrentDictionary<int, ActiveSnapLeaseInfo>();
                        var phase2AttemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        CancellationTokenSource previousAttemptCts;
                        lock (attemptCtsLock)
                        {
                            previousAttemptCts = currentAttemptCtsHolder[0];
                            currentAttemptCtsHolder[0] = phase2AttemptCts;
                            Volatile.Write(ref pendingFrozenRootHolder[0], null);
                        }
                        previousAttemptCts?.Dispose();
                        var phase2Ct = phase2AttemptCts.Token;

                        var consumerCount = Math.Max(1, Math.Min(_seedTasks.Count, _client.AccountConcurrency));
                        var consumers = new List<Task>(consumerCount);
                        for (int i = 0; i < consumerCount; i++)
                        {
                            var consumerIdx = i;
                            consumers.Add(Task.Run(() => new SnapAccountRangeConsumer(
                                _client,
                                consumerIdx,
                                _taskSet,
                                liveTargetRootHolder,
                                accountsNeedingHeal,
                                _state.DeferredStorageDebts,
                                activeAccountRangeLeases,
                                storageNodeStore,
                                cursoredWhalesSupported,
                                _state.ProgressSnapshot,
                                _resumeFrom?.PivotBlockNumber,
                                _state.PublishAccountDeltas,
                                () => Interlocked.Increment(ref totalAccountCount),
                                _state.PublishBytecodes,
                                _state.PublishDeferredCode,
                                _checkpointer.MaybeCheckpoint,
                                frozenRootHolder).RunAsync(phase2Ct), phase2Ct));
                        }

                        var consumersTask = Task.WhenAll(consumers);
                        liveConsumers = consumersTask;
                        var supervisorTask = _client.RunPhase2LivenessSupervisorAsync(
                            _taskSet, activeAccountRangeLeases, _state.ProgressSnapshot, phase2Ct);
                        var firstFaultTask = SnapSyncClient.WaitForFirstFaultedConsumerAsync(consumers);
                        var attempt = new Phase2Attempt(phase2AttemptCts, consumersTask, supervisorTask);
                        var completedTask = await Task.WhenAny(consumersTask, supervisorTask, firstFaultTask).ConfigureAwait(false);

                        var movedRoot = Volatile.Read(ref pendingFrozenRootHolder[0]);
                        if (movedRoot != null)
                        {
                            await DrainAndRevertAttemptAsync(
                                attempt,
                                new OperationCanceledException($"snap.phase2.pivot_move new_root=0x{movedRoot.ToHex()}"),
                                ct).ConfigureAwait(false);

                            if (_client.PivotCatchUp != null)
                            {
                                var caughtUpRoot = await CatchUpToMovedPivotAsync(ct).ConfigureAwait(false);
                                Volatile.Write(ref frozenRootHolder[0], caughtUpRoot);
                            }
                            else
                            {
                                _client._logger.LogInformation(
                                    "snap.phase2.pivot_move new_root=0x{Root}", movedRoot.ToHex());
                                Volatile.Write(ref frozenRootHolder[0], movedRoot);
                            }

                            _client.OnPivotRolled?.Invoke();
                            reentryCount++;
                            continue;
                        }

                        if (phase2Ct.IsCancellationRequested && !ct.IsCancellationRequested)
                        {
                            await DrainAndRevertAttemptAsync(
                                attempt,
                                new OperationCanceledException("snap.phase2.spurious_self_cancel"),
                                ct).ConfigureAwait(false);

                            _client._logger.LogWarning("snap.phase2.reenter_spurious_self_cancel — driver-detected move signal was lost to a timing race; re-entering at the current frozen root and awaiting the driver's next tick");
                            reentryCount++;
                            continue;
                        }

                        if (completedTask == supervisorTask)
                            await DrainAndThrowSupervisorFailureAsync(attempt, ct).ConfigureAwait(false);

                        if (completedTask == firstFaultTask)
                            await DrainAndThrowIfConsumerFaultedAsync(attempt, firstFaultTask, ct).ConfigureAwait(false);

                        phase2AttemptCts.Cancel();
                        try { await supervisorTask.ConfigureAwait(false); }
                        catch (OperationCanceledException) when (phase2Ct.IsCancellationRequested) { }
                        await consumersTask.ConfigureAwait(false);
                        if (_client.PivotCatchUp == null)
                            computedRoot = await _client._sink.FinaliseRootAsync(ct).ConfigureAwait(false);
                        finalRoot = Volatile.Read(ref liveTargetRootHolder[0]);
                        break;
                    }

                    var result = BuildSyncResult(computedRoot, finalRoot, totalAccountCount, accountsNeedingHeal);

                    completedSuccessfully = true;
                    return result;
                }
                finally
                {
                    rootRollCts.Cancel();
                    try { await rootRollTask.ConfigureAwait(false); } catch { }
                    lock (attemptCtsLock) { currentAttemptCtsHolder[0]?.Cancel(); }
                    await DrainConsumersBeforeFinalCheckpointAsync(liveConsumers).ConfigureAwait(false);
                    lock (attemptCtsLock) { currentAttemptCtsHolder[0]?.Dispose(); currentAttemptCtsHolder[0] = null; }

                    EmitFinalCheckpoint(completedSuccessfully);
                }
            }

            private async Task DrainConsumersBeforeFinalCheckpointAsync(Task consumers)
            {
                var drained = await Task.WhenAny(consumers, Task.Delay(_client.Phase2DrainTimeout)).ConfigureAwait(false);
                if (drained == consumers)
                {
                    _ = consumers.Exception;
                    return;
                }

                _client._logger.LogError(
                    "snap.phase2.final_checkpoint.undrained drain_timeout_sec={DrainTimeoutSec} — consumers did not stop before the final checkpoint; the checkpointer is sealed so they cannot persist progress after it",
                    (int)_client.Phase2DrainTimeout.TotalSeconds);
                _client.ObserveOrphanedConsumers(consumers);
            }

            private async Task<byte[]> CatchUpToMovedPivotAsync(CancellationToken ct)
            {
                _taskSet.DiscardParkedRangeAdvances();
                _checkpointer.Emit(SnapPhase.Phase2Running, healTargetRoot: null);
                var caughtUpRoot = await _client.PivotCatchUp(_taskSet.GetUnfinishedCheckpointSnapshot(), ct).ConfigureAwait(false);
                _client._logger.LogInformation(
                    "snap.phase2.pivot_move.bal_healed new_root=0x{Root}", caughtUpRoot.ToHex());
                return caughtUpRoot;
            }

            private async Task DrainAndRevertAttemptAsync(Phase2Attempt attempt, Exception reason, CancellationToken ct)
            {
                attempt.Cts.Cancel();
                await _client.DrainPhase2AttemptAsync(attempt.ConsumersTask, reason, ct).ConfigureAwait(false);

                if (attempt.ConsumersTask.IsFaulted)
                    SnapSyncClient.ThrowConsumersFault(attempt.ConsumersTask);

                try { await attempt.SupervisorTask.ConfigureAwait(false); }
                catch (OperationCanceledException) when (attempt.AttemptCt.IsCancellationRequested) { }

                _taskSet.RevertAllInFlightLeases();
            }

            private async Task DrainAndThrowSupervisorFailureAsync(Phase2Attempt attempt, CancellationToken ct)
            {
                Exception supervisorFailure;
                try
                {
                    await attempt.SupervisorTask.ConfigureAwait(false);
                    supervisorFailure = new InvalidOperationException("Snap Phase 2 liveness supervisor exited before consumers completed.");
                }
                catch (Exception ex) when (!(ex is OperationCanceledException && attempt.AttemptCt.IsCancellationRequested))
                {
                    supervisorFailure = ex;
                }

                attempt.Cts.Cancel();
                await _client.DrainPhase2AttemptAsync(attempt.ConsumersTask, supervisorFailure, ct).ConfigureAwait(false);
                throw supervisorFailure;
            }

            private async Task DrainAndThrowIfConsumerFaultedAsync(Phase2Attempt attempt, Task<Task> firstFaultTask, CancellationToken ct)
            {
                var faultedConsumer = await firstFaultTask.ConfigureAwait(false);
                if (faultedConsumer != null)
                {
                    Exception consumerFailure;
                    try
                    {
                        await faultedConsumer.ConfigureAwait(false);
                        consumerFailure = new InvalidOperationException("Snap Phase 2 consumer fault monitor completed without a consumer exception.");
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException && attempt.AttemptCt.IsCancellationRequested))
                    {
                        consumerFailure = ex;
                    }

                    attempt.Cts.Cancel();
                    await _client.DrainPhase2AttemptAsync(attempt.ConsumersTask, consumerFailure, ct).ConfigureAwait(false);
                    throw consumerFailure;
                }
            }

            private SyncResult BuildSyncResult(byte[] computedRoot, byte[] finalRoot, int totalAccountCount, ConcurrentBag<AccountNeedingHeal> accountsNeedingHeal)
            {
                var result = new SyncResult
                {
                    Sink = _client._sink,
                    ComputedRoot = computedRoot,
                    RootMatchesTarget = ByteUtil.AreEqual(computedRoot, finalRoot),
                    AccountCount = totalAccountCount,
                    FinalTargetRoot = finalRoot,
                    AccountsNeedingHeal = new List<AccountNeedingHeal>(accountsNeedingHeal),
                    CodeHashesNeedingHeal = new List<byte[]>(_state.CodeHashesNeedingHeal.Keys),
                };

                if (_client._sink is InMemorySnapSyncSink mem)
                {
                    result.StateTrie = mem.StateTrie;
                    result.TrieStorage = mem.TrieStorage;
                    foreach (var kv in mem.BytecodeByHash)
                        result.BytecodeByHash[kv.Key] = kv.Value;
                }

                if (!result.RootMatchesTarget && _client.PivotCatchUp == null)
                    throw new SnapRootMismatchException(
                        $"Snap-sync result root {result.ComputedRoot.ToHex()} does not match target {finalRoot.ToHex()} — peer returned tampered or incomplete data",
                        result.AccountsNeedingHeal,
                        result.CodeHashesNeedingHeal);

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
                        _client._logger.LogError(ex,
                            "snap.phase2.checkpoint.blocked reason=final_checkpoint_failed");
                        throw new InvalidOperationException(
                            "Snap Phase 2 completed, but the final checkpoint could not be persisted.",
                            ex);
                    }

                    _client._logger.LogCritical(ex,
                        "snap.phase2.checkpoint.final_failed_suppressed reason=preserve_primary_failure — " +
                        "the durable resume watermark was NOT promoted this attempt; any buffered-but-unflushed " +
                        "flat rows about to be abandoned are safely re-fetched by the next attempt, but this is " +
                        "the checkpoint pipeline's own safety net failing and should be investigated");
                }
            }

            private readonly struct Phase2Attempt
            {
                public Phase2Attempt(CancellationTokenSource cts, Task consumersTask, Task supervisorTask)
                {
                    Cts = cts;
                    ConsumersTask = consumersTask;
                    SupervisorTask = supervisorTask;
                }

                public CancellationTokenSource Cts { get; }
                public Task ConsumersTask { get; }
                public Task SupervisorTask { get; }
                public CancellationToken AttemptCt => Cts.Token;
            }
        }
    }
}
