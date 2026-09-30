using Nethereum.Documentation;
using System;
using System.Collections.Generic;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Nodes.Rlp;
namespace Nethereum.Merkle.Patricia.ProofVerification
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "snap-range-proofs", "The verdict a snap range proof returns")]
    public readonly struct RangeProofResult
    {
        public bool Valid { get; }

        public bool HasMore { get; }

        public RangeProofResult(bool valid, bool hasMore)
        {
            Valid = valid;
            HasMore = hasMore;
        }

        public static readonly RangeProofResult Invalid = new RangeProofResult(false, false);
    }

    public interface IRangeProofVerifier
    {
        RangeProofResult Verify(byte[] rootHash, byte[] firstKey, IList<byte[]> keys, IList<byte[]> values, IList<byte[]> proofNodes);
        bool VerifyEntry(byte[] root, byte[] keyHash, byte[] expectedValue, IList<byte[]> proof);
    }

    public class PatriciaRangeProofVerifier : IRangeProofVerifier
    {
        public static PatriciaRangeProofVerifier Current { get; } = new PatriciaRangeProofVerifier();

        public bool VerifyEntry(byte[] root, byte[] keyHash, byte[] expectedValue, IList<byte[]> proof)
        {
            if (proof == null || proof.Count == 0) return false;
            var hashProvider = Sha3KeccackHashProvider.Instance;
            var storage = new InMemoryContentNodeStore();
            foreach (var node in proof) storage.Put(hashProvider.ComputeHash(node), node);

            var trie = new PatriciaTrie(root, storage);
            byte[] found;
            try { found = trie.Get(keyHash); }
            catch { return false; }

            if (found == null) return false;
            if (expectedValue.Length != found.Length) return false;
            for (int i = 0; i < found.Length; i++)
                if (found[i] != expectedValue[i]) return false;
            return true;
        }

        public RangeProofResult Verify(
            byte[] rootHash,
            byte[] firstKey,
            IList<byte[]> keys,
            IList<byte[]> values,
            IList<byte[]> proofNodes)
        {
            if (rootHash == null) throw new ArgumentNullException(nameof(rootHash));
            if (firstKey == null) throw new ArgumentNullException(nameof(firstKey));
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (keys.Count != values.Count) return RangeProofResult.Invalid;

            for (int i = 0; i < keys.Count; i++)
            {
                if (i < keys.Count - 1)
                {
                    if (CompareBytes(keys[i], keys[i + 1]) >= 0) return RangeProofResult.Invalid;
                    if (HasPrefix(keys[i + 1], keys[i])) return RangeProofResult.Invalid;
                }
                if (values[i] == null || values[i].Length == 0) return RangeProofResult.Invalid;
            }

            var hashProvider = Sha3KeccackHashProvider.Instance;

            if (proofNodes == null || proofNodes.Count == 0)
            {
                var fresh = new PatriciaTrie();
                for (int i = 0; i < keys.Count; i++) fresh.Put(keys[i], values[i]);
                var have = fresh.Root.GetHash();
                if (!BytesEqual(have, rootHash)) return RangeProofResult.Invalid;
                return new RangeProofResult(true, false);
            }

            var proofDb = BuildProofStorage(proofNodes, hashProvider);

            if (keys.Count == 0)
            {
                Node root;
                byte[] val;
                try
                {
                    root = ProofToPath(rootHash, null, KeyBytesToHex(firstKey), proofDb, allowNonExistent: true, out val);
                }
                catch { return RangeProofResult.Invalid; }
                if (val != null || HasRightElement(root, firstKey)) return RangeProofResult.Invalid;
                return new RangeProofResult(true, false);
            }

            var lastKey = keys[keys.Count - 1];

            if (keys.Count == 1 && BytesEqual(firstKey, lastKey))
            {
                Node root;
                byte[] val;
                try
                {
                    root = ProofToPath(rootHash, null, KeyBytesToHex(firstKey), proofDb, allowNonExistent: false, out val);
                }
                catch { return RangeProofResult.Invalid; }
                if (!BytesEqual(firstKey, keys[0])) return RangeProofResult.Invalid;
                if (val == null || !BytesEqual(val, values[0])) return RangeProofResult.Invalid;
                return new RangeProofResult(true, HasRightElement(root, firstKey));
            }

            if (CompareBytes(firstKey, lastKey) >= 0) return RangeProofResult.Invalid;
            if (firstKey.Length != lastKey.Length) return RangeProofResult.Invalid;

            Node leftRoot;
            try
            {
                leftRoot = ProofToPath(rootHash, null, KeyBytesToHex(firstKey), proofDb, allowNonExistent: true, out _);
            }
            catch { return RangeProofResult.Invalid; }

            Node mergedRoot;
            try
            {
                mergedRoot = ProofToPath(rootHash, leftRoot, KeyBytesToHex(lastKey), proofDb, allowNonExistent: true, out _);
            }
            catch { return RangeProofResult.Invalid; }

            bool empty;
            try
            {
                empty = UnsetInternal(mergedRoot, firstKey, lastKey);
            }
            catch { return RangeProofResult.Invalid; }

            Node rebuildRoot = empty ? EmptyNode.Instance : mergedRoot;
            ForceDirty(rebuildRoot);

            var rebuilt = new PatriciaTrie(rebuildRoot);
            for (int i = 0; i < keys.Count; i++) rebuilt.Put(keys[i], values[i]);
            var computed = rebuilt.Root.GetHash();
            if (!BytesEqual(computed, rootHash)) return RangeProofResult.Invalid;
            return new RangeProofResult(true, HasRightElement(rebuilt.Root, keys[keys.Count - 1]));
        }

        private static Node ProofToPath(
            byte[] rootHash,
            Node root,
            byte[] keyHex,
            INodeBlobStore proofDb,
            bool allowNonExistent,
            out byte[] value)
        {
            Node ResolveNode(byte[] hash)
            {
                var rlp = proofDb.Get(hash);
                if (rlp == null)
                    throw new RangeProofException($"proof node (hash {Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(hash)}) missing");
                var n = new NodeDecoder().DecodeFromRlpData(rlp, null, new byte[0], false, new ContentAddressedNodeStore(proofDb));
                if (n == null) throw new RangeProofException("bad proof node");
                return n;
            }

            if (root == null) root = ResolveNode(rootHash);

            var parent = root;
            int pos = 0;
            while (true)
            {
                if (parent is BranchNode branch)
                {
                    if (pos >= keyHex.Length)
                        throw new RangeProofException("key shorter than trie depth");
                    var nibble = keyHex[pos];
                    if (nibble == 16)
                    {
                        value = branch.Value != null && branch.Value.Length > 0 ? branch.Value : null;
                        return root;
                    }
                    var child = branch.Children[nibble];
                    var consumedNibble = nibble;
                    pos += 1;

                    if (child == null)
                    {
                        if (allowNonExistent) { value = null; return root; }
                        throw new RangeProofException("the node is not contained in trie");
                    }

                    if (child is HashNode hn)
                    {
                        var resolved = ResolveNode(hn.Hash);
                        branch.SetChild(consumedNibble, resolved);
                        child = resolved;
                    }

                    parent = child;
                    continue;
                }

                if (parent is ExtendedNode ext)
                {
                    if (!StartsWith(keyHex, pos, ext.Nibbles))
                    {
                        if (allowNonExistent) { value = null; return root; }
                        throw new RangeProofException("the node is not contained in trie");
                    }
                    var child = ext.InnerNode;
                    pos += ext.Nibbles.Length;

                    if (child == null)
                    {
                        if (allowNonExistent) { value = null; return root; }
                        throw new RangeProofException("the node is not contained in trie");
                    }

                    if (child is HashNode hn)
                    {
                        var resolved = ResolveNode(hn.Hash);
                        ext.InnerNode = resolved;
                        child = resolved;
                    }

                    parent = child;
                    continue;
                }

                if (parent is LeafNode leaf)
                {
                    if (!StartsWith(keyHex, pos, leaf.Nibbles))
                    {
                        if (allowNonExistent) { value = null; return root; }
                        throw new RangeProofException("the node is not contained in trie");
                    }
                    pos += leaf.Nibbles.Length;
                    if (pos != keyHex.Length - 1 || keyHex[pos] != 16)
                    {
                        if (allowNonExistent) { value = null; return root; }
                        throw new RangeProofException("the node is not contained in trie");
                    }
                    value = leaf.Value;
                    return root;
                }

                if (parent is HashNode topHash)
                {
                    var resolved = ResolveNode(topHash.Hash);
                    parent = resolved;
                    if (ReferenceEquals(root, topHash)) root = resolved;
                    continue;
                }

                if (parent is EmptyNode || parent == null)
                {
                    if (allowNonExistent) { value = null; return root; }
                    throw new RangeProofException("the node is not contained in trie");
                }

                throw new RangeProofException($"invalid node type: {parent.GetType().Name}");
            }
        }

        private static bool UnsetInternal(Node n, byte[] left, byte[] right)
        {
            left = KeyBytesToHex(left);
            right = KeyBytesToHex(right);

            int pos = 0;
            Node parent = null;
            int shortForkLeft = 0;
            int shortForkRight = 0;

            while (true)
            {
                if (n is ExtendedNode rn)
                {
                    rn.MarkDirty();
                    shortForkLeft = CompareNibbles(left, pos, rn.Nibbles);
                    shortForkRight = CompareNibbles(right, pos, rn.Nibbles);
                    if (shortForkLeft != 0 || shortForkRight != 0) break;
                    parent = n;
                    n = rn.InnerNode;
                    pos += rn.Nibbles.Length;
                    continue;
                }
                if (n is LeafNode lf)
                {
                    lf.MarkDirty();
                    shortForkLeft = CompareNibbles(left, pos, lf.Nibbles);
                    shortForkRight = CompareNibbles(right, pos, lf.Nibbles);
                    break;
                }
                if (n is BranchNode bn)
                {
                    bn.MarkDirty();
                    var leftNibble = left[pos];
                    var rightNibble = right[pos];
                    Node leftChild = leftNibble == 16 ? null : bn.Children[leftNibble];
                    Node rightChild = rightNibble == 16 ? null : bn.Children[rightNibble];
                    if (leftChild == null || rightChild == null || !ReferenceEquals(leftChild, rightChild))
                        break;
                    parent = n;
                    n = leftChild;
                    pos += 1;
                    continue;
                }
                throw new RangeProofException($"invalid node at fork-walk: {(n == null ? "null" : n.GetType().Name)}");
            }

            if (n is ExtendedNode rnFork)
            {
                if (shortForkLeft == -1 && shortForkRight == -1)
                    throw new RangeProofException("empty range");
                if (shortForkLeft == 1 && shortForkRight == 1)
                    throw new RangeProofException("empty range");
                if (shortForkLeft != 0 && shortForkRight != 0)
                {
                    if (parent == null) return true;
                    ((BranchNode)parent).RemoveChild(left[pos - 1]);
                    return false;
                }
                if (shortForkRight != 0)
                {
                    Unset(rnFork, rnFork.InnerNode, left, pos + rnFork.Nibbles.Length, removeLeft: false);
                    return false;
                }
                if (shortForkLeft != 0)
                {
                    Unset(rnFork, rnFork.InnerNode, right, pos + rnFork.Nibbles.Length, removeLeft: true);
                    return false;
                }
                return false;
            }

            if (n is LeafNode lfFork)
            {
                if (shortForkLeft == -1 && shortForkRight == -1)
                    throw new RangeProofException("empty range");
                if (shortForkLeft == 1 && shortForkRight == 1)
                    throw new RangeProofException("empty range");
                if (shortForkLeft != 0 && shortForkRight != 0)
                {
                    if (parent == null) return true;
                    ((BranchNode)parent).RemoveChild(left[pos - 1]);
                    return false;
                }
                if (shortForkRight != 0)
                {
                    if (parent == null) return true;
                    ((BranchNode)parent).RemoveChild(left[pos - 1]);
                    return false;
                }
                if (shortForkLeft != 0)
                {
                    if (parent == null) return true;
                    ((BranchNode)parent).RemoveChild(right[pos - 1]);
                    return false;
                }
                return false;
            }

            if (n is BranchNode bnFork)
            {
                var leftP = left[pos];
                var rightP = right[pos];
                for (int i = leftP + 1; i < rightP; i++) bnFork.RemoveChild(i);
                Unset(bnFork, leftP == 16 ? null : bnFork.Children[leftP], left, pos + 1, removeLeft: false);
                Unset(bnFork, rightP == 16 ? null : bnFork.Children[rightP], right, pos + 1, removeLeft: true);
                return false;
            }

            throw new RangeProofException($"invalid fork node: {(n == null ? "null" : n.GetType().Name)}");
        }

        private static void Unset(Node parent, Node child, byte[] key, int pos, bool removeLeft)
        {
            if (child is BranchNode cld)
            {
                if (pos >= key.Length)
                {
                    cld.MarkDirty();
                    return;
                }
                var p = key[pos];
                if (removeLeft)
                {
                    for (int i = 0; i < p; i++) cld.RemoveChild(i);
                    cld.MarkDirty();
                }
                else
                {
                    for (int i = p + 1; i < 16; i++) cld.RemoveChild(i);
                    cld.MarkDirty();
                }
                if (p == 16)
                {
                    return;
                }
                Unset(cld, cld.Children[p], key, pos + 1, removeLeft);
                return;
            }

            if (child is ExtendedNode extCld)
            {
                if (!StartsWith(key, pos, extCld.Nibbles))
                {
                    int cmp = CompareNibbles(extCld.Nibbles, 0, key, pos);
                    if (removeLeft)
                    {
                        if (cmp < 0)
                        {
                            ((BranchNode)parent).RemoveChild(key[pos - 1]);
                        }
                    }
                    else
                    {
                        if (cmp > 0)
                        {
                            ((BranchNode)parent).RemoveChild(key[pos - 1]);
                        }
                    }
                    return;
                }
                extCld.MarkDirty();
                Unset(extCld, extCld.InnerNode, key, pos + extCld.Nibbles.Length, removeLeft);
                return;
            }

            if (child is LeafNode lfCld)
            {
                if (!StartsWith(key, pos, lfCld.Nibbles))
                {
                    int cmp = CompareNibbles(lfCld.Nibbles, 0, key, pos);
                    if (removeLeft)
                    {
                        if (cmp < 0)
                        {
                            ((BranchNode)parent).RemoveChild(key[pos - 1]);
                        }
                    }
                    else
                    {
                        if (cmp > 0)
                        {
                            ((BranchNode)parent).RemoveChild(key[pos - 1]);
                        }
                    }
                    return;
                }
                ((BranchNode)parent).RemoveChild(key[pos - 1]);
                return;
            }

            if (child == null)
            {
                return;
            }

            throw new RangeProofException($"unexpected node in unset: {child.GetType().Name}");
        }

        private static bool HasRightElement(Node node, byte[] key)
        {
            var keyHex = KeyBytesToHex(key);
            int pos = 0;
            while (node != null && !(node is EmptyNode))
            {
                if (node is BranchNode bn)
                {
                    if (pos >= keyHex.Length) return false;
                    var p = keyHex[pos];
                    if (p == 16) return false;
                    for (int i = p + 1; i < 16; i++)
                    {
                        if (bn.Children[i] != null) return true;
                    }
                    node = bn.Children[p];
                    pos += 1;
                    continue;
                }
                if (node is ExtendedNode ext)
                {
                    if (!StartsWith(keyHex, pos, ext.Nibbles))
                    {
                        return CompareNibbles(ext.Nibbles, 0, keyHex, pos) > 0;
                    }
                    node = ext.InnerNode;
                    pos += ext.Nibbles.Length;
                    continue;
                }
                if (node is LeafNode lf)
                {
                    if (!StartsWith(keyHex, pos, lf.Nibbles))
                    {
                        return CompareNibbles(lf.Nibbles, 0, keyHex, pos) > 0;
                    }
                    return false;
                }
                if (node is HashNode hn)
                {
                    return false;
                }
                return false;
            }
            return false;
        }


        private static InMemoryContentNodeStore BuildProofStorage(IList<byte[]> proofNodes, IHashProvider hashProvider)
        {
            var s = new InMemoryContentNodeStore();
            for (int i = 0; i < proofNodes.Count; i++)
            {
                var raw = proofNodes[i];
                s.Put(hashProvider.ComputeHash(raw), raw);
            }
            return s;
        }

        internal static byte[] KeyBytesToHex(byte[] key)
        {
            var hex = new byte[key.Length * 2 + 1];
            for (int i = 0; i < key.Length; i++)
            {
                hex[i * 2] = (byte)((key[i] >> 4) & 0x0F);
                hex[i * 2 + 1] = (byte)(key[i] & 0x0F);
            }
            hex[hex.Length - 1] = 16;
            return hex;
        }

        private static bool StartsWith(byte[] hex, int pos, byte[] nibbles)
        {
            if (pos + nibbles.Length > hex.Length) return false;
            for (int i = 0; i < nibbles.Length; i++)
            {
                if (hex[pos + i] != nibbles[i]) return false;
            }
            return true;
        }

        private static int CompareNibbles(byte[] hex, int pos, byte[] nibbles)
        {
            int compareLen = Math.Min(hex.Length - pos, nibbles.Length);
            for (int i = 0; i < compareLen; i++)
            {
                if (hex[pos + i] < nibbles[i]) return -1;
                if (hex[pos + i] > nibbles[i]) return 1;
            }
            int remainingHex = hex.Length - pos;
            if (remainingHex < nibbles.Length) return -1;
            return 0;
        }

        private static int CompareNibbles(byte[] a, int aPos, byte[] b, int bPos)
        {
            int aLen = a.Length - aPos;
            int bLen = b.Length - bPos;
            int compareLen = Math.Min(aLen, bLen);
            for (int i = 0; i < compareLen; i++)
            {
                if (a[aPos + i] < b[bPos + i]) return -1;
                if (a[aPos + i] > b[bPos + i]) return 1;
            }
            if (aLen < bLen) return -1;
            if (aLen > bLen) return 1;
            return 0;
        }

        private static int CompareBytes(byte[] a, byte[] b)
        {
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++)
            {
                if (a[i] < b[i]) return -1;
                if (a[i] > b[i]) return 1;
            }
            return a.Length.CompareTo(b.Length);
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static bool HasPrefix(byte[] s, byte[] prefix)
        {
            if (prefix.Length > s.Length) return false;
            for (int i = 0; i < prefix.Length; i++) if (s[i] != prefix[i]) return false;
            return true;
        }

        private static void ForceDirty(Node node)
        {
            if (node == null || node is EmptyNode) return;
            node.MarkDirty();
            if (node is BranchNode bn)
            {
                for (int i = 0; i < bn.Children.Length; i++) ForceDirty(bn.Children[i]);
            }
            else if (node is ExtendedNode ext)
            {
                ForceDirty(ext.InnerNode);
            }
            else if (node is HashNode hn)
            {
                ForceDirty(hn.InnerNode);
            }
        }
    }

    internal sealed class RangeProofException : Exception
    {
        public RangeProofException(string message) : base(message) { }
    }
}
