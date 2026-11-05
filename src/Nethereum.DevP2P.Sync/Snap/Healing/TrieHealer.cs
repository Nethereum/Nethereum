using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Healing
{
    public sealed class TrieHealer
    {
        public static bool ShouldRotateHealPivot(
            bool stalled, bool rootChanged, ulong currentPivotBlock, ulong newPivotBlock, ulong staleDistanceBlocks)
        {
            if (!rootChanged) return false;
            if (stalled) return true;
            return newPivotBlock >= currentPivotBlock + staleDistanceBlocks;
        }

        private const int MaxRounds = 100_000;

        public TimeSpan FetchFailureBackoff { get; set; } = TimeSpan.FromMilliseconds(500);
        public TimeSpan NoPeerFailureBackoff { get; set; } = TimeSpan.FromSeconds(2);

        private readonly HealStats _stats = new();

        private readonly IFetchRequestScheduler _scheduler;
        private readonly Nethereum.CoreChain.Storage.IHealNodeSink _sink;
        private readonly ITrieNodeStore _nodeStore;
        private readonly ILogger _logger;
        private readonly SnapSyncMetrics? _metrics;
        private readonly Nethereum.CoreChain.Storage.ISnapFlatStateWriter? _flatWriter;
        private readonly Nethereum.CoreChain.Storage.IStateStore? _codeStore;

        public TrieHealer(IFetchRequestScheduler scheduler, Nethereum.CoreChain.Storage.IHealNodeSink sink,
            ITrieNodeStore nodeStore, ILogger? logger = null,
            SnapSyncMetrics? metrics = null, Nethereum.CoreChain.Storage.ISnapFlatStateWriter? flatWriter = null,
            Nethereum.CoreChain.Storage.IStateStore? codeStore = null)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _nodeStore = nodeStore ?? throw new ArgumentNullException(nameof(nodeStore));
            _logger = logger ?? NullLogger.Instance;
            _metrics = metrics;
            _flatWriter = flatWriter;
            _codeStore = codeStore;
        }

        public TrieHealer(IFetchRequestScheduler scheduler, INodeBlobStore storage, ILogger? logger = null,
            SnapSyncMetrics? metrics = null, Nethereum.CoreChain.Storage.ISnapFlatStateWriter? flatWriter = null,
            Nethereum.CoreChain.Storage.IStateStore? codeStore = null)
            : this(scheduler,
                   new Nethereum.CoreChain.Storage.HashHealNodeSink(storage ?? throw new ArgumentNullException(nameof(storage))),
                   ContentAddressedNodeStore.Wrap(storage), logger, metrics, flatWriter, codeStore)
        {
        }

        public Func<bool, CancellationToken, Task<(byte[] Root, ulong Block)?>>? PivotRefresher { get; set; }

        internal IReadOnlyCollection<byte[]>? AccountsNeedingHeal { get; set; }

        public readonly record struct HealResult(
            bool Matched,
            int TotalNodesFetched,
            byte[] FinalTargetRoot,
            byte[] ComputedRoot,
            long Pruned = 0,
            long FetchedAbsent = 0,
            long FetchedStale = 0,
            bool NeedsRetarget = false,
            byte[] RetargetRoot = null,
            ulong RetargetBlock = 0,
            IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> RetargetSeeds = null);

        [NethereumDocExample(DocSection.DevP2P, "devp2p-sync", "TrieHealer.HealAsync — Phase-3 heal")]
        public async Task<HealResult> HealAsync(
            byte[] targetRoot,
            IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seedStorageHeal = null,
            IReadOnlyList<byte[]> seedCodeHeal = null,
            ulong pivotBlock = 0,
            bool wipeSeedsFirst = false,
            CancellationToken ct = default)
        {
            if (targetRoot == null || targetRoot.Length != 32)
                throw new ArgumentException("targetRoot must be 32 bytes", nameof(targetRoot));

            var session = new HealSession(_stats, targetRoot, HealSession.SanitizeCodeSeeds(seedCodeHeal));

            session.Configure(pivotBlock, _codeStore, _logger);
            session.ForceDescentTowards(AccountsToDescendTowards(seedStorageHeal));

            var fetcher = new HealBatchFetcher(_scheduler, _logger, NoPeerFailureBackoff);
            var frontier = new HealFrontier(session, fetcher, _sink, _nodeStore, _logger, _flatWriter, _codeStore);
            frontier.SeedFrontier();
            session.BeginRounds();
            var rotation = new HealRotation(session, frontier, _logger, _metrics, PivotRefresher);

            while (frontier.HasWork && session.Round < MaxRounds)
            {
                ct.ThrowIfCancellationRequested();
                var result = await RunHealRoundAsync(session, frontier, fetcher, rotation, ct).ConfigureAwait(false);
                if (result.HasValue) return result.Value;
            }

            _sink.Flush();
            return await BuildFinalResultAsync(session, frontier).ConfigureAwait(false);
        }

        private IReadOnlyCollection<byte[]>? AccountsToDescendTowards(
            IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seedStorageHeal)
        {
            if (seedStorageHeal == null || seedStorageHeal.Count == 0) return AccountsNeedingHeal;
            var accounts = new List<byte[]>(AccountsNeedingHeal ?? Array.Empty<byte[]>());
            foreach (var seed in seedStorageHeal)
                if (seed.AccountHash is { Length: 32 }) accounts.Add(seed.AccountHash);
            return accounts;
        }

        private async Task<HealResult?> RunHealRoundAsync(
            HealSession session, HealFrontier frontier, HealBatchFetcher fetcher, HealRotation rotation,
            CancellationToken ct)
        {
            session.Round++;

            var batch = frontier.TakeBatch();

            var pathsets = HealBatchFetcher.BuildPathSets(batch);

            var (resp, fetchFailed) =
                await fetcher.FetchAsync(session.LiveTargetRoot, pathsets, batch, frontier.ToFetch, session.Round, ct).ConfigureAwait(false);

            var (earlyExit, processed, nodesAddedThisRound) = frontier.ProcessResponse(batch, resp, fetchFailed);
            if (earlyExit.HasValue) return earlyExit.Value;

            var (codeAdded, codeProgress) = await AdvanceCodeDependenciesAsync(session, frontier, batch.Count, ct);
            nodesAddedThisRound += codeAdded;

            if (batch.Count == 0 && nodesAddedThisRound == 0 && !codeProgress)
                await Task.Delay(NoPeerFailureBackoff, ct).ConfigureAwait(false);

            session.TotalNodesFetched += (int)nodesAddedThisRound;
            _sink.Flush();

            if (nodesAddedThisRound > 0)
                _metrics?.RecordPhase3NodesHealed(nodesAddedThisRound);
            _metrics?.SetPhase3QueueDepth(frontier.ToFetch.Count);

            var conclude = await rotation.MaybeConcludeAsync(processed, nodesAddedThisRound, codeProgress, ct);
            if (conclude.HasValue) return conclude.Value;

            LogHealRoundProgress(session, frontier, processed, batch.Count, nodesAddedThisRound);
            return null;
        }

        private async Task<(long Added, bool CodeProgress)> AdvanceCodeDependenciesAsync(
            HealSession session, HealFrontier frontier, int batchCount, CancellationToken ct)
        {
            if (frontier.ToFetch.Count == 0 && frontier.NeededCode.Count > 0 && batchCount == 0)
                _logger.LogInformation("heal.transition walk drained at round {Round}; resolving {Codes} bytecode dependencies", session.Round, frontier.NeededCode.Count);
            int neededCodeBefore = frontier.NeededCode.Count;
            long added = 0;
            if (frontier.NeededCode.Count > 0)
                added = await ResolveCodeDependenciesAsync(frontier, ct);
            return (added, frontier.NeededCode.Count < neededCodeBefore);
        }

        private void LogHealRoundProgress(
            HealSession session, HealFrontier frontier, int processed, int batchCount, long nodesAddedThisRound)
        {
            var stats = session.Stats;
            if (session.Round % 8 == 0 || frontier.ToFetch.Count == 0)
                _logger.LogInformation(
                    "Heal round {Round}: keyspace~{Pct:F1}% processed={Processed}/{Batch} committed={Committed} fetched_total={Total} queue={Queue} stall={Stall} pruned={Pruned} absent={Absent} stale={Stale}",
                    session.Round, System.Numerics.BitOperations.PopCount((uint)stats.TopBranchesHealed) * 6.25,
                    processed, batchCount, nodesAddedThisRound, session.TotalNodesFetched, frontier.ToFetch.Count, session.StallRounds,
                    stats.PrunedChildren, stats.RequiredAbsent, stats.RequiredStale);
            if (session.Round % 64 == 0 && stats.StorageOwnerRequires.Count > 0)
                _logger.LogInformation(
                    "Heal storage hotspots (owner-hash prefix : required fetches): {Owners}",
                    stats.TopStorageOwners());
        }

        private async Task<long> ResolveCodeDependenciesAsync(HealFrontier frontier, CancellationToken ct)
        {
            if (_codeStore == null || frontier.NeededCode.Count == 0) return 0;
            long committed = 0;
            var needed = new List<byte[]>(frontier.NeededCode);
            var toFetch = await SnapByteCodes.AbsentAsync(_codeStore, needed).ConfigureAwait(false);
            var absent = new HashSet<byte[]>(toFetch, ByteArrayComparer.Current);
            foreach (var h in needed)
                if (!absent.Contains(h)) committed += frontier.SatisfyCode(h);
            foreach (var chunk in SnapByteCodes.Chunks(toFetch))
            {
                ct.ThrowIfCancellationRequested();
                ByteCodesMessage resp;
                try { resp = await _scheduler.FetchByteCodesAsync(chunk, HealBatchFetcher.ResponseBytesBudget, ct).ConfigureAwait(false); }
                catch (FetchRequestFailedException) { continue; }
                foreach (var (h, code) in SnapByteCodes.Matched(resp.Codes, frontier.NeededCode.Contains))
                {
                    await _codeStore.SaveCodeAsync(h, code).ConfigureAwait(false);
                    _metrics?.RecordPhase3BytecodesHealed(1);
                    committed += frontier.SatisfyCode(h);
                }
            }
            return committed;
        }

        private async Task<HealResult> BuildFinalResultAsync(HealSession session, HealFrontier frontier)
        {
            if (frontier.ToFetch.Count > 0) return ExhaustedResult(session, frontier);
            if (frontier.AwaitingChildren.Count > 0) return UncommittedParentsResult(session, frontier);
            var matched = await CheckConvergenceAsync(session, frontier).ConfigureAwait(false);
            return FinalResult(session, matched);
        }

        private HealResult ExhaustedResult(HealSession session, HealFrontier frontier)
        {
            _logger.LogWarning("Heal exhausted {MaxRounds} rounds with {Remaining} unresolved tasks", MaxRounds, frontier.ToFetch.Count);
            return session.NotConverged(session.Stats);
        }

        private HealResult UncommittedParentsResult(HealSession session, HealFrontier frontier)
        {
            _logger.LogError(
                "Heal queue drained with {Count} uncommitted parents — reporting non-convergence", frontier.AwaitingChildren.Count);
            return session.NotConverged(session.Stats);
        }

        private async Task<bool> CheckConvergenceAsync(HealSession session, HealFrontier frontier)
        {
            _logger.LogInformation(
                "heal.transition loop exited at round {Round} (queue={Queue}, codes={Codes}) — verifying convergence",
                session.Round, frontier.ToFetch.Count, frontier.NeededCode.Count);
            var matched = session.TargetIsEmptyTrie || _sink.HasRoot(session.LiveTargetRoot);

            if (matched && session.HasCodeSeeds)
                matched = (await SnapByteCodes.AbsentAsync(_codeStore, session.CodeSeeds).ConfigureAwait(false)).Count == 0;
            return matched;
        }

        private HealResult FinalResult(HealSession session, bool matched)
        {
            _logger.LogInformation(
                "Heal complete after {Rounds} rounds ({Fetched} nodes fetched): target=0x{Target} matched={Matched}",
                session.Round, session.TotalNodesFetched, session.LiveTargetRoot.ToHex(), matched);
            return session.Converged(session.Stats, matched);
        }
    }
}
