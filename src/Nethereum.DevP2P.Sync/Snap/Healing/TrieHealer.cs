using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Documentation;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Nodes.Rlp;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.ProofVerification;

namespace Nethereum.DevP2P.Sync.Snap.Healing
{
    public sealed partial class TrieHealer
    {
        private const int BatchSize = 2048;
        private const ulong ResponseBytesBudget = 512 * 1024;
        private const int HealFetchConcurrency = 16;
        private const int MaxRounds = 100_000;

        private const int StallThresholdRounds = 32;

        private static readonly TimeSpan PivotCheckInterval = TimeSpan.FromSeconds(12);

        private const ulong StalePivotDistanceBlocks = 120;

        private static readonly TimeSpan RefresherUnavailableLogInterval = TimeSpan.FromMinutes(10);

        public static bool ShouldRotateHealPivot(
            bool stalled, bool rootChanged, ulong currentPivotBlock, ulong newPivotBlock, ulong staleDistanceBlocks)
        {
            if (!rootChanged) return false;
            if (stalled) return true;
            return newPivotBlock >= currentPivotBlock + staleDistanceBlocks;
        }

        private const int MaxPendingNodes = 1_000_000;

        public TimeSpan FetchFailureBackoff { get; set; } = TimeSpan.FromMilliseconds(500);

        private int _topBranchesHealed;
        public TimeSpan NoPeerFailureBackoff { get; set; } = TimeSpan.FromSeconds(2);

        private long _prunedChildren;
        private long _requiredAbsent;
        private long _requiredStale;
        private readonly Dictionary<string, long> _storageOwnerRequires = new();

        private bool ProbeAndCount(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash)
        {
            switch (_sink.Probe(isStorage, accountHash, nibblePath, expectedHash))
            {
                case Nethereum.CoreChain.Storage.HealNodePresence.Match:
                    _prunedChildren++;
                    return false;
                case Nethereum.CoreChain.Storage.HealNodePresence.Stale:
                    _requiredStale++;
                    break;
                default:
                    _requiredAbsent++;
                    break;
            }
            if (isStorage && accountHash != null && accountHash.Length >= 4)
            {
                var owner = accountHash[0].ToString("x2") + accountHash[1].ToString("x2")
                          + accountHash[2].ToString("x2") + accountHash[3].ToString("x2");
                _storageOwnerRequires.TryGetValue(owner, out var count);
                _storageOwnerRequires[owner] = count + 1;
            }
            return true;
        }

        private string TopStorageOwners(int top = 5)
        {
            if (_storageOwnerRequires.Count == 0) return "-";
            return string.Join(",", _storageOwnerRequires
                .OrderByDescending(kv => kv.Value)
                .Take(top)
                .Select(kv => $"{kv.Key}:{kv.Value}"));
        }

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

        private byte[] _liveTargetRoot;
        private ulong _currentPivotBlock;
        private bool _seeded;
        private bool _hasCodeSeeds;
        private bool _codeOnly;
        private int _stallRounds;
        private DateTimeOffset? _lastRefresherUnavailableLogAt;
        private bool _committedEver;
        private Sha3KeccackHashProvider _keccak;
        private NodeDecoder _decoder;
        private AccountEncoder _accountDecoder;
        private Stack<HealTask> _queue;
        private Dictionary<string, PendingNode> _pending;
        private Dictionary<string, string> _parentOf;
        private HashSet<string> _inQueue;
        private Dictionary<byte[], List<string>> _codeWaiters;
        private HashSet<byte[]> _neededCode;
        private int _round;
        private int _totalNodesFetched;
        private System.Diagnostics.Stopwatch _pivotPollSw;

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

            seedStorageHeal = SanitizeSeeds(seedStorageHeal);
            seedCodeHeal = SanitizeCodeSeeds(seedCodeHeal);

            ConfigureHealSession(targetRoot, seedStorageHeal, seedCodeHeal, pivotBlock, wipeSeedsFirst);
            PrepareHealState(seedStorageHeal, seedCodeHeal);

            while ((_queue.Count > 0 || _neededCode.Count > 0) && _round < MaxRounds)
            {
                ct.ThrowIfCancellationRequested();
                var result = await RunHealRoundAsync(seedStorageHeal, ct).ConfigureAwait(false);
                if (result.HasValue) return result.Value;
            }

            _sink.Flush();
            return await BuildFinalResult(seedStorageHeal, seedCodeHeal).ConfigureAwait(false);
        }

