using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Result = Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapBootstrapper.Result;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed class Snap1Run
    {
        private readonly SnapRunContext _context;

        private readonly Phase3Heal _heal;

        private Snap1Run(SnapRunContext context)
        {
            _context = context;
            _heal = new Phase3Heal(
                context.Bundle, context.Scheduler, context.Pivots, context.Pool, context.Metrics, context.Logger);
        }

        internal static Snap1Run Create(SnapRunContext context) => new Snap1Run(context);

        internal async Task<Result> RunAsync(CancellationToken ct)
        {
            var plan = ResumeRouter.Route(_context.Bundle, _context.Pivot, _context.PivotHash, _context.Logger);

            LogStarting();

            using var bulkFlat = CreateBulkFlat();
            var sink = CreateSink(bulkFlat);
            var client = _context.BuildClient(sink, bulkFlat);

            var checkpointSink = _context.CheckpointSink(() => _context.Pivots.Current);

            ResumeRouter.EnterPhase2(_context, _context.Pivot, _context.PivotHash, plan, _context.BackfillOnly);

            var phase1 = await Phase1History.StartAsync(_context, ct).ConfigureAwait(false);
            if (phase1.BackfillOnlyResult != null)
                return phase1.BackfillOnlyResult;

            var outcome = await RunPhases2And3Async(
                client, sink, plan, checkpointSink, phase1, bulkFlat, ct).ConfigureAwait(false);

            LogPopulated(sink, outcome.Result);

            return await Phase4Finalize.FinishAsync(
                _context, phase1,
                () => FinalizeAsync(bulkFlat, plan, outcome, phase1, ct),
                () => (sink.AccountCount, sink.SlotCount, sink.BytecodeCount),
                ct).ConfigureAwait(false);
        }

        private void LogStarting()
        {
            _context.Logger.LogInformation(
                "Snap-bootstrap: starting fetch at pivot block={Block} hash=0x{Hash} stateRoot=0x{Root}",
                _context.Pivot.BlockNumber, _context.PivotHash.ToHex(), _context.Pivot.StateRoot.ToHex());
        }

        private IBulkFlatStateSink CreateBulkFlat()
            => (_context.Bundle as IBulkFlatStateSinkProvider)?.CreateBulkFlatSink();

        private TrieSnapSyncSink CreateSink(IBulkFlatStateSink bulkFlat)
        {
            var bundle = _context.Bundle;
            return bulkFlat != null
                ? new TrieSnapSyncSink(bundle.StateTrieNodes, bundle.State, bulkFlat)
                : new TrieSnapSyncSink(bundle.StateTrieNodes, bundle.State);
        }

        private async Task<StateSyncOutcome> RunPhases2And3Async(
            SnapSyncClient client, TrieSnapSyncSink sink, ResumePlan plan,
            Action<SnapSyncClient.SnapSyncCheckpoint> checkpointSink,
            Phase1History phase1, IBulkFlatStateSink bulkFlat, CancellationToken ct)
        {
            try
            {
                if (plan.SkipPhase2)
                    return await _heal.ResumeAsync(plan.ResumeFrom, sink, ct).ConfigureAwait(false);

                var streamed = await new Phase2Stream(_context, client, plan.ResumeFrom, checkpointSink)
                    .StreamAsync(ct)
                    .ConfigureAwait(false);
                return await _heal.AfterStreamAsync(streamed, plan.ResumeFrom, sink, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await phase1.AbandonAsync(bulkFlat).ConfigureAwait(false);
                throw;
            }
        }

        private void LogPopulated(TrieSnapSyncSink sink, SnapSyncClient.SyncResult syncResult)
        {
            _context.Logger.LogInformation(
                "Snap-bootstrap: state populated — {Accounts} accounts, {Slots} storage slots, {Codes} bytecodes (computed root matches pivot: {Match}).",
                sink.AccountCount, sink.SlotCount, sink.BytecodeCount, syncResult.RootMatchesTarget);
        }

        private async Task<BlockHeader> FinalizeAsync(
            IBulkFlatStateSink bulkFlat, ResumePlan plan, StateSyncOutcome outcome,
            Phase1History phase1, CancellationToken ct)
        {
            var bundle = _context.Bundle;
            Phase4Finalize.FlushBulk(bulkFlat, bundle);

            var finalPivot = await new FlatCertifier(bundle, _context.Scheduler, _context.Pivots, _heal, _context.Logger)
                .RunAsync(outcome.ConvergedPivot, ct).ConfigureAwait(false);

            return await Phase4Finalize.CompleteAsync(
                _context, finalPivot.Hash, (ulong)finalPivot.Header.BlockNumber, phase1.BackfillTask != null,
                outcome.HealEntered, ct).ConfigureAwait(false);
        }
    }
}
