using System;
using System.Collections.Generic;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Patricia
{
    /// <summary>
    /// Result of a Patricia range-proof verification.
    /// </summary>
    public readonly struct RangeProofResult
    {
        /// <summary>True if the supplied (keys, values, edge proofs) hash up to the expected root.</summary>
        public bool Valid { get; }

        /// <summary>
        /// True if the proof witnesses that more entries exist to the right of the last
        /// returned key. Snap-sync uses this flag to decide whether to continue fetching
        /// the next chunk of the same account or storage range.
        /// </summary>
        public bool HasMore { get; }

        public RangeProofResult(bool valid, bool hasMore)
        {
            Valid = valid;
            HasMore = hasMore;
        }

        public static readonly RangeProofResult Invalid = new RangeProofResult(false, false);
    }

    /// <summary>
    /// Verifies Patricia Merkle Trie range proofs as defined by EIP-2718 / snap/1
    /// (see <c>github.com/ethereum/devp2p/blob/master/caps/snap.md</c>).
    ///
    /// The implementation follows the standard Merkle-Patricia range-proof
    /// verification algorithm (<c>VerifyRangeProof</c>, <c>proofToPath</c>,
    /// <c>unsetInternal</c>, <c>unset</c>, <c>hasRightElement</c>); node types
    /// are mapped onto Nethereum.Merkle.Patricia's split <see cref="LeafNode"/> /
    /// <see cref="ExtendedNode"/> equivalents of a single combined short node.
    /// </summary>
    public static class PatriciaRangeProofVerifier
    {
        /// <summary>
        /// Lightweight single-key boundary check: confirms one entry resolves to its
        /// expected value through the given proof under the expected root. Useful as
        /// a fast pre-acceptance check on a single snap-sync boundary, but not a
        /// completeness proof — see <see cref="VerifyRangeProof"/> for the trustless
        /// reconstruction algorithm.
        /// </summary>
        public static bool VerifyEntryAgainstRoot(byte[] root, byte[] keyHash, byte[] expectedValue, IList<byte[]> proof)
        {
            if (proof == null || proof.Count == 0) return false;
            var hashProvider = new Sha3KeccackHashProvider();
            var storage = new InMemoryTrieStorage();
            foreach (var node in proof) storage.Put(hashProvider.ComputeHash(node), node);

            var trie = new PatriciaTrie(root);
            byte[] found;
            try { found = trie.Get(keyHash, storage); }
            catch { return false; }

            if (found == null) return false;
            if (expectedValue.Length != found.Length) return false;
            for (int i = 0; i < found.Length; i++)
                if (found[i] != expectedValue[i]) return false;
            return true;
        }

        /// <summary>
        /// Verifies that <paramref name="keys"/>/<paramref name="values"/> are the
        /// complete, consecutive set of leaves in the trie rooted at
        /// <paramref name="rootHash"/> over the range [<paramref name="firstKey"/>,
        /// keys[keys.Count-1]]. The two edge proofs (concatenated into
        /// <paramref name="proofNodes"/>) witness the boundary paths from the
        /// root to the would-be position of firstKey and lastKey.
        ///
        /// <para>
        /// Range-proof verification entry point. Special cases:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>If <paramref name="proofNodes"/> is null/empty, <paramref name="keys"/> are
        ///   expected to be the entire leaf-set of the trie (no edge proof needed).</description></item>
        ///   <item><description>If <paramref name="keys"/> is empty but a single non-existence
        ///   edge proof is given, succeeds only when the trie has no entries
        ///   ≥ firstKey.</description></item>
        ///   <item><description>If there is exactly one element and firstKey == that key, the
        ///   single edge proof is used and HasMore is derived from the proof.</description></item>
        /// </list>
        ///
        /// <para>
        /// Returns <see cref="RangeProofResult.Valid"/>=true and
        /// <see cref="RangeProofResult.HasMore"/>=true/false on success, or
        /// <see cref="RangeProofResult.Invalid"/> on any verification failure.
        /// </para>
        /// </summary>
        public static RangeProofResult VerifyRangeProof(
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

            // Monotonicity / no-deletion / no-prefix-expansion checks.
            for (int i = 0; i < keys.Count; i++)
            {
                if (i < keys.Count - 1)
                {
                    if (CompareBytes(keys[i], keys[i + 1]) >= 0) return RangeProofResult.Invalid;
                    if (HasPrefix(keys[i + 1], keys[i])) return RangeProofResult.Invalid;
                }
                if (values[i] == null || values[i].Length == 0) return RangeProofResult.Invalid;
            }

            var hashProvider = new Sha3KeccackHashProvider();

            // Special case 1: no edge proof at all — the supplied (keys, values) must
            // be the entire trie. Reconstruct from scratch and compare roots.
            if (proofNodes == null || proofNodes.Count == 0)
            {
                var fresh = new PatriciaTrie();
                for (int i = 0; i < keys.Count; i++) fresh.Put(keys[i], values[i]);
                var have = fresh.Root.GetHash();
                if (!BytesEqual(have, rootHash)) return RangeProofResult.Invalid;
                return new RangeProofResult(true, false); // No more elements
            }

            var proofDb = BuildProofStorage(proofNodes, hashProvider);

            // Special case 2: edge proof present but zero entries — must be a
            // non-existence proof for firstKey and the trie must have nothing
            // strictly greater than firstKey.
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

            // Special case 3: single element where firstKey == lastKey == keys[0].
            // The edge proof here is a single existence/non-existence path.
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

            // General case: two edge proofs.
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

            Node rebuildRoot = empty ? new EmptyNode() : mergedRoot;
            // Mark the root and its in-place modified path dirty so cached
            // hashes are invalidated and recomputed bottom-up after the rebuild.
            ForceDirty(rebuildRoot);

            var rebuilt = new PatriciaTrie(rebuildRoot);
            for (int i = 0; i < keys.Count; i++) rebuilt.Put(keys[i], values[i]);
            var computed = rebuilt.Root.GetHash();
            if (!BytesEqual(computed, rootHash)) return RangeProofResult.Invalid;
            return new RangeProofResult(true, HasRightElement(rebuilt.Root, keys[keys.Count - 1]));
        }

        // ---------- proofToPath ----------
        // Decodes the edge-proof bytes referenced by walking the key path, links
        // resolved children into <paramref name="root"/> so the trie structure
        // along the path is materialized in-memory. Returns the (possibly updated)
        // root and, if the key terminates at a value, that value.
        private static Node ProofToPath(
            byte[] rootHash,
            Node root,
            byte[] keyHex,
            ITrieStorage proofDb,
            bool allowNonExistent,
            out byte[] value)
        {
            Node ResolveNode(byte[] hash)
            {
                var rlp = proofDb.Get(hash);
                if (rlp == null)
                    throw new RangeProofException($"proof node (hash {Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(hash)}) missing");
                var n = new NodeDecoder().DecodeNodeFromRlpData(rlp, false, proofDb);
                if (n == null) throw new RangeProofException("bad proof node");
                return n;
            }

            if (root == null) root = ResolveNode(rootHash);

            var parent = root;
            int pos = 0;
            while (true)
            {
                // Walk one step from `parent` consuming nibbles from keyHex[pos..].
                // The walk step get(parent, key) returns
                //   (keyrest, child) where parent's edge to child has just been
                //   traversed (one nibble for fullNode, full extension nibbles
                //   for shortNode). We reproduce that one-step descent here.
                if (parent is BranchNode branch)
                {
                    if (pos >= keyHex.Length)
                        throw new RangeProofException("key shorter than trie depth");
                    var nibble = keyHex[pos];
                    if (nibble == 16)
                    {
                        // Terminator landed at branch's value slot — return its
                        // value (potentially null/empty for branch with no value).
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

                    // Resolve hashNode children in place.
                    if (child is HashNode hn)
                    {
                        var resolved = ResolveNode(hn.Hash);
                        branch.SetChild(consumedNibble, resolved);
                        child = resolved;
                    }

                    // Descend.
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
                    // This is a short node with a value child. The leaf's
                    // Nibbles must be the path from current pos minus the
                    // terminator nibble.
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
                    // Root itself was a HashNode (rare — would mean root proof node
                    // is wrapped). Resolve and retry.
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

        // ---------- unsetInternal ----------
        // Walks from the root down to the fork point (where the leftKey and
        // rightKey paths diverge), clearing all internal references inside
        // [leftKey, rightKey]. After this call, the keys returned by the
        // rebuild loop will fill the cleared interior. Returns true if the
        // entire trie should be empty (caller sets root = nil).
        private static bool UnsetInternal(Node n, byte[] left, byte[] right)
        {
            left = KeyBytesToHex(left);
            right = KeyBytesToHex(right);

            int pos = 0;
            Node parent = null;
            int shortForkLeft = 0;
            int shortForkRight = 0;

            // Walk to the fork point.
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
                    // shortNode whose Val is a value. We can only land here if
                    // both paths' nibbles match the leaf exactly so far. If
                    // either fork doesn't match, we use the same shortFork
                    // handling as for ExtendedNode.
                    lf.MarkDirty();
                    shortForkLeft = CompareNibbles(left, pos, lf.Nibbles);
                    shortForkRight = CompareNibbles(right, pos, lf.Nibbles);
                    break; // leaf always terminates fork search
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

            // Fork-point handling.
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
                // Only one proof points to non-existent key.
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
                // A short node with a value child — same five cases apply, but
                // since the child is a value, the "unset shortNode entirely"
                // branches cause parent's child to be cleared (or whole-trie
                // unset). For non-existent-leaf cases, no internal references
                // exist to clear within the leaf itself.
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

        // ---------- unset ----------
        // Removes all internal node references on the "removeLeft" or "removeRight"
        // side of <paramref name="child"/> relative to <paramref name="key"/> at
        // <paramref name="pos"/>.
        private static void Unset(Node parent, Node child, byte[] key, int pos, bool removeLeft)
        {
            if (child is BranchNode cld)
            {
                if (pos >= key.Length)
                {
                    // Should not happen for monotonic 32-byte keys, but guard anyway.
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
                    // Terminator nibble at this depth refers to branch.Value slot;
                    // no recursive child to unset.
                    return;
                }
                Unset(cld, cld.Children[p], key, pos + 1, removeLeft);
                return;
            }

            if (child is ExtendedNode extCld)
            {
                if (!StartsWith(key, pos, extCld.Nibbles))
                {
                    // Fork shortnode.
                    int cmp = CompareNibbles(extCld.Nibbles, 0, key, pos);
                    if (removeLeft)
                    {
                        if (cmp < 0)
                        {
                            ((BranchNode)parent).RemoveChild(key[pos - 1]);
                        }
                        // else: keep with cached hash
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
                // Leaf matches — unset the parent's link because the leaf is
                // a value terminal that falls inside [leftKey, rightKey] and
                // will be re-inserted by the rebuild Put loop.
                ((BranchNode)parent).RemoveChild(key[pos - 1]);
                return;
            }

            if (child == null)
            {
                // Child of fork-point fullnode is nil: non-existent branch.
                return;
            }

            throw new RangeProofException($"unexpected node in unset: {child.GetType().Name}");
        }

        // ---------- hasRightElement ----------
        // Walks the path of <paramref name="key"/> from <paramref name="root"/>,
        // returning true if any branch along the way has a sibling child to the
        // right of the walked nibble.
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
                    return false; // value resolved
                }
                if (node is HashNode hn)
                {
                    // hashNode here would mean an unresolved child outside the edge
                    // proof path — treat as opaque "no info" → no right element on
                    // this descent. (We soft-fail to false on this opaque case.)
                    return false;
                }
                return false;
            }
            return false;
        }

        // ---------- helpers ----------

        private static InMemoryTrieStorage BuildProofStorage(IList<byte[]> proofNodes, IHashProvider hashProvider)
        {
            var s = new InMemoryTrieStorage();
            for (int i = 0; i < proofNodes.Count; i++)
            {
                var raw = proofNodes[i];
                s.Put(hashProvider.ComputeHash(raw), raw);
            }
            return s;
        }

        /// <summary>
        /// <c>keybytesToHex</c>: expand each byte into its two hex
        /// nibbles and append the terminator nibble <c>0x10</c>. This is the
        /// representation the verifier walks in <see cref="ProofToPath"/>,
        /// <see cref="UnsetInternal"/>, <see cref="Unset"/> and
        /// <see cref="HasRightElement"/>. The terminator lets the algorithm
        /// distinguish branch-value vs branch-child reads at the boundary
        /// without special casing.
        /// </summary>
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
            if (remainingHex > nibbles.Length) return 1;
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

    /// <summary>
    /// Thrown internally when a range proof is structurally malformed. Callers of
    /// <see cref="PatriciaRangeProofVerifier.VerifyRangeProof"/> never see this —
    /// the verifier catches it and reports <see cref="RangeProofResult.Invalid"/>.
    /// </summary>
    internal sealed class RangeProofException : Exception
    {
        public RangeProofException(string message) : base(message) { }
    }
}
