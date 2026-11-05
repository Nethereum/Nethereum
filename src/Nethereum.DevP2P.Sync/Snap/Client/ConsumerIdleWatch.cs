using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static Nethereum.DevP2P.Sync.Snap.Client.SnapSyncClient;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class ConsumerIdleWatch
    {
        internal const int IdleDelayMs = 20;

        internal enum IdleOutcome { AllDone, Retry }

        private readonly ConsumerContext _ctx;
        private readonly SnapSyncClient _client;
        private readonly SnapTaskSet _taskSet;
        private readonly int _consumerIdx;

        private long _idleSpins;
        private long _ownerlessIdleSpins;
        private long _lastIdleProgressSnapshot;
        private long _lastTaskSetDumpTick;

        public ConsumerIdleWatch(ConsumerContext ctx, int consumerIdx)
        {
            _ctx = ctx;
            _client = ctx.Client;
            _taskSet = ctx.TaskSet;
            _consumerIdx = consumerIdx;
            Reset();
        }

        public void Reset()
        {
            _idleSpins = 0;
            _ownerlessIdleSpins = 0;
            _lastIdleProgressSnapshot = _ctx.State.ProgressSnapshot();
        }

        public void NarratePeriodicTaskSetState()
        {
            if (_consumerIdx == 0 && Environment.TickCount64 - _lastTaskSetDumpTick > 15000)
            {
                _lastTaskSetDumpTick = Environment.TickCount64;
                _client.Logger.LogInformation("snap.taskset.state {State}", _taskSet.DescribeNotDone());
            }
        }

        public async Task<IdleOutcome> OnIdleAsync(CancellationToken ct)
        {
            if (_taskSet.AllDone) return IdleOutcome.AllDone;

            _idleSpins++;
            var diagnostics = _taskSet.GetDiagnostics();
            var activeLeases = Phase2Watchdog.SnapshotActiveLeases(_ctx.Leases);
            var progressSnapshot = _ctx.State.ProgressSnapshot();
            if (activeLeases.Count > 0 || progressSnapshot != _lastIdleProgressSnapshot)
            {
                _ownerlessIdleSpins = 0;
                _lastIdleProgressSnapshot = progressSnapshot;
            }
            else
            {
                _ownerlessIdleSpins++;
            }

            LogPeriodically(activeLeases, diagnostics, progressSnapshot);

            await Task.Delay(IdleDelayMs, ct).ConfigureAwait(false);
            return IdleOutcome.Retry;
        }

        private void LogPeriodically(ActiveLeaseSnapshot activeLeases, SnapTaskSetDiagnostics diagnostics, long progressSnapshot)
        {
            var idleFor = TimeSpan.FromMilliseconds(_idleSpins * (long)IdleDelayMs);
            var ownerlessIdleFor = TimeSpan.FromMilliseconds(_ownerlessIdleSpins * (long)IdleDelayMs);
            if (_idleSpins % 600 == 0)
            {
                if (activeLeases.Count > 0)
                {
                    _client.Logger.LogInformation(
                        "snap.taskset.waiting_on_active_work consumer={Consumer} idle_sec={IdleSec} active_leases={ActiveLeases} oldest_owner={OldestOwner} oldest_task={OldestTask} oldest_active_sec={OldestActiveSec} oldest_origin=0x{OldestOrigin} oldest_limit=0x{OldestLimit} progress={Progress} all_done={AllDone} has_leasable={HasLeasable} range_inflight={RangeInFlight} range_pending={RangePending}",
                        _consumerIdx, (long)idleFor.TotalSeconds, activeLeases.Count, activeLeases.OldestConsumer,
                        activeLeases.OldestTaskIndex, activeLeases.OldestAgeSeconds, activeLeases.OldestOrigin,
                        activeLeases.OldestLimit, progressSnapshot, diagnostics.AllDone, diagnostics.HasLeasableWork,
                        diagnostics.RangeInFlightCount, diagnostics.RangePendingCount);
                }
                else
                {
                    _client.Logger.LogWarning(
                        "snap.consumer.idle consumer={Consumer} no leasable fragment for {Spins} polls (~{Seconds}s) ownerless_idle_sec={OwnerlessIdleSec} progress={Progress} all_done={AllDone} has_leasable={HasLeasable} range_inflight={RangeInFlight} range_pending={RangePending} state_tasks={StateTasks} large_inflight={LargeInFlight} code_queue={CodeQueue}",
                        _consumerIdx, _idleSpins, (long)idleFor.TotalSeconds, (long)ownerlessIdleFor.TotalSeconds,
                        progressSnapshot, diagnostics.AllDone, diagnostics.HasLeasableWork,
                        diagnostics.RangeInFlightCount, diagnostics.RangePendingCount, diagnostics.StateTaskCount,
                        diagnostics.LargeSubtaskInFlightCount, diagnostics.CodeQueueCount);
                }
            }
        }
    }
}
