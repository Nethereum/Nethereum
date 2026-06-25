using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// snap/1 heal phase. Drives the trie under <c>targetRoot</c> to
    /// completeness by BFS-walking the in-storage view and re-fetching any
    /// hash-referenced node that's missing via
    /// <see cref="IFetchRequestScheduler.FetchTrieNodesAsync"/>:
    /// <list type="bullet">
    ///   <item>Walk the state trie discovering missing inner-trie nodes.</item>
    ///   <item>Each leaf body with a non-empty <c>StorageRoot</c> queues a
    ///     heal of that account's storage subtree.</item>
    ///   <item>Hash returned nodes, persist under hash, decode + recurse.</item>
    ///   <item>Loop until no unresolved references remain.</item>
    /// </list>
    ///
    /// <para>
    /// The fetch flows through <see cref="IFetchRequestScheduler"/> so each
    /// batch hits a snap-capable peer with the same retry / disconnect /
    /// score-based rotation used for header+body fetches.
    /// </para>
    /// </summary>
    public sealed class TrieHealer
    {
        // Batch + budget tuned to stay safely inside the snap/1 GetTrieNodes
        // soft response limit (2 MB) and the 1024 max-node-lookups cap per
        // call. 256 paths * worst-case ~1.5 KB/node ≈ 384 KB — plenty of
        // headroom for branch nodes.
        private const int BatchSize = 256;
        private const ulong ResponseBytesBudget = 2 * 1024 * 1024;
        // Heal can run for multiple hours on mainnet; we allow up to
        // ~100k batches (covers ~25M nodes at BatchSize=256) before giving up.
        // The convergence shrinks per round so the practical wall-time is
        // dominated by the first few rounds of drift recovery.
        private const int MaxRounds = 100_000;

        // When this many consecutive batches all return zero usable nodes,
        // the peers' snapshots have rotated past our captured pivot. Refresh
        // the live pivot, reseed the queue with the new root, continue. The
        // live head advances while we heal; we poll the caller for a refreshed
        // pivot rather than receiving pushed roots.
        private const int StallThresholdRounds = 32;

        // Preemptive pivot refresh — even when peers keep serving SOME nodes
        // the live head moves on, and a heal running for hours against an
        // obsolete-but-still-snapshotted root will not converge to the chain
        // tip. SnapSyncClient does the equivalent every 8 account-range rounds
        // during the leaf phase. Here we poll every N heal rounds regardless
        // of stall state; the caller signals a new pivot via PivotRefresher.
        // At 256 nodes/batch with ~200ms-2s per round, the cadence below works
        // out to ~50s-10min, safely inside the ~1024-block (~3.4 h mainnet)
        // snapshot retention window peers keep.
        private const int PreemptiveRefreshIntervalRounds = 256;

        private readonly IFetchRequestScheduler _scheduler;
        private readonly ITrieStorage _storage;
        private readonly ILogger _logger;
        private readonly SnapSyncMetrics? _metrics;

        public TrieHealer(IFetchRequestScheduler scheduler, ITrieStorage storage, ILogger? logger = null, SnapSyncMetrics? metrics = null)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _logger = logger ?? NullLogger.Instance;
            _metrics = metrics;
        }

        /// <summary>
        /// Pivot-refresh callback. When stalled (peers serving zero nodes for
        /// many consecutive batches), the healer calls this to get a fresh
        /// target root, reseeds the queue, and resumes against the new root.
        /// </summary>
        public Func<CancellationToken, Task<byte[]>>? PivotRefresher { get; set; }

        /// <summary>
        /// Heal the trie under <paramref name="targetRoot"/>. Returns true if
        /// the assembled trie's root hashes to the FINAL target root (which
        /// may differ from <paramref name="targetRoot"/> if the pivot rotated
        /// during heal); false if rounds were exhausted before convergence.
        /// </summary>
        public readonly record struct HealResult(
            bool Matched,
            int TotalNodesFetched,
            byte[] FinalTargetRoot,
            byte[] ComputedRoot);

        public async Task<HealResult> HealAsync(byte[] targetRoot, CancellationToken ct = default)
        {
            if (targetRoot == null || targetRoot.Length != 32)
                throw new ArgumentException("targetRoot must be 32 bytes", nameof(targetRoot));

            var liveTargetRoot = targetRoot;
            int stallRounds = 0;

            var keccak = new Sha3KeccackHashProvider();
            var decoder = new NodeDecoder();
            var accountDecoder = new AccountEncoder();

            // Queue entries:
            //   isStorage=false → state-trie path, accountHash=null
            //   isStorage=true  → storage-trie path under that account
            var queue = new Queue<HealTask>();
            queue.Enqueue(new HealTask(IsStorage: false, AccountHash: null, NibblePath: Array.Empty<byte>(), ExpectedHash: liveTargetRoot));

            int round = 0;
            int totalNodesFetched = 0;
            while (queue.Count > 0 && round < MaxRounds)
            {
                ct.ThrowIfCancellationRequested();
                round++;

                var batch = new List<HealTask>(BatchSize);
                while (queue.Count > 0 && batch.Count < BatchSize)
                    batch.Add(queue.Dequeue());

                var pathsets = new List<List<byte[]>>(batch.Count);
                foreach (var task in batch)
                {
                    var compact = PatriciaPathWalker.NibblesToCompact(task.NibblePath);
                    if (task.IsStorage)
                        pathsets.Add(new List<byte[]> { task.AccountHash!, compact });
                    else
                        pathsets.Add(new List<byte[]> { compact });
                }

                TrieNodesMessage resp;
                try
                {
                    resp = await _scheduler.FetchTrieNodesAsync(liveTargetRoot, pathsets, ResponseBytesBudget, ct)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Heal round {Round} fetch failed; requeuing batch", round);
                    foreach (var t in batch) queue.Enqueue(t);
                    continue;
                }

                // Match returned blobs to requested tasks BY HASH. The snap
                // server omits nodes it cannot serve and truncates the response
                // when it hits its byte/load/time limits, so the reply is
                // densely packed and may be shorter than the batch — it is NOT
                // positionally aligned with the request. Hash each returned blob
                // and walk the batch forward to the task whose expected hash
                // matches, leaving skipped tasks as misses to retry next round.
                // Trusting the index would feed a node to the wrong task, derive
                // child paths from the wrong subtree, and stall convergence.
                var filled = new byte[batch.Count][];
                var filledHash = new byte[batch.Count][];
                int matchCursor = 0;
                for (int i = 0; i < resp.Nodes.Count; i++)
                {
                    var blob = resp.Nodes[i];
                    if (blob == null || blob.Length == 0) continue;
                    var hash = keccak.ComputeHash(blob);
                    while (matchCursor < batch.Count
                        && !ByteUtil.AreEqual(hash, batch[matchCursor].ExpectedHash))
                        matchCursor++;
                    if (matchCursor >= batch.Count)
                    {
                        // Ran out of requested hashes — the peer returned a node
                        // we never asked for. Keep the already hash-matched nodes
                        // and retry the rest, logging the anomaly.
                        _logger.LogWarning(
                            "Heal round {Round}: unexpected trienode at index {Index} (hash 0x{Hash}) — discarding remainder of response",
                            round, i, hash.ToHex());
                        break;
                    }
                    filled[matchCursor] = blob;
                    filledHash[matchCursor] = hash;
                    matchCursor++;
                }

                int processed = 0;
                long nodesAddedThisRound = 0;
                for (int k = 0; k < batch.Count; k++)
                {
                    var task = batch[k];
                    var blob = filled[k];
                    if (blob == null) { queue.Enqueue(task); continue; }

                    if (_storage.Get(filledHash[k]) == null)
                    {
                        _storage.Put(filledHash[k], blob);
                        totalNodesFetched++;
                        nodesAddedThisRound++;
                    }

                    var node = decoder.DecodeNodeFromRlpData(blob, decodeHashNodes: false, _storage);
                    QueueChildren(node, task, queue, accountDecoder);
                    processed++;
                }

                if (nodesAddedThisRound > 0)
                    _metrics?.RecordPhase3NodesHealed(nodesAddedThisRound);
                _metrics?.SetPhase3QueueDepth(queue.Count);

                if (processed == 0) stallRounds++; else stallRounds = 0;

                // Refresh the pivot when either (a) peers' snapshots have
                // rotated past our captured pivot and we're stalled, or
                // (b) we've been walking long enough that the chain has
                // moved on even though peers are still serving nodes. Both
                // paths use the same retarget+reseed: queue is cleared and
                // re-rooted at the new state-trie root; already-fetched
                // nodes stay in storage and are reused when their hashes
                // recur under the new root (content-addressing).
                bool stallTrigger = stallRounds >= StallThresholdRounds;
                bool preemptiveTrigger = round % PreemptiveRefreshIntervalRounds == 0;
                bool rootRotated = false;
                if ((stallTrigger || preemptiveTrigger) && PivotRefresher != null)
                {
                    try
                    {
                        var refreshed = await PivotRefresher(ct).ConfigureAwait(false);
                        if (refreshed != null && refreshed.Length == 32
                            && !ByteUtil.AreEqual(refreshed, liveTargetRoot))
                        {
                            _logger.LogWarning(
                                "Heal {Trigger} at round {Round} (stall={Stall}) — rotating root 0x{Old} → 0x{New}",
                                stallTrigger ? "stalled" : "preemptive-refresh",
                                round, stallRounds, liveTargetRoot.ToHex(), refreshed.ToHex());
                            liveTargetRoot = refreshed;
                            queue.Clear();
                            queue.Enqueue(new HealTask(IsStorage: false, AccountHash: null, NibblePath: Array.Empty<byte>(), ExpectedHash: liveTargetRoot));
                            stallRounds = 0;
                            rootRotated = true;
                            _metrics?.RecordPhase3PivotRotation();
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Heal pivot refresh failed; continuing against current root");
                    }
                }

                // Stall-detection exit: peers are no longer serving usable nodes
                // AND a pivot refresh (if available) did not change the target
                // root. Without this guard the outer loop busy-spins to
                // MaxRounds=100k even though no progress is possible. We surface
                // it as a non-convergence HealResult so the caller can decide
                // whether to retry from a fresh bootstrap.
                if (stallTrigger && !rootRotated)
                {
                    _logger.LogWarning(
                        "snap.phase3.stalled at round={Round} stall={Stall} queue={Queue} — pivot refresh returned same root",
                        round, stallRounds, queue.Count);
                    return new HealResult(false, totalNodesFetched, liveTargetRoot, Array.Empty<byte>());
                }

                if (round % 8 == 0 || queue.Count == 0)
                    _logger.LogInformation(
                        "Heal round {Round}: processed={Processed}/{Batch} fetched_total={Total} queue={Queue} stall={Stall}",
                        round, processed, batch.Count, totalNodesFetched, queue.Count, stallRounds);
            }

            if (queue.Count > 0)
            {
                _logger.LogWarning("Heal exhausted {MaxRounds} rounds with {Remaining} unresolved tasks", MaxRounds, queue.Count);
                return new HealResult(false, totalNodesFetched, liveTargetRoot, Array.Empty<byte>());
            }

            // Final verification: load + walk + compute root against the
            // live target root (which may differ from the initial one if
            // the pivot rotated during heal).
            var trie = PatriciaTrie.LoadFromStorage(liveTargetRoot, _storage);
            var computedRoot = trie.Root.GetHash();
            var matched = ByteUtil.AreEqual(computedRoot, liveTargetRoot);
            _logger.LogInformation(
                "Heal complete after {Rounds} rounds ({Fetched} nodes fetched): computed_root=0x{Computed} target=0x{Target} matched={Matched}",
                round, totalNodesFetched, computedRoot.ToHex(), liveTargetRoot.ToHex(), matched);
            return new HealResult(matched, totalNodesFetched, liveTargetRoot, computedRoot);
        }

        private void QueueChildren(
            Node node, HealTask current, Queue<HealTask> queue, AccountEncoder accountDecoder)
        {
            switch (node)
            {
                case BranchNode branch:
                    for (int i = 0; i < 16; i++)
                    {
                        var child = branch.Children[i];
                        if (child is HashNode bh && _storage.Get(bh.Hash) == null)
                        {
                            var childPath = ByteUtil.AppendByte(current.NibblePath, (byte)i);
                            queue.Enqueue(current with { NibblePath = childPath, ExpectedHash = bh.Hash });
                        }
                    }
                    break;

                case ExtendedNode ext:
                    if (ext.InnerNode is HashNode eh && _storage.Get(eh.Hash) == null)
                    {
                        var childPath = ConcatNibbles(current.NibblePath, ext.Nibbles);
                        queue.Enqueue(current with { NibblePath = childPath, ExpectedHash = eh.Hash });
                    }
                    break;

                case LeafNode leaf:
                    if (!current.IsStorage)
                    {
                        Account account;
                        try { account = accountDecoder.Decode(leaf.Value); }
                        catch { return; }
                        if (account?.StateRoot == null
                            || ByteUtil.AreEqual(account.StateRoot, DefaultValues.EMPTY_TRIE_HASH))
                            return;
                        if (_storage.Get(account.StateRoot) != null) return;

                        // Reconstruct the 32-byte keccak(address) for this
                        // account leaf: the full nibble key is `current path
                        // through state trie || leaf's tail nibbles`.
                        var fullKeyNibbles = ConcatNibbles(current.NibblePath, leaf.Nibbles);
                        if (fullKeyNibbles.Length != 64) return;
                        var accountHash = fullKeyNibbles.ConvertFromNibbles();
                        queue.Enqueue(new HealTask(IsStorage: true, AccountHash: accountHash, NibblePath: Array.Empty<byte>(), ExpectedHash: account.StateRoot));
                    }
                    break;
            }
        }

        private static byte[] ConcatNibbles(byte[] a, byte[] b)
        {
            if (b == null || b.Length == 0) return a;
            var copy = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, copy, 0, a.Length);
            Buffer.BlockCopy(b, 0, copy, a.Length, b.Length);
            return copy;
        }

        private readonly record struct HealTask(bool IsStorage, byte[]? AccountHash, byte[] NibblePath, byte[] ExpectedHash);
    }
}
