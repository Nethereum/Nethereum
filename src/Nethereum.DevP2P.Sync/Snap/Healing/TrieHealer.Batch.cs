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
        private List<HealTask> BuildBatch()
        {
            var batch = new List<HealTask>(BatchSize);
            while (_queue.Count > 0 && batch.Count < BatchSize)
                batch.Add(_queue.Pop());
            return batch;
        }

        private static List<List<byte[]>> BuildPathSets(List<HealTask> batch)
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

        private async Task<(TrieNodesMessage resp, bool fetchFailed)> FetchTrieNodesForBatchAsync(
            byte[] liveTargetRoot, List<List<byte[]>> pathsets, List<HealTask> batch,
            Stack<HealTask> queue, int round, CancellationToken ct)
        {
            TrieNodesMessage resp = null;
            bool fetchFailed = false;
            int subSize = Math.Max(32, (pathsets.Count + HealFetchConcurrency - 1) / HealFetchConcurrency);
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
            if (subFetches.Count > 0 && failedFetches == subFetches.Count)
            {
                fetchFailed = true;
                _logger.LogWarning("Heal round {Round} fetch failed (no serving peer / retries exhausted); requeuing batch", round);
                foreach (var t in batch) queue.Push(t);
                await Task.Delay(NoPeerFailureBackoff, ct).ConfigureAwait(false);
            }
            else
            {
                resp = new TrieNodesMessage { Nodes = mergedNodes };
            }
            return (resp, fetchFailed);
        }

        private static Func<TrieNodesMessage, bool> BuildTrieNodeVerify(List<HealTask> subBatch)
        {
            return resp =>
            {
                if (resp?.Nodes == null || resp.Nodes.Count == 0) return true;
                var keccak = Sha3KeccackHashProvider.Instance;
                var wanted = new Dictionary<byte[], int>(ByteArrayComparer.Current);
                foreach (var t in subBatch)
                    wanted[t.ExpectedHash] = wanted.TryGetValue(t.ExpectedHash, out var c) ? c + 1 : 1;
                foreach (var blob in resp.Nodes)
                {
                    if (blob == null || blob.Length == 0) continue;
                    var hash = keccak.ComputeHash(blob);
                    if (wanted.TryGetValue(hash, out var remaining) && remaining > 0)
                        wanted[hash] = remaining - 1;
                    else
                        return false;
                }
                return true;
            };
        }

        private (byte[][] filled, byte[][] filledHash) MatchNodesToTasks(
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

        private static string LocKey(in HealTask t)
            => t.IsStorage
                ? "s:" + t.AccountHash.ToHex() + ":" + t.NibblePath.ToHex()
                : "a:" + t.NibblePath.ToHex();
    }
}
