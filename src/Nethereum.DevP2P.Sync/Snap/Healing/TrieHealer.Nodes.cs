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
                    _topBranchesHealed |= 1 << task.NibblePath[0];

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
                        LocKey(child), currentKey);
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
                    missing += CollectLeafChildren(leaf, current, currentKey, accountDecoder, RequireChild, requireCode);
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
                    if (ProbeAndCount(current.IsStorage, current.AccountHash, childPath, bh.Hash))
                        requireChild(current with { NibblePath = childPath, ExpectedHash = bh.Hash });
                }
            }
        }

        private void CollectExtensionChild(ExtendedNode ext, HealTask current, Action<HealTask> requireChild)
        {
            if (ext.InnerNode is HashNode eh)
            {
                var childPath = ConcatNibbles(current.NibblePath, ext.Nibbles);
                if (ProbeAndCount(current.IsStorage, current.AccountHash, childPath, eh.Hash))
                    requireChild(current with { NibblePath = childPath, ExpectedHash = eh.Hash });
            }
        }

        private int CollectLeafChildren(
            LeafNode leaf, HealTask current, string currentKey, AccountEncoder accountDecoder,
            Action<HealTask> requireChild, Action<byte[], string> requireCode)
        {
            int missing = 0;
            if (!current.IsStorage)
            {
                Account account;
                try { account = accountDecoder.Decode(leaf.Value); }
                catch { return missing; }
                if (account == null) return missing;

                var fullKeyNibbles = ConcatNibbles(current.NibblePath, leaf.Nibbles);
                if (fullKeyNibbles.Length != 64) return missing;
                var accountHash = fullKeyNibbles.ConvertFromNibbles();

                _flatWriter?.SaveAccountByHashAsync(accountHash, account).GetAwaiter().GetResult();

                if (_codeStore != null && account.CodeHash != null && account.CodeHash.Length == 32
                    && !ByteUtil.AreEqual(account.CodeHash, DefaultValues.EMPTY_DATA_HASH))
                {
                    missing++;
                    requireCode(account.CodeHash, currentKey);
                }

                if (account.StateRoot == null
                    || ByteUtil.AreEqual(account.StateRoot, DefaultValues.EMPTY_TRIE_HASH))
                {
                    _sink.WipeStorage(accountHash);
                    return missing;
                }

                if (ProbeAndCount(true, accountHash, Array.Empty<byte>(), account.StateRoot))
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
            return missing;
        }

        private static byte[] ConcatNibbles(byte[] a, byte[] b)
        {
            if (b == null || b.Length == 0) return a;
            var copy = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, copy, 0, a.Length);
            Buffer.BlockCopy(b, 0, copy, a.Length, b.Length);
            return copy;
        }
    }
}
