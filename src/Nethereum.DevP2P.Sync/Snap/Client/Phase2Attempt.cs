using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class Phase2Attempt
    {
        public Phase2Attempt(CancellationTokenSource cts, Task consumersTask, Task supervisorTask, byte[] attemptRoot)
        {
            Cts = cts;
            ConsumersTask = consumersTask;
            SupervisorTask = supervisorTask;
            AttemptRoot = attemptRoot;
        }

        public CancellationTokenSource Cts { get; }
        public Task ConsumersTask { get; }
        public Task SupervisorTask { get; }
        public byte[] AttemptRoot { get; }
        public CancellationToken AttemptCt => Cts.Token;

        internal async Task<Exception> FailureOf(Task task, string fallback)
        {
            try
            {
                await task.ConfigureAwait(false);
                return new InvalidOperationException(fallback);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && AttemptCt.IsCancellationRequested))
            {
                return ex;
            }
        }

        internal async Task DrainAndRevertAttemptAsync(
            SnapSyncClient client, SnapTaskSet taskSet, Exception reason, CancellationToken ct)
        {
            Cts.Cancel();
            await client.DrainPhase2AttemptAsync(ConsumersTask, reason, ct).ConfigureAwait(false);

            if (ConsumersTask.IsFaulted)
                Phase2Watchdog.ThrowConsumersFault(ConsumersTask);

            try { await SupervisorTask.ConfigureAwait(false); }
            catch (OperationCanceledException) when (AttemptCt.IsCancellationRequested) { }

            taskSet.RevertAllInFlightLeases();
        }

        internal async Task DrainAndThrowSupervisorFailureAsync(SnapSyncClient client, CancellationToken ct)
        {
            var supervisorFailure = await FailureOf(
                SupervisorTask, "Snap Phase 2 liveness supervisor exited before consumers completed.").ConfigureAwait(false);
            Cts.Cancel();
            await client.DrainPhase2AttemptAsync(ConsumersTask, supervisorFailure, ct).ConfigureAwait(false);
            throw supervisorFailure;
        }

        internal async Task DrainAndThrowIfConsumerFaultedAsync(
            SnapSyncClient client, Task<Task> firstFaultTask, CancellationToken ct)
        {
            var faultedConsumer = await firstFaultTask.ConfigureAwait(false);
            if (faultedConsumer == null) return;
            var consumerFailure = await FailureOf(
                faultedConsumer, "Snap Phase 2 consumer fault monitor completed without a consumer exception.").ConfigureAwait(false);
            Cts.Cancel();
            await client.DrainPhase2AttemptAsync(ConsumersTask, consumerFailure, ct).ConfigureAwait(false);
            throw consumerFailure;
        }
    }
}
