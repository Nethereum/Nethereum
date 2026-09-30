using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ChainNode.Hosting
{
    public static class ChainNodeMaintenanceRunner
    {
        public static bool AnyRequested(ChainNodeMaintenanceConfig config) =>
            config.WipeState || config.CompactAll || config.RebuildStateFromFlat || config.VerifyFlat;

        public static async Task RunRequestedOpsAsync(
            IChainStoreBundle bundle, ChainNodeMaintenanceConfig config, ILogger logger, CancellationToken ct)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (config == null) throw new ArgumentNullException(nameof(config));

            if (config.VerifyFlat)
            {
                await RunVerifyFlatAsync(bundle, config, logger, ct).ConfigureAwait(false);
                return;
            }

            await WipeStateIfRequestedAsync(bundle, config, logger, ct).ConfigureAwait(false);
            await RunCompactAllIfRequestedAsync(bundle, config, logger, ct).ConfigureAwait(false);
            await RunRebuildStateFromFlatIfRequestedAsync(bundle, config, logger, ct).ConfigureAwait(false);
        }

        public static async Task<int> RunAndReportExitCodeAsync(
            IChainStoreBundle bundle, ChainNodeMaintenanceConfig config, ILogger logger, CancellationToken ct)
        {
            try
            {
                await RunRequestedOpsAsync(bundle, config, logger, ct).ConfigureAwait(false);
                logger?.LogInformation("Maintenance: requested operation(s) completed; exiting with code 0.");
                return 0;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Maintenance: requested operation(s) failed; exiting with a non-zero code.");
                return 1;
            }
        }

        private static async Task WipeStateIfRequestedAsync(
            IChainStoreBundle bundle, ChainNodeMaintenanceConfig config, ILogger logger, CancellationToken ct)
        {
            if (!config.WipeState) return;

            logger?.LogWarning(
                "WipeState set — wiping state / trie / state-history CFs and SnapSyncState metadata; " +
                "receipts, logs and Phase 1 cursors are preserved.");
            await bundle.ResetSnapBootstrapStateAsync(ct).ConfigureAwait(false);
            logger?.LogWarning(
                "Snap-bootstrap state wipe complete; Phase 1 archive untouched. Snap bootstrap will re-stream from a fresh pivot.");
        }

        private static async Task RunCompactAllIfRequestedAsync(
            IChainStoreBundle bundle, ChainNodeMaintenanceConfig config, ILogger logger, CancellationToken ct)
        {
            if (!config.CompactAll) return;

            if (bundle is not IStateCompaction compaction)
                throw new InvalidOperationException(
                    "CompactAll requested but the active storage backend does not implement IStateCompaction " +
                    "(compaction only applies to a RocksDB-backed store). Remove the flag or switch to RocksDB storage.");

            logger?.LogWarning("CompactAll set — running a full store compaction before sync; this can take a while.");
            await compaction.CompactAllAsync(
                msg => logger?.LogInformation("compact.all {Progress}", msg), ct).ConfigureAwait(false);
            logger?.LogWarning("CompactAll complete.");
        }

        private static async Task RunRebuildStateFromFlatIfRequestedAsync(
            IChainStoreBundle bundle, ChainNodeMaintenanceConfig config, ILogger logger, CancellationToken ct)
        {
            if (!config.RebuildStateFromFlat) return;

            if (bundle is not IFlatStateReconciler reconciler)
                throw new InvalidOperationException(
                    "RebuildStateFromFlat requested but the active storage backend does not implement " +
                    "IFlatStateReconciler (only applies to a RocksDB-backed flat-state store). Remove the flag or switch to RocksDB storage.");

            var headBlock = bundle.Metadata.GetLastBlock();
            if (headBlock == 0)
            {
                logger?.LogWarning(
                    "RebuildStateFromFlat set but no committed state (LastBlock=0) — nothing to repair; skipping.");
                return;
            }

            var headHeader = await bundle.Blocks.GetByNumberAsync(headBlock).ConfigureAwait(false);
            if (headHeader?.StateRoot == null || headHeader.StateRoot.Length != 32)
                throw new InvalidOperationException(
                    $"RebuildStateFromFlat: committed head {headBlock} has no header state root in the store — cannot verify the repair. Aborting.");
            var expectedRoot = headHeader.StateRoot;

            if (!bundle.StateTrieNodes.ContainsKey(expectedRoot))
                throw new InvalidOperationException(
                    $"RebuildStateFromFlat: the committed head {headBlock} account root 0x{expectedRoot.ToHex()} " +
                    "is not present in the trie store — nothing to reconcile flat against. Did you also set " +
                    "WipeState? Run a state resync instead. Refusing.");

            logger?.LogWarning(
                "RebuildStateFromFlat set — reconciling FLAT to the canonical TRIE at committed head {Head} " +
                "(0x{Root}); the trie is the source of truth. One-shot full-state pass; clear the flag after.",
                headBlock, expectedRoot.ToHex());
            var reconcileSw = Stopwatch.StartNew();
            var reconcile = await reconciler.ReconcileFlatStateAsync(
                    expectedRoot,
                    msg => logger?.LogInformation("state.reconcile {Progress} elapsed={Elapsed}", msg, reconcileSw.Elapsed),
                    ct)
                .ConfigureAwait(false);
            logger?.LogWarning(
                "RebuildStateFromFlat reconcile done in {Elapsed}: accounts patched={AP} added={AA} ghosts={AG}, " +
                "slots patched={SP} added={SA} ghosts={SG}.",
                reconcileSw.Elapsed, reconcile.AccountsPatched, reconcile.AccountsAdded, reconcile.GhostAccountsDeleted,
                reconcile.SlotsPatched, reconcile.SlotsAdded, reconcile.GhostSlotsDeleted);

            var verifySw = Stopwatch.StartNew();
            var verify = await reconciler.VerifyFlatStateAsync(
                    expectedRoot,
                    msg => logger?.LogInformation("state.reconcile.verify {Progress} elapsed={Elapsed}", msg, verifySw.Elapsed),
                    ct)
                .ConfigureAwait(false);
            if (verify.TotalRepairs != 0)
                throw new InvalidOperationException(
                    $"RebuildStateFromFlat: post-reconcile verify still found {verify.TotalRepairs} flat/trie diffs at head " +
                    $"{headBlock} (0x{expectedRoot.ToHex()}) — refusing to go live.");
            logger?.LogWarning(
                "RebuildStateFromFlat VERIFIED in {Elapsed}: flat == trie at head {Head} (0x{Root}), zero diffs. " +
                "Clear the flag before the next restart.",
                verifySw.Elapsed, headBlock, expectedRoot.ToHex());
        }

        private static async Task RunVerifyFlatAsync(
            IChainStoreBundle bundle, ChainNodeMaintenanceConfig config, ILogger logger, CancellationToken ct)
        {
            if (bundle is not IFlatStateReconciler reconciler)
                throw new InvalidOperationException(
                    "VerifyFlat requested but the active storage backend does not implement IFlatStateReconciler " +
                    "(only applies to a RocksDB-backed flat-state store). Remove the flag or switch to RocksDB storage.");

            var headBlock = bundle.Metadata.GetLastBlock();
            if (headBlock == 0)
            {
                logger?.LogWarning("VerifyFlat set but no committed state (LastBlock=0) — nothing to verify; stopping.");
                return;
            }

            var headHeader = await bundle.Blocks.GetByNumberAsync(headBlock).ConfigureAwait(false);
            if (headHeader?.StateRoot == null || headHeader.StateRoot.Length != 32)
                throw new InvalidOperationException(
                    $"VerifyFlat: committed head {headBlock} has no header state root in the store — cannot verify. Aborting.");
            var expectedRoot = headHeader.StateRoot;

            if (!bundle.StateTrieNodes.ContainsKey(expectedRoot))
                throw new InvalidOperationException(
                    $"VerifyFlat: the committed head {headBlock} account root 0x{expectedRoot.ToHex()} " +
                    "is not present in the trie store — nothing to verify flat against. Refusing.");

            var sampleCap = config.VerifyFlatSampleAccountsPerShard;
            logger?.LogWarning(
                "VerifyFlat set — write-free verify of FLAT against the canonical TRIE at committed head {Head} " +
                "(0x{Root}); sample={Sample}. One-shot diagnostic.",
                headBlock, expectedRoot.ToHex(), sampleCap > 0 ? sampleCap.ToString() : "full");
            var verifySw = Stopwatch.StartNew();
            var verify = await reconciler.VerifyFlatStateAsync(
                    expectedRoot,
                    msg => logger?.LogInformation("state.verify {Progress} elapsed={Elapsed}", msg, verifySw.Elapsed),
                    ct,
                    sampleCap)
                .ConfigureAwait(false);
            logger?.LogWarning(
                "VerifyFlat done in {Elapsed} at head {Head} (0x{Root}): " +
                "accounts scanned={AccountsScanned} patched={AccountsPatched} added={AccountsAdded} ghosts={AccountGhosts}; " +
                "slots scanned={SlotsScanned} patched={SlotsPatched} added={SlotsAdded} ghosts={SlotGhosts}; " +
                "total={Total}; sample={Sample}.",
                verifySw.Elapsed, headBlock, expectedRoot.ToHex(),
                verify.AccountsScanned, verify.AccountsPatched, verify.AccountsAdded, verify.GhostAccountsDeleted,
                verify.SlotsScanned, verify.SlotsPatched, verify.SlotsAdded, verify.GhostSlotsDeleted,
                verify.TotalRepairs, sampleCap > 0 ? sampleCap.ToString() : "full");

            if (verify.TotalRepairs != 0)
                throw new InvalidOperationException(
                    $"VerifyFlat found {verify.TotalRepairs} flat/trie diffs between the flat store and the canonical " +
                    $"trie at head {headBlock} (0x{expectedRoot.ToHex()}).");
        }
    }
}
