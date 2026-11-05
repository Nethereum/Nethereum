using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Nodes.Rlp;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using HealResult = Nethereum.DevP2P.Sync.Snap.Healing.TrieHealer.HealResult;

namespace Nethereum.DevP2P.Sync.Snap.Healing
{
    internal sealed class HealFrontier
    {
        private const int MaxPendingNodes = 1_000_000;

        private readonly HealSession _session;
        private readonly HealStats _stats;
        private readonly HealBatchFetcher _fetcher;
        private readonly Nethereum.CoreChain.Storage.IHealNodeSink _sink;
        private readonly ITrieNodeStore _nodeStore;
        private readonly ILogger _logger;
        private readonly Nethereum.CoreChain.Storage.ISnapFlatStateWriter? _flatWriter;
        private readonly Nethereum.CoreChain.Storage.IStateStore? _codeStore;

        private readonly Sha3KeccackHashProvider _keccak;
        private readonly NodeDecoder _decoder;
        private readonly AccountEncoder _accountDecoder;
        internal readonly Stack<HealTask> ToFetch;
        internal readonly Dictionary<string, PendingNode> AwaitingChildren;
        private readonly Dictionary<string, string> _parentOf;
        private readonly HashSet<string> _toFetchKeys;
        private readonly Dictionary<byte[], List<string>> _codeWaiters;
        internal readonly HashSet<byte[]> NeededCode;

        internal HealFrontier(
            HealSession session, HealBatchFetcher fetcher, Nethereum.CoreChain.Storage.IHealNodeSink sink,
            ITrieNodeStore nodeStore, ILogger logger,
            Nethereum.CoreChain.Storage.ISnapFlatStateWriter? flatWriter,
            Nethereum.CoreChain.Storage.IStateStore? codeStore)
        {
            _session = session;
            _stats = session.Stats;
            _fetcher = fetcher;
            _sink = sink;
            _nodeStore = nodeStore;
            _logger = logger;
            _flatWriter = flatWriter;
            _codeStore = codeStore;

            _keccak = Sha3KeccackHashProvider.Instance;
            _decoder = new NodeDecoder();
            _accountDecoder = new AccountEncoder();

            ToFetch = new Stack<HealTask>();
            AwaitingChildren = new Dictionary<string, PendingNode>();
            _parentOf = new Dictionary<string, string>();
            _toFetchKeys = new HashSet<string>();
            _codeWaiters = new Dictionary<byte[], List<string>>(ByteArrayComparer.Current);
            NeededCode = new HashSet<byte[]>(ByteArrayComparer.Current);
        }

        private bool ProbeAndPruneStale(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash)
        {
            if (!isStorage && _session.ForcesDescentAt(nibblePath)) return true;
            switch (_sink.Probe(isStorage, accountHash, nibblePath, expectedHash))
            {
                case Nethereum.CoreChain.Storage.HealNodePresence.Match:
                    _stats.PrunedChildren++;
                    return false;
                case Nethereum.CoreChain.Storage.HealNodePresence.Stale:
                    _stats.RequiredStale++;
                    _sink.DeleteNode(isStorage, accountHash, nibblePath);
                    break;
                default:
                    _stats.RequiredAbsent++;
                    break;
            }
            if (isStorage && accountHash != null && accountHash.Length >= 4)
            {
                var owner = accountHash[0].ToString("x2") + accountHash[1].ToString("x2")
                          + accountHash[2].ToString("x2") + accountHash[3].ToString("x2");
                _stats.StorageOwnerRequires.TryGetValue(owner, out var count);
                _stats.StorageOwnerRequires[owner] = count + 1;
            }
            return true;
        }

        internal List<HealTask> TakeBatch()
        {
            var batch = new List<HealTask>(HealBatchFetcher.BatchSize);
            while (ToFetch.Count > 0 && batch.Count < HealBatchFetcher.BatchSize)
                batch.Add(ToFetch.Pop());
            return batch;
        }

        internal BatchOutcome ProcessResponse(List<HealTask> batch, TrieNodesMessage resp, bool fetchFailed)
        {
            if (fetchFailed) return new BatchOutcome(null, 0, 0);
            int processed = 0;
            long nodesAddedThisRound = 0;
            var (filled, filledHash) = _fetcher.MatchNodesToTasks(batch, resp, _keccak, _session.Round);

            for (int k = 0; k < batch.Count; k++)
            {
                var task = batch[k];
                var blob = filled[k];
                if (blob == null) { ToFetch.Push(task); continue; }

                var earlyExit = ProcessFilledTask(task, blob, filledHash[k], ref nodesAddedThisRound);
                if (earlyExit.HasValue) return new BatchOutcome(earlyExit, processed, nodesAddedThisRound);
                processed++;
            }
            return new BatchOutcome(null, processed, nodesAddedThisRound);
        }

