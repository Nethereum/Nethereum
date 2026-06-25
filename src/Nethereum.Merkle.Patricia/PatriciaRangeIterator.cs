using System;
using System.Collections.Generic;

namespace Nethereum.Merkle.Patricia
{
    /// <summary>
    /// In-order iteration over a Patricia trie from a given start key, yielding
    /// (key, value) pairs in lex order of the key nibbles. Foundation for the
    /// snap/1 GetAccountRange / GetStorageRanges responses and equivalently for
    /// AppChain fast-follower state bootstrap.
    ///
    /// Keys are 32-byte hashes (keccak256(address) for state, keccak256(slotKey)
    /// for storage). The iterator follows the spec convention: start at the
    /// hash >= startHash, enumerate up to maxCount entries or maxResponseBytes.
    /// </summary>
    public static class PatriciaRangeIterator
    {
        public class RangeEntry
        {
            public byte[] KeyBytes { get; set; }
            public byte[] Value { get; set; }
        }

        /// <summary>
        /// Enumerate trie leaves whose key &gt;= startKey in lex order. HashNodes
        /// are resolved through <paramref name="storage"/> on demand. Limits are
        /// soft caps — the iterator stops *after* it would yield a leaf that
        /// pushes the cumulative response past <paramref name="maxResponseBytes"/>.
        /// </summary>
        public static IEnumerable<RangeEntry> EnumerateRange(
            Node root,
            ITrieStorage storage,
            byte[] startKey,
            int maxCount = int.MaxValue,
            long maxResponseBytes = long.MaxValue)
        {
            if (root == null || root is EmptyNode) yield break;
            if (startKey == null) throw new ArgumentNullException(nameof(startKey));
            if (startKey.Length != 32)
                throw new ArgumentException("startKey must be 32 bytes (state-trie hash)", nameof(startKey));

            var startNibbles = startKey.ConvertToNibbles();
            var pathBuffer = new List<byte>(64);
            int count = 0;
            long bytes = 0;

            foreach (var entry in EnumerateNode(root, storage, startNibbles, 0, true, pathBuffer))
            {
                yield return entry;
                count++;
                bytes += entry.KeyBytes.Length + entry.Value.Length;
                if (count >= maxCount) yield break;
                if (bytes >= maxResponseBytes) yield break;
            }
        }

        private static IEnumerable<RangeEntry> EnumerateNode(
            Node node,
            ITrieStorage storage,
            byte[] startNibbles,
            int startOffset,
            bool boundary,
            List<byte> pathSoFar)
        {
            // Lazily resolve HashNode pointers.
            if (node is HashNode hashNode)
            {
                if (hashNode.InnerNode == null && storage != null)
                    hashNode.DecodeInnerNode(storage, false);
                if (hashNode.InnerNode == null) yield break;
                node = hashNode.InnerNode;
            }
            if (node == null || node is EmptyNode) yield break;

            switch (node)
            {
                case LeafNode leaf:
                {
                    if (boundary)
                    {
                        // Compare leaf's full key (pathSoFar + leaf.Nibbles)
                        // against startNibbles. Skip iff strictly less than start.
                        if (CompareLeafToStart(pathSoFar, leaf.Nibbles, startNibbles) < 0) yield break;
                    }
                    yield return new RangeEntry
                    {
                        KeyBytes = PackPath(pathSoFar, leaf.Nibbles),
                        Value = leaf.Value
                    };
                    yield break;
                }
                case ExtendedNode ext:
                {
                    if (boundary)
                    {
                        var cmp = CompareExtensionToStart(ext.Nibbles, startNibbles, startOffset);
                        if (cmp < 0) yield break;            // whole subtree < start
                        if (cmp > 0) boundary = false;       // whole subtree > start, free walk
                        // cmp == 0: extension nibbles equal corresponding portion of start; stay on boundary
                    }
                    int origLen = pathSoFar.Count;
                    pathSoFar.AddRange(ext.Nibbles);
                    foreach (var e in EnumerateNode(ext.InnerNode, storage, startNibbles, startOffset + ext.Nibbles.Length, boundary, pathSoFar))
                        yield return e;
                    pathSoFar.RemoveRange(origLen, pathSoFar.Count - origLen);
                    yield break;
                }
                case BranchNode branch:
                {
                    int firstChild = 0;
                    if (boundary)
                    {
                        if (startOffset >= startNibbles.Length)
                        {
                            // Start key terminates here. All children are >= start.
                            // Don't yield the branch's own Value: its "key" =
                            // pathSoFar is strictly a prefix of startNibbles,
                            // therefore lex < start.
                            firstChild = 0;
                            boundary = false;
                        }
                        else
                        {
                            firstChild = startNibbles[startOffset];
                        }
                    }
                    // Children with index < firstChild are entirely below start; skip.
                    for (int i = firstChild; i < 16; i++)
                    {
                        var child = branch.Children[i];
                        if (child == null || child is EmptyNode) continue;
                        bool childBoundary = boundary && (i == firstChild);
                        pathSoFar.Add((byte)i);
                        foreach (var e in EnumerateNode(child, storage, startNibbles, startOffset + 1, childBoundary, pathSoFar))
                            yield return e;
                        pathSoFar.RemoveAt(pathSoFar.Count - 1);
                    }
                    yield break;
                }
                default:
                    yield break;
            }
        }

        // Returns sign of (pathSoFar + leafNibbles) vs startNibbles in lex order.
        private static int CompareLeafToStart(List<byte> pathSoFar, byte[] leafNibbles, byte[] startNibbles)
        {
            int total = pathSoFar.Count + leafNibbles.Length;
            int min = Math.Min(total, startNibbles.Length);
            for (int i = 0; i < min; i++)
            {
                byte a = i < pathSoFar.Count ? pathSoFar[i] : leafNibbles[i - pathSoFar.Count];
                byte b = startNibbles[i];
                if (a != b) return a < b ? -1 : 1;
            }
            return total.CompareTo(startNibbles.Length);
        }

        // Returns sign of extension.Nibbles vs startNibbles[startOffset .. startOffset+ext.Length].
        private static int CompareExtensionToStart(byte[] extNibbles, byte[] startNibbles, int startOffset)
        {
            int remainingStart = startNibbles.Length - startOffset;
            int min = Math.Min(extNibbles.Length, remainingStart);
            for (int i = 0; i < min; i++)
            {
                byte a = extNibbles[i];
                byte b = startNibbles[startOffset + i];
                if (a != b) return a < b ? -1 : 1;
            }
            // Prefix-shorter side is considered "smaller" only if the longer side
            // has a strictly greater nibble after the prefix. For boundary
            // semantics we treat the extension as equal-prefix-of-start (cmp=0)
            // when it covers <= remainingStart, otherwise >.
            return extNibbles.Length <= remainingStart ? 0 : 1;
        }

        private static byte[] PackPath(List<byte> pathSoFar, byte[] tail)
        {
            int totalNibbles = pathSoFar.Count + tail.Length;
            if ((totalNibbles & 1) != 0)
                throw new InvalidOperationException(
                    $"Leaf key has odd nibble length {totalNibbles}; state-trie keys must be 64 nibbles (32 bytes)");
            var allNibbles = new byte[totalNibbles];
            for (int i = 0; i < pathSoFar.Count; i++) allNibbles[i] = pathSoFar[i];
            for (int i = 0; i < tail.Length; i++) allNibbles[pathSoFar.Count + i] = tail[i];
            return allNibbles.ConvertFromNibbles();
        }
    }
}