        private void ConfigureHealSession(
            byte[] targetRoot,
            IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seedStorageHeal,
            IReadOnlyList<byte[]> seedCodeHeal,
            ulong pivotBlock,
            bool wipeSeedsFirst)
        {
            _liveTargetRoot = targetRoot;
            _currentPivotBlock = pivotBlock;
            _seeded = seedStorageHeal != null && seedStorageHeal.Count > 0;
            _hasCodeSeeds = seedCodeHeal != null && seedCodeHeal.Count > 0;
            if (_hasCodeSeeds && _codeStore == null)
            {
                _logger.LogError(
                    "heal.code.seed {Count} bytecode heal seed(s) supplied but no code store is wired — these codes cannot be healed",
                    seedCodeHeal.Count);
                _hasCodeSeeds = false;
            }
            _codeOnly = _hasCodeSeeds && !_seeded;

            if (_seeded && wipeSeedsFirst)
            {
                foreach (var s in seedStorageHeal)
                    _sink.ForceWipeStorage(s.AccountHash);
            }
            _stallRounds = 0;
            _lastRefresherUnavailableLogAt = null;
            _committedEver = false;
        }

        private void PrepareHealState(
            IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seedStorageHeal,
            IReadOnlyList<byte[]> seedCodeHeal)
        {
            _keccak = Sha3KeccackHashProvider.Instance;
            _decoder = new NodeDecoder();
            _accountDecoder = new AccountEncoder();

            _queue = new Stack<HealTask>();
            _pending = new Dictionary<string, PendingNode>();
            _parentOf = new Dictionary<string, string>();
            _inQueue = new HashSet<string>();
            _codeWaiters = new Dictionary<byte[], List<string>>(ByteArrayComparer.Current);
            _neededCode = new HashSet<byte[]>(ByteArrayComparer.Current);

            SeedQueue(seedStorageHeal, seedCodeHeal);

            _round = 0;
            _totalNodesFetched = 0;
            _pivotPollSw = System.Diagnostics.Stopwatch.StartNew();
        }

        private async Task<HealResult?> RunHealRoundAsync(
            IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seedStorageHeal, CancellationToken ct)
        {
            _round++;

            var batch = BuildBatch();

            var pathsets = BuildPathSets(batch);

            var (resp, fetchFailed) =
                await FetchTrieNodesForBatchAsync(_liveTargetRoot, pathsets, batch, _queue, _round, ct).ConfigureAwait(false);

            var (earlyExit, processed, nodesAddedThisRound) = ProcessBatchResponse(batch, resp, fetchFailed);
            if (earlyExit.HasValue) return earlyExit.Value;

            var (codeAdded, codeProgress) = await AdvanceCodeDependenciesAsync(batch.Count, ct);
            nodesAddedThisRound += codeAdded;

            if (batch.Count == 0 && nodesAddedThisRound == 0 && !codeProgress)
                await Task.Delay(NoPeerFailureBackoff, ct).ConfigureAwait(false);

            _totalNodesFetched += (int)nodesAddedThisRound;
            _sink.Flush();

            if (nodesAddedThisRound > 0)
                _metrics?.RecordPhase3NodesHealed(nodesAddedThisRound);
            _metrics?.SetPhase3QueueDepth(_queue.Count);

            var conclude = await MaybeConcludeCycleAsync(processed, nodesAddedThisRound, codeProgress, ct, seedStorageHeal);
            if (conclude.HasValue) return conclude.Value;

            LogHealRoundProgress(processed, batch.Count, nodesAddedThisRound);
            return null;
        }

        private async Task<(long Added, bool CodeProgress)> AdvanceCodeDependenciesAsync(int batchCount, CancellationToken ct)
        {
            if (_queue.Count == 0 && _neededCode.Count > 0 && batchCount == 0)
                _logger.LogInformation("heal.transition walk drained at round {Round}; resolving {Codes} bytecode dependencies", _round, _neededCode.Count);
            int neededCodeBefore = _neededCode.Count;
            long added = 0;
            if (_neededCode.Count > 0)
                added = await ResolveCodeDependenciesAsync(ct);
            return (added, _neededCode.Count < neededCodeBefore);
        }

        private void LogHealRoundProgress(int processed, int batchCount, long nodesAddedThisRound)
        {
            if (_round % 8 == 0 || _queue.Count == 0)
                _logger.LogInformation(
                    "Heal round {Round}: keyspace~{Pct:F1}% processed={Processed}/{Batch} committed={Committed} fetched_total={Total} queue={Queue} stall={Stall} pruned={Pruned} absent={Absent} stale={Stale}",
                    _round, System.Numerics.BitOperations.PopCount((uint)_topBranchesHealed) * 6.25,
                    processed, batchCount, nodesAddedThisRound, _totalNodesFetched, _queue.Count, _stallRounds,
                    _prunedChildren, _requiredAbsent, _requiredStale);
            if (_round % 64 == 0 && _storageOwnerRequires.Count > 0)
                _logger.LogInformation(
                    "Heal storage hotspots (owner-hash prefix : required fetches): {Owners}",
                    TopStorageOwners());
        }

