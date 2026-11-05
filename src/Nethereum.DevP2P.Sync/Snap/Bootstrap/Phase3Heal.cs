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
    internal sealed class Phase3Heal
    {
        internal sealed record HealInput(
            IReadOnlyCollection<byte[]> AccountsNeedingHeal, IReadOnlyList<byte[]> CodeSeeds, PivotState Entry);

        private readonly IChainStoreBundle _bundle;
        private readonly IFetchRequestScheduler _scheduler;
        private readonly PivotWatch _pivots;
        private readonly IPeerPool? _pool;
        private readonly SnapSyncMetrics? _metrics;
        private readonly ILogger _logger;

        internal Phase3Heal(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, PivotWatch pivots,
            IPeerPool? pool, SnapSyncMetrics? metrics, ILogger logger)
        {
            _bundle = bundle;
            _scheduler = scheduler;
            _pivots = pivots;
            _pool = pool;
            _metrics = metrics;
            _logger = logger;
        }

        internal async Task<StateSyncOutcome> AfterStreamAsync(
            SnapSyncClient.SyncResult streamed, SnapSyncState resumeFrom, TrieSnapSyncSink sink, CancellationToken ct)
        {
            if (!streamed.RootMatchesTarget && _scheduler == null)
                SnapSyncClient.ThrowIfRootMismatched(streamed, pivotCatchUpActive: false);
            PersistCodeNeedingHeal(_bundle.Metadata, streamed.CodeHashesNeedingHeal);
            var codeSeeds = NeedsHealCodeCodec.Decode(_bundle.Metadata.GetDeferredHealCodeBlob());

            var needHeal = AccountsNeedingHeal(_bundle.Metadata, streamed.AccountsNeedingHeal);
            PersistAccountsNeedingHeal(_bundle.Metadata, needHeal);
            if (_scheduler == null || (streamed.RootMatchesTarget && needHeal.Count == 0 && codeSeeds.Count == 0))
                return OutcomeWithoutHeal(streamed);

            return await RunAsync(needHeal, codeSeeds, resumeFrom, sink, ct).ConfigureAwait(false);
        }

        internal async Task<StateSyncOutcome> ResumeAsync(
            SnapSyncState resumeFrom, TrieSnapSyncSink sink, CancellationToken ct)
        {
            var codeSeeds = NeedsHealCodeCodec.Decode(_bundle.Metadata.GetDeferredHealCodeBlob());
            if (_scheduler == null)
                throw new SnapSyncClient.SnapRootMismatchException(
                    $"Snap resume at heal needs a request scheduler (persisted heal target 0x{resumeFrom!.HealTargetRoot.ToHex()})",
                    Array.Empty<SnapSyncClient.AccountNeedingHeal>(),
                    codeSeeds);
            var needHeal = AccountsNeedingHeal(_bundle.Metadata, null);
            return await RunAsync(needHeal, codeSeeds, resumeFrom, sink, ct).ConfigureAwait(false);
        }

        internal static void PersistCodeNeedingHeal(IChainMetadataStore metadata, IReadOnlyList<byte[]> codes)
        {
            if (metadata == null || codes == null || codes.Count == 0) return;
            var union = NeedsHealCodeCodec.DecodeSet(metadata.GetDeferredHealCodeBlob());
            foreach (var h in codes) if (h is { Length: 32 }) union.Add(h);
            if (union.Count == 0) return;
            metadata.SaveDeferredHealCodeBlob(NeedsHealCodeCodec.EncodeSet(union));
        }

        internal static async Task HealCodeAsync(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, byte[] stateRoot,
            IReadOnlyList<byte[]> codeNeedingHeal, ILogger logger, CancellationToken ct)
        {
            if (scheduler == null) return;
            scheduler.ClearStatelessMarks();
            PersistCodeNeedingHeal(bundle.Metadata, codeNeedingHeal);
            var seeds = NeedsHealCodeCodec.DecodeSet(bundle.Metadata.GetDeferredHealCodeBlob());
            var sweep = bundle as IFlatStateReconciler;
            if (sweep != null)
                foreach (var h in sweep.GetPersistedMissingCode()) seeds.Add(h);
            if (seeds.Count == 0) return;
            var absent = await SnapByteCodes.AbsentAsync(bundle.State, seeds).ConfigureAwait(false);
            if (absent.Count == 0)
            {
                bundle.Metadata.ClearDeferredHealCodeBlob();
                sweep?.ClearPersistedMissingCode();
                return;
            }

            var target = new PivotState(new BlockHeader { StateRoot = stateRoot }, null);
            var result = await HealerFactory.HealAtAsync(
                bundle, scheduler, logger, null, null, target, null, absent, ct).ConfigureAwait(false);
            if (!result.Matched)
                throw new InvalidOperationException(
                    $"Snap code heal could not fetch {absent.Count} contract code(s) before pivot commit.");
            bundle.Metadata.ClearDeferredHealCodeBlob();
            sweep?.ClearPersistedMissingCode();
        }

        internal static IReadOnlyList<SnapSyncClient.AccountNeedingHeal> AccountsNeedingHeal(
            IChainMetadataStore metadata, IEnumerable<SnapSyncClient.AccountNeedingHeal> fresh)
            => NeedsHealAccountsCodec.Union(metadata.GetDeferredHealAccountsBlob(), fresh);

        internal static void PersistAccountsNeedingHeal(
            IChainMetadataStore metadata, IReadOnlyList<SnapSyncClient.AccountNeedingHeal> accounts)
        {
            if (accounts == null || accounts.Count == 0) return;
            var existing = metadata.GetDeferredHealAccountsBlob();
            var union = NeedsHealAccountsCodec.Union(existing, accounts);
            var encoded = NeedsHealAccountsCodec.Encode(union);
            if (encoded.Length == (existing?.Length ?? 0)) return;
            metadata.SaveDeferredHealAccountsBlob(encoded);
        }

        internal static IReadOnlyList<SnapSyncClient.AccountNeedingHeal> ResolveAccounts(
            IReadOnlyList<SnapSyncClient.AccountNeedingHeal> freshFromException,
            byte[] persistedBlob,
            ILogger logger = null)
        {
            if (freshFromException is { Count: > 0 }) return freshFromException;
            if (persistedBlob == null) return null;
            var decoded = NeedsHealAccountsCodec.Decode(persistedBlob);
            if (decoded.Count == 0)
            {
                logger?.LogWarning(
                    "snap.phase3.needsheal.corrupt persisted deferred-heal-accounts blob ({Length} bytes) " +
                    "did not decode to any entries (not a multiple of the 64-byte entry size) — the deferred " +
                    "storage heal for this account set is being skipped this attempt due to metadata corruption.",
                    persistedBlob.Length);
            }
            return decoded;
        }

        private StateSyncOutcome OutcomeWithoutHeal(SnapSyncClient.SyncResult streamed)
        {
            var streamedAt = _pivots.Current;
            var converged = Nethereum.Util.ByteUtil.AreEqual(streamedAt.Header.StateRoot, streamed.FinalTargetRoot)
                ? streamedAt
                : null;
            return new StateSyncOutcome(streamed, HealEntered: false, converged);
        }


        private async Task<StateSyncOutcome> RunAsync(
            IReadOnlyList<SnapSyncClient.AccountNeedingHeal> needHeal,
            IReadOnlyList<byte[]> codeSeeds,
            SnapSyncState resumeFrom,
            TrieSnapSyncSink sink,
            CancellationToken ct)
        {
            _logger.LogWarning("Snap leaf-stream root mismatch — entering heal phase");
            var entry = _pivots.Current;
            ResumeRouter.SaveHealEntryState(_bundle, resumeFrom, entry);
            _logger.LogInformation(
                "snap.phase.transition from=Phase2 to=Phase3 pivot={Pivot} heal_target=0x{Root}",
                entry.Header.BlockNumber, entry.Header.StateRoot.ToHex());
            if (needHeal.Count > 0 || codeSeeds.Count > 0)
                _logger.LogInformation(
                    "snap.phase3.needheal accounts={Accounts} codes={Codes} heal_target=0x{Root} — the heal descends along their account paths at the live pivot and fetches the code",
                    needHeal.Count, codeSeeds.Count, entry.Header.StateRoot.ToHex());

            var (_, converged) = await HealUntilConvergedAsync(
                new HealInput(needHeal.Select(a => a.AccountHash).ToList(), codeSeeds, entry), ct).ConfigureAwait(false);
            _bundle.Metadata.ClearDeferredHealAccountsBlob();
            _bundle.Metadata.ClearDeferredHealCodeBlob();
            _logger.LogInformation(
                "snap.phase3.converged block={Block} root=0x{Root} rolling_pivot_block={RollingBlock} rolling_pivot_root=0x{RollingRoot} — " +
                "certify and Phase 4 start from the converged root",
                converged.Header.BlockNumber, converged.Header.StateRoot.ToHex(),
                _pivots.Current.Header.BlockNumber, _pivots.Current.Header.StateRoot.ToHex());

            var result = new SnapSyncClient.SyncResult
            {
                Sink = sink,
                ComputedRoot = converged.Header.StateRoot,
                RootMatchesTarget = true,
                AccountCount = sink.AccountCount,
                FinalTargetRoot = converged.Header.StateRoot,
                CodeHashesNeedingHeal = Array.Empty<byte[]>(),
            };
            return new StateSyncOutcome(result, HealEntered: true, converged);
        }

        internal async Task<(TrieHealer.HealResult Result, PivotState Converged)> HealUntilConvergedAsync(
            HealInput input, CancellationToken ct)
        {
            long cumulativeHealNodes = 0;
            var healCyclePivot = input.Entry;
            var stalls = new HealStallLog(_logger);
            for (int healCycle = 1; ; healCycle++)
            {
                using var peerJoins = new PeerJoinWatch(_pool, _scheduler);
                var healResult = await HealerFactory.HealAtAsync(
                    _bundle, _scheduler, _logger, _metrics, _pivots, healCyclePivot,
                    input.AccountsNeedingHeal, input.CodeSeeds, ct).ConfigureAwait(false);
                cumulativeHealNodes += healResult.TotalNodesFetched;
                if (healResult.Matched)
                    return (healResult with { TotalNodesFetched = (int)Math.Min(cumulativeHealNodes, int.MaxValue) }, healCyclePivot);

                var previousTarget = healCyclePivot;
                healCyclePivot = await NextHealCyclePivotAsync(healResult, previousTarget, peerJoins, ct)
                    .ConfigureAwait(false);
                _pivots.ResetSchedulerIfMoved(previousTarget.Header.StateRoot, healCyclePivot);

                stalls.Observe(healCycle, cumulativeHealNodes);
                ResumeRouter.SaveHealEntryState(_bundle, null, healCyclePivot);
                _logger.LogWarning(
                    "snap.phase3.recycle cycle={Cycle} not converged (cumulative {Nodes} nodes; " +
                    "this cycle: pruned={Pruned} absent={Absent} stale={Stale}) — " +
                    "re-pivoting to block {Block} root 0x{New} and continuing heal (persisted nodes retained)",
                    healCycle, cumulativeHealNodes,
                    healResult.Pruned, healResult.FetchedAbsent, healResult.FetchedStale,
                    healCyclePivot.Header.BlockNumber, healCyclePivot.Header.StateRoot.ToHex());
            }
        }

        private async Task<PivotState> NextHealCyclePivotAsync(
            TrieHealer.HealResult healResult, PivotState previousTarget, PeerJoinWatch peerJoins, CancellationToken ct)
        {
            await _pivots.PollAsync(ct).ConfigureAwait(false);
            if (PivotWatch.Stalled(healResult, previousTarget, _pivots.Current))
            {
                await _pivots.WaitAfterNoProgressAsync(previousTarget.Header.StateRoot, peerJoins, _metrics, ct)
                    .ConfigureAwait(false);
            }
            return _pivots.Current;
        }
    }
}
