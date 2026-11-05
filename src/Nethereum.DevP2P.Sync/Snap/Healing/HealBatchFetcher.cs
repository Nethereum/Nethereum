using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.DevP2P.Sync.Snap.Healing
{
    internal sealed class HealBatchFetcher
    {
        internal const int BatchSize = 2048;
        internal const ulong ResponseBytesBudget = 512 * 1024;
        internal const int FetchConcurrency = 16;

        private readonly IFetchRequestScheduler _scheduler;
        private readonly ILogger _logger;
        private readonly TimeSpan _noPeerFailureBackoff;

        internal HealBatchFetcher(IFetchRequestScheduler scheduler, ILogger logger, TimeSpan noPeerFailureBackoff)
        {
            _scheduler = scheduler;
            _logger = logger;
            _noPeerFailureBackoff = noPeerFailureBackoff;
        }

        internal static List<List<byte[]>> BuildPathSets(List<HealTask> batch)
        {
            var pathsets = new List<List<byte[]>>(batch.Count);
            foreach (var task in batch)
            {
                var compact = PatriciaPathWalker.NibblesToCompact(task.NibblePath);
                if (task.IsStorage)
                    pathsets.Add(new List<byte[]> { task.AccountHash!, compact });
                else
                    pathsets.Add(new List<byte[]> { compact });
            }
            return pathsets;
        }

        internal async Task<(TrieNodesMessage resp, bool fetchFailed)> FetchAsync(
            byte[] liveTargetRoot, List<List<byte[]>> pathsets, List<HealTask> batch,
            Stack<HealTask> toFetch, int round, CancellationToken ct)
        {
            var subFetches = StartSubFetches(liveTargetRoot, pathsets, batch, ct);
            var (mergedNodes, failedFetches) = await MergeSubFetchesAsync(subFetches, round).ConfigureAwait(false);
            return await ConcludeFetchAsync(subFetches.Count, failedFetches, mergedNodes, batch, toFetch, round, ct).ConfigureAwait(false);
        }

        private List<Task<TrieNodesMessage>> StartSubFetches(
            byte[] liveTargetRoot, List<List<byte[]>> pathsets, List<HealTask> batch, CancellationToken ct)
        {
            int subSize = Math.Max(32, (pathsets.Count + FetchConcurrency - 1) / FetchConcurrency);
            var subFetches = new List<Task<TrieNodesMessage>>();
            for (int off = 0; off < pathsets.Count; off += subSize)
            {
                Task<TrieNodesMessage> subFetch;
                try
                {
                    var count = Math.Min(subSize, pathsets.Count - off);
                    var subBatch = batch.GetRange(off, count);
                    subFetch = _scheduler.FetchTrieNodesAsync(
                        liveTargetRoot, pathsets.GetRange(off, count),
                        ResponseBytesBudget, BuildTrieNodeVerify(subBatch), ct);
                }
                catch (Exception ex)
                {
                    subFetch = Task.FromException<TrieNodesMessage>(ex);
                }
                subFetches.Add(subFetch);
            }
            return subFetches;
        }

        private async Task<(List<byte[]> mergedNodes, int failedFetches)> MergeSubFetchesAsync(
            List<Task<TrieNodesMessage>> subFetches, int round)
        {
            var mergedNodes = new List<byte[]>();
            int failedFetches = 0;
            foreach (var subFetch in subFetches)
            {
                try
                {
                    var sub = await subFetch.ConfigureAwait(false);
                    if (sub?.Nodes != null) mergedNodes.AddRange(sub.Nodes);
                }
                catch (OperationCanceledException) { throw; }
                catch (FetchRequestFailedException) { failedFetches++; }
                catch (SnapPeerCapabilityMismatchException ex)
                {
                    _logger.LogError(ex,
                        "Heal round {Round}: peer capability mismatch; permanent — failing fast without retrying", round);
                    throw;
                }
                catch (Exception ex)
                {
                    failedFetches++;
                    _logger.LogWarning(ex, "Heal round {Round}: sub-fetch failed; its tasks retry next round", round);
                }
            }
            return (mergedNodes, failedFetches);
        }

        private async Task<(TrieNodesMessage resp, bool fetchFailed)> ConcludeFetchAsync(
            int subFetchCount, int failedFetches, List<byte[]> mergedNodes, List<HealTask> batch,
            Stack<HealTask> toFetch, int round, CancellationToken ct)
        {
            if (subFetchCount > 0 && failedFetches == subFetchCount)
            {
                _logger.LogWarning("Heal round {Round} fetch failed (no serving peer / retries exhausted); requeuing batch", round);
                foreach (var t in batch) toFetch.Push(t);
                await Task.Delay(_noPeerFailureBackoff, ct).ConfigureAwait(false);
                return (null, true);
            }
            return (new TrieNodesMessage { Nodes = mergedNodes }, false);
        }

        private static Func<TrieNodesMessage, bool> BuildTrieNodeVerify(List<HealTask> subBatch)
        {
            return resp => SnapProofVerifier.VerifyHashedBlobs(
                subBatch.ConvertAll(t => t.ExpectedHash), resp?.Nodes);
        }

        internal (byte[][] filled, byte[][] filledHash) MatchNodesToTasks(
            List<HealTask> batch, TrieNodesMessage resp, Sha3KeccackHashProvider keccak, int round)
        {
            var filled = new byte[batch.Count][];
            var filledHash = new byte[batch.Count][];
            var wanted = new Dictionary<byte[], Queue<int>>(ByteArrayComparer.Current);
            for (int k = 0; k < batch.Count; k++)
            {
                if (!wanted.TryGetValue(batch[k].ExpectedHash, out var idxs))
                {
                    idxs = new Queue<int>();
                    wanted[batch[k].ExpectedHash] = idxs;
                }
                idxs.Enqueue(k);
            }
            for (int i = 0; i < resp.Nodes.Count; i++)
            {
                var blob = resp.Nodes[i];
                if (blob == null || blob.Length == 0) continue;
                var hash = keccak.ComputeHash(blob);
                if (wanted.TryGetValue(hash, out var idxs) && idxs.Count > 0)
                {
                    int idx = idxs.Dequeue();
                    filled[idx] = blob;
                    filledHash[idx] = hash;
                }
                else
                {
                    _logger.LogWarning(
                        "Heal round {Round}: unrequested trienode at index {Index} (hash 0x{Hash}) — ignoring",
                        round, i, hash.ToHex());
                }
            }
            return (filled, filledHash);
        }
    }
}
