using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using PivotState = Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapBootstrapper.PivotState;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed class FlatCertifier
    {
        internal static async Task ReconcileAndCertifyAsync(
            IFlatStateReconciler flatReconciler, byte[] stateRoot, ulong pivotBlockNumber,
            ILogger logger, CancellationToken ct)
        {
            logger.LogInformation(
                "snap.flat.reconcile starting root=0x{Root} pivot={Block}", stateRoot.ToHex(), pivotBlockNumber);
            var reconcileSw = System.Diagnostics.Stopwatch.StartNew();
            var reconcile = await flatReconciler.ReconcileFlatStateAsync(
                    stateRoot,
                    msg => logger.LogInformation("snap.flat.reconcile {Progress}", msg),
                    ct)
                .ConfigureAwait(false);
            logger.LogInformation(
                "snap.flat.reconcile done in {Elapsed}: accounts={Accounts} slots={Slots} " +
                "ghost_accounts={GhostAccounts} ghost_slots={GhostSlots} added={AddedAccounts}/{AddedSlots} patched={PatchedAccounts}/{PatchedSlots} " +
                "ranges_walked={RangesWalked} leaves_walked={LeavesWalked} " +
                "trie_missing={TrieNodesMissing} trie_differ={TrieNodesDiffering}",
                reconcileSw.Elapsed, reconcile.AccountsScanned, reconcile.SlotsScanned,
                reconcile.GhostAccountsDeleted, reconcile.GhostSlotsDeleted,
                reconcile.AccountsAdded, reconcile.SlotsAdded,
                reconcile.AccountsPatched, reconcile.SlotsPatched,
                reconcile.RangesWalked, reconcile.LeavesWalked,
                reconcile.TrieNodesMissing, reconcile.TrieNodesDiffering);
        }

        private const int MaxCertifyAttempts = 4;

        private readonly IChainStoreBundle _bundle;
        private readonly IFlatStateReconciler? _reconciler;
        private readonly IFetchRequestScheduler _scheduler;
        private readonly PivotWatch _pivots;
        private readonly Phase3Heal _healer;
        private readonly ILogger _logger;

        public FlatCertifier(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, PivotWatch pivots,
            Phase3Heal healer, ILogger logger)
        {
            _bundle = bundle;
            _reconciler = bundle as IFlatStateReconciler;
            _scheduler = scheduler;
            _pivots = pivots;
            _healer = healer;
            _logger = logger;
        }

        public async Task<PivotState> RunAsync(PivotState? converged, CancellationToken ct)
        {
            DiscardPersistedDamage();
            var storedRootPivot = await PivotWhoseRootIsStoredAsync(converged, ct).ConfigureAwait(false);
            if (_reconciler == null) return SkipWithoutReconciler(storedRootPivot);

            if (_bundle.Metadata.GetSnapSyncState()?.Phase != SnapPhase.Phase3Running)
                ResumeRouter.SaveHealEntryState(_bundle, null, storedRootPivot);
            return await CertifyAsync(_reconciler, storedRootPivot, ct).ConfigureAwait(false);
        }

        private PivotState SkipWithoutReconciler(PivotState start)
        {
            _logger.LogWarning(
                "snap.flat.reconcile SKIPPED: bundle {BundleType} does not expose IFlatStateReconciler — " +
                "flat rows may diverge from the healed trie (rolling-pivot ghosts) and execution reads flat.",
                _bundle.GetType().Name);
            return start;
        }

        private void DiscardPersistedDamage()
        {
            var pending = _reconciler?.GetPersistedDamage();
            if (pending == null || pending.Count == 0) return;
            _logger.LogWarning(
                "snap.flat.certify.inventory_discard of {Count} damaged subtree(s) persisted by a prior run — " +
                "certify re-derives every hole: proven owners are regenerated locally, the rest are healed by path", pending.Count);
            _reconciler!.ClearPersistedDamage();
        }

        private async Task<PivotState> PivotWhoseRootIsStoredAsync(PivotState? convergedPivot, CancellationToken ct)
        {
            var converged = convergedPivot ?? _pivots.Current;
            if (StoresAccountRoot(converged.Header.StateRoot)) return converged;
            if (_scheduler == null)
                throw new InvalidOperationException(
                    $"snap.flat.certify: the stored account root at [] is not 0x{converged.Header.StateRoot.ToHex()} " +
                    "and no scheduler is available to heal forward — refusing to certify or commit a root the trie does not hold.");
            _logger.LogWarning(
                "snap.flat.certify.guard the stored account root at [] is not the converged root 0x{Root} (block {Block}) — " +
                "healing forward to a fresh pivot instead of starting a certify pass",
                converged.Header.StateRoot.ToHex(), converged.Header.BlockNumber);
            return await HandToHealAsync(new HealDamage(), Array.Empty<byte[]>(), ct).ConfigureAwait(false);
        }

        private bool StoresAccountRoot(byte[] root)
            => Nethereum.Util.ByteUtil.AreEqual(root, DefaultValues.EMPTY_TRIE_HASH) || HealerFactory.CreateSink(_bundle).HasRoot(root);

        private async Task<PivotState> CertifyAsync(IFlatStateReconciler reconciler, PivotState start, CancellationToken ct)
        {
            var certifyAt = start;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await ReconcileAndCertifyAsync(
                            reconciler, certifyAt.Header.StateRoot, (ulong)certifyAt.Header.BlockNumber, _logger, ct)
                        .ConfigureAwait(false);
                    var missingCode = reconciler.GetPersistedMissingCode();
                    if (missingCode.Count == 0) return certifyAt;
                    if (_scheduler == null || attempt >= MaxCertifyAttempts)
                        throw new InvalidOperationException(
                            $"snap.flat.certify: {missingCode.Count} contract code blob(s) named by the sweep are still missing " +
                            "and cannot be healed - refusing to commit the pivot.");
                    _logger.LogWarning(
                        "snap.flat.certify.code attempt={Attempt}/{Max}: {Count} contract code blob(s) missing — healing them through the heal cycle",
                        attempt, MaxCertifyAttempts, missingCode.Count);
                    certifyAt = await HandToHealAsync(new HealDamage(), missingCode, ct).ConfigureAwait(false);
                    reconciler.ClearPersistedMissingCode();
                }
                catch (FlatReconcileUnresolvableNodeException hole) when (_scheduler != null && attempt < MaxCertifyAttempts)
                {
                    _logger.LogWarning(
                        "snap.flat.reheal attempt={Attempt}/{Max}: {Count} damaged subtree(s) missing_nodes={MissingNodes} — " +
                        "deleting their ancestor chains and healing at the current pivot. {Message}",
                        attempt, MaxCertifyAttempts, hole.Subtrees.Count, hole.MissingNodes.Count, hole.Message);
                    certifyAt = await HandToHealAsync(HealDamage.From(hole), Array.Empty<byte[]>(), ct).ConfigureAwait(false);
                }
            }
        }

        private async Task<PivotState> HandToHealAsync(HealDamage damage, IReadOnlyList<byte[]> codeSeeds, CancellationToken ct)
        {
            _scheduler.ClearStatelessMarks();
            DeleteAncestorChains(damage);
            var certifiedRoot = _pivots.Current.Header.StateRoot;
            await _pivots.PollAsync(ct).ConfigureAwait(false);
            _pivots.ResetSchedulerIfMoved(certifiedRoot, _pivots.Current);
            var owners = damage.StorageNodes.Select(n => n.Owner).Concat(damage.OwnersToWipe)
                .Distinct(Nethereum.Util.ByteArrayComparer.Current).ToList();
            var (_, healed) = await _healer.HealUntilConvergedAsync(
                new Phase3Heal.HealInput(owners, codeSeeds, _pivots.Current), ct).ConfigureAwait(false);
            _reconciler?.ClearPersistedDamage();
            return healed;
        }

        private void DeleteAncestorChains(HealDamage damage)
        {
            var sink = HealerFactory.CreateSink(_bundle);
            damage.DeleteAncestorChains(sink);
            _logger.LogInformation(
                "snap.flat.reheal chains account_paths={AccountPaths} storage_paths={StoragePaths} owner_wide={OwnerWide}",
                damage.AccountPaths.Count, damage.StorageNodes.Count, damage.OwnersToWipe.Count);
        }
    }
}
