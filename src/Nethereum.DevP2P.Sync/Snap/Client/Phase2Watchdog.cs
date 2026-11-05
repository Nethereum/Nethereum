using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static Nethereum.DevP2P.Sync.Snap.Client.SnapSyncClient;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class Phase2Watchdog
    {
        private readonly SnapSyncClient _client;

        public Phase2Watchdog(SnapSyncClient client)
        {
            _client = client;
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

        public async Task RunSupervisorAsync(
            SnapTaskSet taskSet,
            ConcurrentDictionary<int, ActiveSnapLeaseInfo> activeAccountRangeLeases,
            Func<long> getProgressSnapshot,
            CancellationToken ct)
        {
            var poll = PollInterval();
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
                else
                {
                    ThrowIfTaskSetStalled(now, ownerlessSince, progress, diagnostics);
                }

                ThrowIfLeaseStalled(activeLeases, progress, diagnostics);
            }
        }

        private void ThrowIfTaskSetStalled(long now, long ownerlessSince, long progress, SnapTaskSetDiagnostics diagnostics)
        {
            if (!(_client.TaskSetStallTimeout > TimeSpan.Zero
                && TimeSpan.FromMilliseconds(Math.Max(0, now - ownerlessSince)) >= _client.TaskSetStallTimeout))
                return;

            var idleFor = TimeSpan.FromMilliseconds(Math.Max(0, now - ownerlessSince));
            _client.Logger.LogError(
                "snap.phase2.supervisor.taskset_stalled ownerless_idle_sec={IdleSec} progress={Progress} all_done={AllDone} has_leasable={HasLeasable} range_inflight={RangeInFlight} range_pending={RangePending} state_tasks={StateTasks} large_pending={LargePending} large_inflight={LargeInFlight} code_queue={CodeQueue}",
                (long)idleFor.TotalSeconds, progress, diagnostics.AllDone, diagnostics.HasLeasableWork,
                diagnostics.RangeInFlightCount, diagnostics.RangePendingCount, diagnostics.StateTaskCount,
                diagnostics.LargeSubtaskPendingCount, diagnostics.LargeSubtaskInFlightCount, diagnostics.CodeQueueCount);
            throw new SnapTaskSetStalledException(-1, idleFor, progress, diagnostics);
        }

        private void ThrowIfLeaseStalled(ActiveLeaseSnapshot activeLeases, long progress, SnapTaskSetDiagnostics diagnostics)
        {
            if (!(_client.ActiveLeaseStallTimeout > TimeSpan.Zero && activeLeases.Count > 0))
                return;

            var activeIdle = TimeSpan.FromSeconds(activeLeases.OldestAgeSeconds);
            if (activeIdle >= _client.ActiveLeaseStallTimeout)
            {
                _client.Logger.LogError(
                    "snap.phase2.supervisor.active_lease_stalled active_idle_sec={IdleSec} active_leases={ActiveLeases} oldest_owner={OldestOwner} oldest_task={OldestTask} oldest_origin=0x{OldestOrigin} oldest_limit=0x{OldestLimit} progress={Progress} all_done={AllDone} has_leasable={HasLeasable} range_inflight={RangeInFlight} range_pending={RangePending}",
                    (long)activeIdle.TotalSeconds, activeLeases.Count, activeLeases.OldestConsumer,
                    activeLeases.OldestTaskIndex, activeLeases.OldestOrigin, activeLeases.OldestLimit,
                    progress, diagnostics.AllDone, diagnostics.HasLeasableWork,
                    diagnostics.RangeInFlightCount, diagnostics.RangePendingCount);
                throw new SnapTaskLeaseStalledException(activeIdle, activeLeases, diagnostics);
            }
        }

        private TimeSpan PollInterval()
        {
            var timeout = _client.ActiveLeaseStallTimeout > TimeSpan.Zero ? _client.ActiveLeaseStallTimeout : _client.TaskSetStallTimeout;
            if (_client.TaskSetStallTimeout > TimeSpan.Zero && (timeout <= TimeSpan.Zero || _client.TaskSetStallTimeout < timeout))
                timeout = _client.TaskSetStallTimeout;
            if (timeout <= TimeSpan.Zero) return TimeSpan.FromSeconds(5);
            var ms = Math.Max(50, Math.Min(30_000, timeout.TotalMilliseconds / 6));
            return TimeSpan.FromMilliseconds(ms);
        }

        public async Task DrainAttemptAsync(Task consumersTask, Exception supervisorFailure, CancellationToken outerCt)
        {
            var drainTask = await Task.WhenAny(consumersTask, Task.Delay(_client.Phase2DrainTimeout, outerCt)).ConfigureAwait(false);
            if (drainTask == consumersTask)
            {
                try { await consumersTask.ConfigureAwait(false); }
                catch { }
                return;
            }

            int drainSec = (int)_client.Phase2DrainTimeout.TotalSeconds;
            _client.Logger.LogError(supervisorFailure,
                "snap.phase2.recycle_failed reason=undrained_attempt drain_timeout_sec={DrainTimeoutSec} — " +
                "consumers did not drain (blocked in uncancellable native calls); ABANDONING this attempt and " +
                "recycling with a fresh sink/client", drainSec);
            ObserveOrphanedConsumers(consumersTask);
            throw new SnapPhase2UndrainedException(
                "Snap Phase 2 liveness supervisor cancelled the attempt, but consumers did not drain within the bounded timeout.",
                _client.Phase2DrainTimeout, supervisorFailure);
        }

        internal static void ThrowConsumersFault(Task consumersTask)
        {
            var flattened = consumersTask.Exception!.Flatten();
            if (flattened.InnerExceptions.Count == 1)
                throw flattened.InnerExceptions[0];
            throw flattened;
        }

        internal void ObserveOrphanedConsumers(Task consumersTask)
        {
            _ = consumersTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _client.Logger.LogWarning(t.Exception?.Flatten(),
                        "snap.phase2.orphaned_consumers.completed_faulted — an abandoned Phase 2 consumer finally unblocked and faulted (attempt already recycled)");
                else
                    _client.Logger.LogInformation(
                        "snap.phase2.orphaned_consumers.completed outcome={Outcome} — an abandoned Phase 2 consumer finally unblocked (attempt already recycled)",
                        t.Status);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        internal static ActiveLeaseSnapshot SnapshotActiveLeases(ConcurrentDictionary<int, ActiveSnapLeaseInfo> activeLeases)
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
    }
}
