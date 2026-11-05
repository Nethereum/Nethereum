using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using PivotState = Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapBootstrapper.PivotState;
using Result = Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapBootstrapper.Result;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed class Snap2Run
    {
        private sealed record Snap2Resume(SnapSyncState ResumeFrom, PivotState Applied, bool Generating);

        private readonly SnapRunContext _context;
        private readonly IFetchRequestScheduler _scheduler;
        private readonly IPeerPool _pool;
        private readonly IFlatStateTrieGenerator _generator;

        private Snap2Run(
            SnapRunContext context, IFetchRequestScheduler scheduler, IPeerPool pool, IFlatStateTrieGenerator generator)
        {
            _context = context;
            _scheduler = scheduler;
            _pool = pool;
            _generator = generator;
        }

        internal static Snap2Run Create(SnapRunContext context)
        {
            var scheduler = context.Scheduler
                ?? throw new InvalidOperationException("snap/2 needs a request scheduler.");
            var pool = context.Pool
                ?? throw new InvalidOperationException("snap/2 needs a peer pool to fetch block access lists.");
            var generator = context.Bundle as IFlatStateTrieGenerator
                ?? throw new InvalidOperationException($"snap/2 needs a bundle that generates the trie from flat state; {context.Bundle.GetType().Name} does not.");
            if (!context.ExternalHeaderFollow)
                throw new InvalidOperationException("snap/2 reads gap headers from the local canonical chain and needs external header follow.");
            return new Snap2Run(context, scheduler, pool, generator);
        }

        internal static bool ShouldBalHeal(IChainActivations activations, BlockHeader header, bool balHealEnabled)
            => balHealEnabled
               && activations != null
               && header != null
               && activations.ResolveAt((long)header.BlockNumber, (ulong)header.Timestamp) >= HardforkName.Amsterdam;

        internal async Task<Result> RunAsync(CancellationToken ct)
        {
            var bundle = _context.Bundle;

            var resume = await RouteResumeAsync(ct).ConfigureAwait(false);

            LogStarting(resume);

            using var bulkFlat = resume.Generating ? null : (bundle as IBulkFlatStateSinkProvider)?.CreateBulkFlatSink();
            var catchUp = CreateCatchUp(resume);

            FlatSnapSyncSink sink = null;
            SnapSyncClient client = null;
            if (!resume.Generating)
            {
                sink = new FlatSnapSyncSink(bulkFlat ?? bundle.State as ISnapFlatStateWriter, bundle.State);
                client = _context.BuildClient(sink, bulkFlat);
                ResumeRouter.EnterPhase2(_context, resume.Applied.Header, resume.Applied.Hash, new ResumePlan(resume.ResumeFrom, false), backfillOnly: false);
            }

            var phase1 = await Phase1History.StartAsync(_context, ct).ConfigureAwait(false);

            SnapSyncClient.SyncResult syncResult = null;
            if (!resume.Generating)
            {
                syncResult = await RunPhase2Async(
                        client, bulkFlat, catchUp, resume.ResumeFrom,
                        _context.CheckpointSink(() => catchUp.Applied),
                        phase1, ct)
                    .ConfigureAwait(false);

                LogPopulated(sink, catchUp);
            }

            return await Phase4Finalize.FinishAsync(
                _context, phase1,
                () => FinalizeAsync(bulkFlat, catchUp, resume.ResumeFrom, syncResult, phase1.BackfillTask != null, ct),
                () => (sink?.AccountCount ?? 0, sink?.SlotCount ?? 0, sink?.BytecodeCount ?? 0),
                ct).ConfigureAwait(false);
        }

        private void LogStarting(Snap2Resume resume)
        {
            _context.Logger.LogInformation(
                "Snap-bootstrap: snap/2 starting at applied pivot block={Block} hash=0x{Hash} generating={Generating}",
                resume.Applied.Header.BlockNumber, resume.Applied.Hash.ToHex(), resume.Generating);
        }

        private BalCatchUp CreateCatchUp(Snap2Resume resume)
        {
            var options = _context.Options;
            return BalCatchUp.Create(
                _context.Bundle, options.BlockAccessListPeers ?? new PeerPoolBlockAccessListPeerSource(_pool),
                _context.Pivots.Rolling, resume.Applied, _context.Metrics, _context.Logger, options.BlockAccessListApplier);
        }

        private void LogPopulated(FlatSnapSyncSink sink, BalCatchUp catchUp)
        {
            _context.Logger.LogInformation(
                "Snap-bootstrap: state populated — {Accounts} accounts, {Slots} storage slots, {Codes} bytecodes (snap/2, applied pivot {Block}).",
                sink.AccountCount, sink.SlotCount, sink.BytecodeCount, catchUp.Applied.Header.BlockNumber);
        }

        private async Task<Snap2Resume> RouteResumeAsync(CancellationToken ct)
        {
            var bundle = _context.Bundle;
            var logger = _context.Logger;
            var saved = bundle.Metadata.GetSnapSyncState();
            if (saved != null)
            {
                var savedPivot = await CanonicalSavedPivotAsync(bundle, saved).ConfigureAwait(false);
#pragma warning disable CS0618
                var legacyStorageDebts = bundle.Metadata.CountOpenDeferredStorageDebts() > 0;
#pragma warning restore CS0618
                var resetReason = ResetReason(legacyStorageDebts, saved, savedPivot, _context.Activations, _context.BalHealEnabled);
                if (resetReason == null)
                {
                    logger.LogInformation(
                        "snap.bootstrap.snap2_resume phase={Phase} pivot={Pivot} tasks={Tasks}",
                        saved.Phase, saved.PivotBlockNumber, saved.Tasks?.Count ?? 0);
                    return new Snap2Resume(saved, savedPivot, saved.Phase == SnapPhase.Generating);
                }

                logger.LogWarning(
                    "snap.bootstrap.snap2_reset reason={Reason} phase={Phase} saved_pivot={Pivot} — wiping snap state and starting fresh",
                    resetReason, saved.Phase, saved.PivotBlockNumber);
            }

            await bundle.ResetSnapBootstrapStateAsync(ct).ConfigureAwait(false);
            return new Snap2Resume(null, new PivotState(_context.Pivot, _context.PivotHash), false);
        }

        private static async Task<PivotState> CanonicalSavedPivotAsync(IChainStoreBundle bundle, SnapSyncState saved)
        {
            if (saved.PivotBlockHash == null || saved.PivotBlockHash.Length != 32) return null;
            var canonical = await bundle.Blocks.GetHashByNumberAsync(new BigInteger(saved.PivotBlockNumber)).ConfigureAwait(false);
            if (canonical == null || !Nethereum.Util.ByteUtil.AreEqual(canonical, saved.PivotBlockHash)) return null;
            var header = await bundle.Blocks.GetByHashAsync(saved.PivotBlockHash).ConfigureAwait(false);
            return header == null ? null : new PivotState(header, saved.PivotBlockHash);
        }

        private static string ResetReason(
            bool legacyStorageDebts, SnapSyncState saved, PivotState savedPivot, IChainActivations activations, bool balHealEnabled)
        {
            if (legacyStorageDebts)
                return "legacy_storage_debts";
            if (saved.Phase == SnapPhase.Generating)
                return savedPivot == null ? "not_canonical" : null;
            if (saved.Phase != SnapPhase.Phase2Running)
                return "phase";
            if (savedPivot == null)
                return "not_canonical";
            if (!ShouldBalHeal(activations, savedPivot.Header, balHealEnabled))
                return "pre_amsterdam";
            if (saved.Tasks == null || saved.Tasks.Count == 0)
                return "no_tasks";
            return null;
        }

        private async Task<SnapSyncClient.SyncResult> RunPhase2Async(
            SnapSyncClient client,
            IBulkFlatStateSink bulkFlat,
            BalCatchUp catchUp,
            SnapSyncState resumeFrom,
            Action<SnapSyncClient.SnapSyncCheckpoint> checkpointSink,
            Phase1History phase1,
            CancellationToken ct)
        {
            var bundle = _context.Bundle;
            var logger = _context.Logger;
            try
            {
                if (_context.PivotRefresher != null)
                    client.PivotRefresher = _context.Pivots.AsClientRefresher();
                client.PivotCatchUp = catchUp.CatchUpAsync;

                await _context.Pivots.AnchorFreshAtStartAsync(_context.Pivot, ct).ConfigureAwait(false);
                var targetRoot = await catchUp.CatchUpAsync(resumeFrom?.Tasks, ct).ConfigureAwait(false);

                var syncResult = await client.SyncStateWithCheckpointAsync(targetRoot, resumeFrom, checkpointSink, ct).ConfigureAwait(false);
                Phase3Heal.PersistCodeNeedingHeal(bundle.Metadata, syncResult.CodeHashesNeedingHeal);
                return syncResult;
            }
            catch (Exception ex)
            {
                await phase1.AbandonAsync(bulkFlat).ConfigureAwait(false);
                if (ex is SnapSyncResetRequiredException reset)
                {
                    logger.LogWarning("snap.bootstrap.snap2_reset reason={Reason} — wiping snap state after a mid-flight catch-up", reset.Reason);
                    await bundle.ResetSnapBootstrapStateAsync(ct).ConfigureAwait(false);
                }
                throw;
            }
        }

        private async Task<BlockHeader> FinalizeAsync(
            IBulkFlatStateSink bulkFlat,
            BalCatchUp catchUp,
            SnapSyncState resumeFrom,
            SnapSyncClient.SyncResult syncResult,
            bool backfillRan,
            CancellationToken ct)
        {
            var bundle = _context.Bundle;
            var applied = catchUp.Applied;
            Phase4Finalize.FlushBulk(bulkFlat, bundle);

            SaveGeneratingState(applied);

            await GenerateTrieAsync(applied, ct).ConfigureAwait(false);

            await Phase3Heal.HealCodeAsync(
                    bundle, _scheduler, applied.Header.StateRoot, syncResult?.CodeHashesNeedingHeal, _context.Logger, ct)
                .ConfigureAwait(false);

            return await Phase4Finalize.CompleteAsync(
                    _context, applied.Hash, (ulong)applied.Header.BlockNumber, backfillRan, phase3Entered: false, ct)
                .ConfigureAwait(false);
        }

        private void SaveGeneratingState(PivotState applied)
        {
            var bundle = _context.Bundle;
            bundle.Metadata.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Generating,
                PivotBlockNumber = (ulong)applied.Header.BlockNumber,
                PivotBlockHash = applied.Hash,
                HealTargetRoot = applied.Header.StateRoot,
                Tasks = Array.Empty<SnapSyncAccountTask>(),
                Counters = bundle.Metadata.GetSnapSyncState()?.Counters ?? SnapSyncCounters.Zero,
            });
        }

        private async Task GenerateTrieAsync(PivotState applied, CancellationToken ct)
        {
            var logger = _context.Logger;
            logger.LogInformation(
                "snap.generate.start pivot={Block} root=0x{Root}", applied.Header.BlockNumber, applied.Header.StateRoot.ToHex());
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var generated = await _generator.GenerateTrieFromFlatAsync(
                    applied.Header.StateRoot,
                    progress => logger.LogInformation("snap.generate {Progress}", progress),
                    ct)
                .ConfigureAwait(false);
            logger.LogInformation(
                "snap.generate.done in {Elapsed}: root=0x{Root} accounts={Accounts} slots={Slots} roots_rewritten={Rewritten} dangling_slots_deleted={Dangling}",
                sw.Elapsed, generated.Root.ToHex(), generated.AccountsScanned, generated.SlotsScanned,
                generated.AccountRootsRewritten, generated.DanglingSlotsDeleted);
        }
    }
}
