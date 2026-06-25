using System;
using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.Merkle.Patricia
{
    /// <summary>
    /// Generates an "edge proof" for a [startKey, lastReturnedKey] range of a
    /// Patricia trie. The proof is the set of RLP-encoded trie nodes that
    /// witness the boundary paths from the root to the would-be position of
    /// startKey and lastReturnedKey.
    ///
    /// Combined with the range of (key, value) entries returned by
    /// <see cref="PatriciaRangeIterator"/>, a verifier can reconstruct the
    /// relevant subtrie and check its root against the expected state root.
    ///
    /// Spec: https://github.com/ethereum/devp2p/blob/master/caps/snap.md
    /// "AccountRange / StorageRanges proof": list of RLP-encoded trie nodes
    /// that prove the response is the complete set of accounts/slots in
    /// [startHash, lastReturnedHash] under the given root.
    /// </summary>
    public static class PatriciaRangeProofGenerator
    {
        /// <summary>
        /// Generate the edge proof for a single-bound range (just startKey).
        /// Used when the receiver requested a range but the trie is small
        /// enough that the whole returned slice has only one boundary.
        /// </summary>
        public static List<byte[]> GenerateProof(
            Node root,
            ITrieStorage storage,
            byte[] startKey)
        {
            if (startKey == null) throw new ArgumentNullException(nameof(startKey));
            var collector = new InMemoryTrieStorage();
            CollectPathToKey(root, storage, startKey.ConvertToNibbles(), collector);
            return new List<byte[]>(collector.Storage.Values);
        }

        /// <summary>
        /// Generate the edge proof for a [startKey, lastReturnedKey] range.
        /// Combines the path-to-start and path-to-lastReturned into a single
        /// deduplicated proof bundle.
        /// </summary>
        public static List<byte[]> GenerateProof(
            Node root,
            ITrieStorage storage,
            byte[] startKey,
            byte[] lastReturnedKey)
        {
            if (startKey == null) throw new ArgumentNullException(nameof(startKey));
            if (lastReturnedKey == null) throw new ArgumentNullException(nameof(lastReturnedKey));
            var collector = new InMemoryTrieStorage();
            CollectPathToKey(root, storage, startKey.ConvertToNibbles(), collector);
            if (!ByteUtil.AreEqual(startKey, lastReturnedKey))
                CollectPathToKey(root, storage, lastReturnedKey.ConvertToNibbles(), collector);
            return new List<byte[]>(collector.Storage.Values);
        }

        private static void CollectPathToKey(
            Node node,
            ITrieStorage storage,
            byte[] keyNibblesRemaining,
            InMemoryTrieStorage collector)
        {
            if (node is HashNode hash)
            {
                if (hash.InnerNode == null && storage != null)
                    hash.DecodeInnerNode(storage, false);
                if (hash.InnerNode == null) return;
                node = hash.InnerNode;
            }
            if (node == null || node is EmptyNode) return;

            // Record this node in the proof — both presence and non-existence
            // proofs need the entire walked path.
            collector.Put(node.GetHash(), node.GetRLPEncodedData());

            switch (node)
            {
                case LeafNode:
                    // Leaf terminates the path; nothing more to record.
                    return;

                case BranchNode branch:
                    if (keyNibblesRemaining.Length == 0) return;
                    var nextNibble = keyNibblesRemaining[0];
                    var child = branch.Children[nextNibble];
                    if (child == null) return;
                    CollectPathToKey(child, storage, keyNibblesRemaining.SliceFrom(1), collector);
                    return;

                case ExtendedNode ext:
                    // Only descend if the extension's nibbles match the key.
                    // If they diverge, this is the non-existence boundary and
                    // we've already captured the diverging node.
                    var shared = ext.Nibbles.FindAllTheSameBytesFromTheStart(keyNibblesRemaining);
                    if (shared.Length < ext.Nibbles.Length) return;
                    CollectPathToKey(ext.InnerNode, storage, keyNibblesRemaining.SliceFrom(ext.Nibbles.Length), collector);
                    return;
            }
        }
    }
}
