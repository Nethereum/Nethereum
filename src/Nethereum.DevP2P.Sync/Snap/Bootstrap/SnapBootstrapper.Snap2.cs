using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    public static partial class SnapBootstrapper
    {
        private sealed record Snap2Resume(SnapSyncState ResumeFrom, PivotState Applied, bool Generating);

        private static async Task<Result> RunSnap2Async(
            IChainStoreBundle bundle,
            ISnapPeer peer,
            BlockHeader bootPivot,
            byte[] bootPivotHash,
            RollingPivot rollingPivot,
            SnapRunOptions options,
            ILogger logger,
            CancellationToken ct)
        {
            var scheduler = options.Scheduler
                ?? throw new InvalidOperationException("snap/2 needs a request scheduler.");
            var pool = options.Pool
                ?? throw new InvalidOperationException("snap/2 needs a peer pool to fetch block access lists.");
            var generator = bundle as IFlatStateTrieGenerator
                ?? throw new InvalidOperationException($"snap/2 needs a bundle that generates the trie from flat state; {bundle.GetType().Name} does not.");
            if (!options.ExternalHeaderFollow)
                throw new InvalidOperationException("snap/2 reads gap headers from the local canonical chain and needs external header follow.");

            var resume = await RouteSnap2ResumeAsync(bundle, bootPivot, bootPivotHash, options.Activations, logger, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Snap-bootstrap: snap/2 starting at applied pivot block={Block} hash=0x{Hash} generating={Generating}",
                resume.Applied.Header.BlockNumber, resume.Applied.Hash.ToHex(), resume.Generating);

            using var bulkFlat = resume.Generating ? null : (bundle as IBulkFlatStateSinkProvider)?.CreateBulkFlatSink();
            var catchUp = BalCatchUp.Create(
                bundle, options.BlockAccessListPeers ?? new PeerPoolBlockAccessListPeerSource(pool),
                rollingPivot, resume.Applied, options.Metrics, logger, options.BlockAccessListApplier);

            FlatSnapSyncSink sink = null;
            SnapSyncClient client = null;
            if (!resume.Generating)
            {
                sink = new FlatSnapSyncSink(bulkFlat ?? bundle.State as ISnapFlatStateWriter, bundle.State);
                client = BuildSnapClient(peer, sink, logger, options.Metrics, options.RootRefreshIntervalMs, scheduler, bundle, bulkFlat, options.AccountConcurrency, options.LargeContractConcurrency);
                StampPhase2Entry(bundle, resume.Applied.Header, resume.Applied.Hash, resume.ResumeFrom, skipPhase2: false, backfillOnly: false, options.Metrics, logger);
            }

            var phase1 = await StartPhase1Async(bundle, options, rollingPivot, logger, ct).ConfigureAwait(false);

            SnapSyncClient.SyncResult syncResult = null;
            if (!resume.Generating)
            {
                syncResult = await RunSnap2Phase2Async(
                        bundle, client, bulkFlat, catchUp, bootPivot, resume.ResumeFrom,
                        BuildCheckpointSink(bundle, () => catchUp.Applied),
                        rollingPivot, phase1, options, logger, ct)
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "Snap-bootstrap: state populated — {Accounts} accounts, {Slots} storage slots, {Codes} bytecodes (snap/2, applied pivot {Block}).",
                    sink.AccountCount, sink.SlotCount, sink.BytecodeCount, catchUp.Applied.Header.BlockNumber);
            }

            return await FinishWithHistoryHandOffAsync(
                bundle, phase1, scheduler, pool, options.Activations, options.ExternalHeaderFollow, rollingPivot,
                () => FinishSnap2Async(bundle, bulkFlat, generator, catchUp, scheduler, resume.ResumeFrom, syncResult, phase1.BackfillTask != null, logger, ct),
                () => (sink?.AccountCount ?? 0, sink?.SlotCount ?? 0, sink?.BytecodeCount ?? 0),
                logger, ct).ConfigureAwait(false);
        }

        private static async Task<Snap2Resume> RouteSnap2ResumeAsync(
            IChainStoreBundle bundle, BlockHeader bootPivot, byte[] bootPivotHash, IChainActivations activations,
            ILogger logger, CancellationToken ct)
        {
            var saved = bundle.Metadata.GetSnapSyncState();
            if (saved != null)
            {
                var savedPivot = await CanonicalSavedPivotAsync(bundle, saved).ConfigureAwait(false);
                var resetReason = Snap2ResetReason(saved, savedPivot, activations);
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
            return new Snap2Resume(null, new PivotState(bootPivot, bootPivotHash), false);
        }

        private static async Task<PivotState> CanonicalSavedPivotAsync(IChainStoreBundle bundle, SnapSyncState saved)
        {
            if (saved.PivotBlockHash == null || saved.PivotBlockHash.Length != 32) return null;
            var canonical = await bundle.Blocks.GetHashByNumberAsync(new BigInteger(saved.PivotBlockNumber)).ConfigureAwait(false);
            if (canonical == null || !Nethereum.Util.ByteUtil.AreEqual(canonical, saved.PivotBlockHash)) return null;
            var header = await bundle.Blocks.GetByHashAsync(saved.PivotBlockHash).ConfigureAwait(false);
            return header == null ? null : new PivotState(header, saved.PivotBlockHash);
        }

        private static string Snap2ResetReason(SnapSyncState saved, PivotState savedPivot, IChainActivations activations)
        {
            if (saved.Phase == SnapPhase.Generating)
                return savedPivot == null ? "not_canonical" : null;
            if (saved.Phase != SnapPhase.Phase2Running)
                return "phase";
            if (savedPivot == null)
                return "not_canonical";
            if (!ShouldBalHeal(activations, savedPivot.Header, balHealEnabled: true))
                return "pre_amsterdam";
            if (saved.Tasks == null || saved.Tasks.Count == 0)
                return "no_tasks";
            return null;
        }

        private static async Task<SnapSyncClient.SyncResult> RunSnap2Phase2Async(
            IChainStoreBundle bundle,
            SnapSyncClient client,
            IBulkFlatStateSink bulkFlat,
            BalCatchUp catchUp,
            BlockHeader bootPivot,
            SnapSyncState resumeFrom,
            Action<SnapSyncClient.SnapSyncCheckpoint> checkpointSink,
            RollingPivot rollingPivot,
            Phase1Start phase1,
            SnapRunOptions options,
            ILogger logger,
            CancellationToken ct)
        {
            try
            {
                if (options.PivotRefresher != null)
                    client.PivotRefresher = BuildClientPivotRefresher(options.PivotRefresher, rollingPivot, logger);
                client.PivotCatchUp = catchUp.CatchUpAsync;

                await AnchorFreshPivotAtStartAsync(options.PivotRefresher, rollingPivot, bootPivot, logger, ct).ConfigureAwait(false);
                var targetRoot = await catchUp.CatchUpAsync(resumeFrom?.Tasks, ct).ConfigureAwait(false);

                var syncResult = await client.SyncStateWithCheckpointAsync(targetRoot, resumeFrom, checkpointSink, ct).ConfigureAwait(false);
                PersistDeferredHealCode(bundle.Metadata, syncResult.CodeHashesNeedingHeal);
                return syncResult;
            }
            catch (Exception ex)
            {
                await AbandonPhase2AndStopBackfillAsync(bulkFlat, phase1, logger).ConfigureAwait(false);
                if (ex is SnapSyncResetRequiredException reset)
                {
                    logger.LogWarning("snap.bootstrap.snap2_reset reason={Reason} — wiping snap state after a mid-flight catch-up", reset.Reason);
                    await bundle.ResetSnapBootstrapStateAsync(ct).ConfigureAwait(false);
                }
                throw;
            }
        }

        private static async Task<BlockHeader> FinishSnap2Async(
            IChainStoreBundle bundle,
            IBulkFlatStateSink bulkFlat,
            IFlatStateTrieGenerator generator,
            BalCatchUp catchUp,
            IFetchRequestScheduler scheduler,
            SnapSyncState resumeFrom,
            SnapSyncClient.SyncResult syncResult,
            bool backfillRan,
            ILogger logger,
            CancellationToken ct)
        {
            var applied = catchUp.Applied;
            bulkFlat?.Flush();
            (bundle as IBulkDurabilityBoundary)?.CheckpointBulk();

            ThrowIfStorageCompletenessGateFails(bundle);
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

            logger.LogInformation(
                "snap.generate.start pivot={Block} root=0x{Root}", applied.Header.BlockNumber, applied.Header.StateRoot.ToHex());
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var generated = await generator.GenerateTrieFromFlatAsync(
                    applied.Header.StateRoot,
                    progress => logger.LogInformation("snap.generate {Progress}", progress),
                    ct)
                .ConfigureAwait(false);
            logger.LogInformation(
                "snap.generate.done in {Elapsed}: root=0x{Root} accounts={Accounts} slots={Slots} roots_rewritten={Rewritten} dangling_slots_deleted={Dangling}",
                sw.Elapsed, generated.Root.ToHex(), generated.AccountsScanned, generated.SlotsScanned,
                generated.AccountRootsRewritten, generated.DanglingSlotsDeleted);

            await FetchMissingBytecodeAsync(bundle, scheduler, resumeFrom, applied.Header.StateRoot, logger, ct,
                syncResult?.CodeHashesNeedingHeal).ConfigureAwait(false);

            return await PersistPivotAndCheckpointAsync(
                    bundle, applied.Hash, (ulong)applied.Header.BlockNumber, backfillRan, healPhaseEntered: false, logger, ct)
                .ConfigureAwait(false);
        }
    }
}