        private HealResult? ProcessFilledTask(HealTask task, byte[] blob, byte[] hash, ref long nodesAddedThisRound)
        {
            DeleteHealRootIfStaleOnFirstFill(task);
            var myKey = HealTask.LocKey(task);
            _toFetchKeys.Remove(myKey);

            var node = _decoder.DecodeFromRlpData(blob, null, new byte[0], decodeHashNodes: false, _nodeStore);

            int missing = CollectChildren(node, task, myKey, _accountDecoder, EnqueueChild, WaitForCode);

            return CommitOrPark(myKey, task, hash, blob, missing, ref nodesAddedThisRound);
        }

        private bool EnqueueChild(HealTask child, string parentKey)
        {
            var childKey = HealTask.LocKey(child);
            if (_toFetchKeys.Contains(childKey) || AwaitingChildren.ContainsKey(childKey)) return false;
            _toFetchKeys.Add(childKey);
            _parentOf[childKey] = parentKey;
            ToFetch.Push(child);
            return true;
        }

        private void WaitForCode(byte[] codeHash, string leafKey)
        {
            if (!_codeWaiters.TryGetValue(codeHash, out var w)) { w = new List<string>(); _codeWaiters[codeHash] = w; }
            w.Add(leafKey);
            NeededCode.Add(codeHash);
        }

        private HealResult? CommitOrPark(
            string myKey, HealTask task, byte[] hash, byte[] blob, int missing, ref long nodesAddedThisRound)
        {
            if (missing == 0)
            {
                nodesAddedThisRound += PersistAndPropagate(myKey, task, hash, blob, AwaitingChildren, _parentOf);
                return null;
            }
            AwaitingChildren[myKey] = new PendingNode(task, hash, blob, missing);
            if (AwaitingChildren.Count > MaxPendingNodes) return NonConvergenceExit(nodesAddedThisRound);
            return null;
        }

        private HealResult NonConvergenceExit(long nodesAddedThisRound)
        {
            _session.TotalNodesFetched += (int)nodesAddedThisRound;
            _sink.Flush();
            _logger.LogError(
                "Heal pending-parent set exceeded {Max} without draining (queue={Queue}) — bailing as non-convergence",
                MaxPendingNodes, ToFetch.Count);
            return _session.NotConverged(_stats);
        }

        private int PersistAndPropagate(
            string key, HealTask task, byte[] hash, byte[] blob,
            Dictionary<string, PendingNode> pending, Dictionary<string, string> parentOf)
        {
            int persisted = 0;
            while (true)
            {
                if (!_sink.HasNode(task.IsStorage, task.AccountHash, task.NibblePath, hash))
                {
                    _sink.PutNode(task.IsStorage, task.AccountHash, task.NibblePath, hash, blob);
                    persisted++;
                }

                if (!task.IsStorage && task.NibblePath is { Length: 1 })
                    _stats.TopBranchesHealed |= 1 << task.NibblePath[0];

                if (!parentOf.TryGetValue(key, out var parentKey)) return persisted;
                parentOf.Remove(key);
                if (!pending.TryGetValue(parentKey, out var parent)) return persisted;
                parent.MissingChildren--;
                if (parent.MissingChildren > 0) return persisted;

                pending.Remove(parentKey);
                key = parentKey;
                task = parent.Task;
                hash = parent.Hash;
                blob = parent.Blob;
            }
        }


        private int CollectChildren(
            Node node, HealTask current, string currentKey, AccountEncoder accountDecoder,
            Func<HealTask, string, bool> enqueueChild, Action<byte[], string> requireCode)
        {
            int missing = 0;

            void RequireChild(HealTask child)
            {
                missing++;
                if (!enqueueChild(child, currentKey))
                    _logger.LogWarning(
                        "Heal: child {Key} already tracked by another parent — parent {Parent} will not commit this epoch",
                        HealTask.LocKey(child), currentKey);
            }

            void RequireCode(byte[] codeHash)
            {
                missing++;
                requireCode(codeHash, currentKey);
            }

            switch (node)
            {
                case BranchNode branch:
                    CollectBranchChildren(branch, current, RequireChild);
                    break;

                case ExtendedNode ext:
                    CollectExtensionChild(ext, current, RequireChild);
                    break;

                case LeafNode leaf:
                    CollectLeafChildren(leaf, current, accountDecoder, RequireChild, RequireCode);
                    break;
            }

            return missing;
        }

        private void CollectBranchChildren(BranchNode branch, HealTask current, Action<HealTask> requireChild)
        {
            for (int i = 0; i < 16; i++)
            {
                var child = branch.Children[i];
                if (child is HashNode bh)
                {
                    var childPath = ByteUtil.AppendByte(current.NibblePath, (byte)i);
                    if (ProbeAndPruneStale(current.IsStorage, current.AccountHash, childPath, bh.Hash))
                        requireChild(current with { NibblePath = childPath, ExpectedHash = bh.Hash });
                }
            }
        }