        private (HealResult? EarlyExit, int Processed, long NodesAdded) ProcessBatchResponse(
            List<HealTask> batch, TrieNodesMessage resp, bool fetchFailed)
        {
            int processed = 0;
            long nodesAddedThisRound = 0;
            if (!fetchFailed)
            {
                var (filled, filledHash) = MatchNodesToTasks(batch, resp, _keccak, _round);

                for (int k = 0; k < batch.Count; k++)
                {
                    var task = batch[k];
                    var blob = filled[k];
                    if (blob == null) { _queue.Push(task); continue; }

                    var myKey = LocKey(task);
                    _inQueue.Remove(myKey);

                    var node = _decoder.DecodeFromRlpData(blob, null, new byte[0], decodeHashNodes: false, _nodeStore);

                    int missing = CollectChildren(node, task, myKey, _accountDecoder,
                        (child, parentKey) =>
                        {
                            var childKey = LocKey(child);
                            if (_inQueue.Contains(childKey) || _pending.ContainsKey(childKey)) return false;
                            _inQueue.Add(childKey);
                            _parentOf[childKey] = parentKey;
                            _queue.Push(child);
                            return true;
                        },
                        (codeHash, leafKey) =>
                        {
                            if (!_codeWaiters.TryGetValue(codeHash, out var w)) { w = new List<string>(); _codeWaiters[codeHash] = w; }
                            w.Add(leafKey);
                            _neededCode.Add(codeHash);
                        });

                    if (missing == 0)
                    {
                        nodesAddedThisRound += PersistAndPropagate(myKey, task, filledHash[k], blob, _pending, _parentOf);
                    }
                    else
                    {
                        _pending[myKey] = new PendingNode(task, filledHash[k], blob, missing);
                        if (_pending.Count > MaxPendingNodes)
                        {
                            _totalNodesFetched += (int)nodesAddedThisRound;
                            _sink.Flush();
                            _logger.LogError(
                                "Heal pending-parent set exceeded {Max} without draining (queue={Queue}) — bailing as non-convergence",
                                MaxPendingNodes, _queue.Count);
                            return (new HealResult(false, _totalNodesFetched, _liveTargetRoot, Array.Empty<byte>(),
                _prunedChildren, _requiredAbsent, _requiredStale), processed, nodesAddedThisRound);
                        }
                    }
                    processed++;
                }
            }
            return (null, processed, nodesAddedThisRound);
        }

        private async Task<HealResult?> MaybeConcludeCycleAsync(
            int processed, long nodesAddedThisRound, bool codeProgress, CancellationToken ct,
            IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seedStorageHeal)
        {
            RecordRoundProgress(processed, nodesAddedThisRound, codeProgress);

            bool stallTrigger = _stallRounds >= StallThresholdRounds;
            bool checkTrigger = _pivotPollSw.Elapsed >= PivotCheckInterval;
            if (checkTrigger) _pivotPollSw.Restart();
            if ((stallTrigger || checkTrigger) && _seeded)
            {
                var (resolved, newRoot, newBlock, updatedSeeds) =
                    await TryResolveSeededRotationAsync(stallTrigger, ct, seedStorageHeal).ConfigureAwait(false);
                if (resolved)
                {
                    _metrics?.RecordPhase3PivotRotation();
                    return new HealResult(false, _totalNodesFetched, _liveTargetRoot, Array.Empty<byte>(),
                        _prunedChildren, _requiredAbsent, _requiredStale,
                        NeedsRetarget: true, RetargetRoot: newRoot, RetargetBlock: newBlock, RetargetSeeds: updatedSeeds);
                }
            }
            else if ((stallTrigger || checkTrigger) && PivotRefresher != null && !_seeded && !_codeOnly)
            {
                var rotated = await TryRotateUnseededPivotAsync(stallTrigger, ct).ConfigureAwait(false);
                if (rotated.HasValue) return rotated.Value;
            }

            if (stallTrigger)
            {
                _logger.LogWarning(
                    "snap.phase3.stalled at round={Round} stall={Stall} queue={Queue} — pivot refresh returned same root",
                    _round, _stallRounds, _queue.Count);
                return new HealResult(false, _totalNodesFetched, _liveTargetRoot, Array.Empty<byte>(),
                _prunedChildren, _requiredAbsent, _requiredStale);
            }

            return null;
        }

        private void RecordRoundProgress(int processed, long nodesAddedThisRound, bool codeProgress)
        {
            if (nodesAddedThisRound > 0) _committedEver = true;
            bool madeProgress = (_committedEver ? nodesAddedThisRound > 0 : processed > 0) || codeProgress;
            if (madeProgress) _stallRounds = 0; else _stallRounds++;
        }

        private sealed class PendingNode
        {
            public PendingNode(HealTask task, byte[] hash, byte[] blob, int missingChildren)
            {
                Task = task;
                Hash = hash;
                Blob = blob;
                MissingChildren = missingChildren;
            }

            public HealTask Task { get; }
            public byte[] Hash { get; }
            public byte[] Blob { get; }
            public int MissingChildren { get; set; }
        }

        private readonly record struct HealTask(bool IsStorage, byte[]? AccountHash, byte[] NibblePath, byte[] ExpectedHash);
    }
}