        private void CollectExtensionChild(ExtendedNode ext, HealTask current, Action<HealTask> requireChild)
        {
            if (ext.InnerNode is HashNode eh)
            {
                for (int i = 1; i < ext.Nibbles.Length; i++)
                    _sink.DeleteNode(current.IsStorage, current.AccountHash, ConcatNibbles(current.NibblePath, ext.Nibbles.Take(i).ToArray()));
                var childPath = ConcatNibbles(current.NibblePath, ext.Nibbles);
                if (ProbeAndPruneStale(current.IsStorage, current.AccountHash, childPath, eh.Hash))
                    requireChild(current with { NibblePath = childPath, ExpectedHash = eh.Hash });
            }
        }

        private void CollectLeafChildren(
            LeafNode leaf, HealTask current, AccountEncoder accountDecoder,
            Action<HealTask> requireChild, Action<byte[]> requireCode)
        {
            if (!current.IsStorage)
            {
                Account account;
                try { account = accountDecoder.Decode(leaf.Value); }
                catch { return; }
                if (account == null) return;

                var fullKeyNibbles = ConcatNibbles(current.NibblePath, leaf.Nibbles);
                if (fullKeyNibbles.Length != 64) return;
                var accountHash = fullKeyNibbles.ConvertFromNibbles();

                _flatWriter?.SaveAccountByHashAsync(accountHash, account).GetAwaiter().GetResult();

                if (_codeStore != null && account.CodeHash != null && account.CodeHash.Length == 32
                    && !ByteUtil.AreEqual(account.CodeHash, DefaultValues.EMPTY_DATA_HASH))
                    requireCode(account.CodeHash);

                if (account.StateRoot == null
                    || ByteUtil.AreEqual(account.StateRoot, DefaultValues.EMPTY_TRIE_HASH))
                {
                    _sink.WipeStorage(accountHash);
                    return;
                }

                if (ProbeAndPruneStale(true, accountHash, Array.Empty<byte>(), account.StateRoot))
                    requireChild(new HealTask(IsStorage: true, AccountHash: accountHash, NibblePath: Array.Empty<byte>(), ExpectedHash: account.StateRoot));
            }
            else if (_flatWriter != null && current.AccountHash != null)
            {
                var fullSlotNibbles = ConcatNibbles(current.NibblePath, leaf.Nibbles);
                if (fullSlotNibbles.Length == 64)
                    _flatWriter.SaveStorageByHashAsync(
                        current.AccountHash, fullSlotNibbles.ConvertFromNibbles(),
                        Nethereum.RLP.RLP.Decode(leaf.Value).RLPData).GetAwaiter().GetResult();
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

        internal bool HasWork => ToFetch.Count > 0 || NeededCode.Count > 0;

        internal void SeedFrontier()
        {
            if (!_session.TargetIsEmptyTrie)
            {
                var t = new HealTask(IsStorage: false, AccountHash: null, NibblePath: Array.Empty<byte>(), ExpectedHash: _session.LiveTargetRoot);
                if (_toFetchKeys.Add(HealTask.LocKey(t))) ToFetch.Push(t);
            }
            if (_session.HasCodeSeeds)
                foreach (var h in _session.CodeSeeds) NeededCode.Add(h);
        }

        private void DeleteSubTrieRootIfStale(HealTask root)
        {
            if (_sink.Probe(root.IsStorage, root.AccountHash, root.NibblePath, root.ExpectedHash) != Nethereum.CoreChain.Storage.HealNodePresence.Stale)
                return;
            _stats.RequiredStale++;
            _sink.DeleteNode(root.IsStorage, root.AccountHash, root.NibblePath);
        }

        private void DeleteHealRootIfStaleOnFirstFill(HealTask filled)
        {
            if (filled.NibblePath.Length != 0) return;
            if (!filled.IsStorage) DeleteSubTrieRootIfStale(filled);
        }

        internal long SatisfyCode(byte[] codeHash)
        {
            NeededCode.Remove(codeHash);
            if (!_codeWaiters.TryGetValue(codeHash, out var leaves)) return 0;
            _codeWaiters.Remove(codeHash);
            long committed = 0;
            foreach (var leafKey in leaves)
            {
                if (!AwaitingChildren.TryGetValue(leafKey, out var leaf)) continue;
                leaf.MissingChildren--;
                if (leaf.MissingChildren > 0) continue;
                AwaitingChildren.Remove(leafKey);
                committed += PersistAndPropagate(leafKey, leaf.Task, leaf.Hash, leaf.Blob, AwaitingChildren, _parentOf);
            }
            return committed;
        }
    }

    internal readonly record struct BatchOutcome(HealResult? EarlyExit, int Processed, long NodesAdded);
}
