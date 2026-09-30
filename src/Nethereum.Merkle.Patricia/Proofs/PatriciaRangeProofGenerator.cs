using System;
using System.Collections.Generic;
using Nethereum.Util;

using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
namespace Nethereum.Merkle.Patricia.Proofs
{
    public static class PatriciaRangeProofGenerator
    {
        public static List<byte[]> GenerateProof(
            Node root,
            ITrieNodeStore store,
            byte[] startKey)
        {
            if (startKey == null) throw new ArgumentNullException(nameof(startKey));
            var collector = new InMemoryContentNodeStore();
            CollectPathToKey(root, store, startKey.ConvertToNibbles(), collector);
            return new List<byte[]>(collector.Storage.Values);
        }

        public static List<byte[]> GenerateProof(
            Node root,
            ITrieNodeStore store,
            byte[] startKey,
            byte[] lastReturnedKey)
        {
            if (startKey == null) throw new ArgumentNullException(nameof(startKey));
            if (lastReturnedKey == null) throw new ArgumentNullException(nameof(lastReturnedKey));
            var collector = new InMemoryContentNodeStore();
            CollectPathToKey(root, store, startKey.ConvertToNibbles(), collector);
            if (!ByteUtil.AreEqual(startKey, lastReturnedKey))
                CollectPathToKey(root, store, lastReturnedKey.ConvertToNibbles(), collector);
            return new List<byte[]>(collector.Storage.Values);
        }

        private static void CollectPathToKey(
            Node node,
            ITrieNodeStore store,
            byte[] keyNibblesRemaining,
            InMemoryContentNodeStore collector)
        {
            if (node is HashNode hash)
            {
                if (hash.InnerNode == null && store != null)
                    hash.DecodeInnerNode(store, false);
                if (hash.InnerNode == null) return;
                node = hash.InnerNode;
            }
            if (node == null || node is EmptyNode) return;

            collector.Put(node.GetHash(), node.GetEncodedData());

            switch (node)
            {
                case LeafNode:
                    return;

                case BranchNode branch:
                    if (keyNibblesRemaining.Length == 0) return;
                    var nextNibble = keyNibblesRemaining[0];
                    var child = branch.Children[nextNibble];
                    if (child == null) return;
                    CollectPathToKey(child, store, keyNibblesRemaining.SliceFrom(1), collector);
                    return;

                case ExtendedNode ext:
                    var shared = ext.Nibbles.FindAllTheSameBytesFromTheStart(keyNibblesRemaining);
                    if (shared.Length < ext.Nibbles.Length) return;
                    CollectPathToKey(ext.InnerNode, store, keyNibblesRemaining.SliceFrom(ext.Nibbles.Length), collector);
                    return;
            }
        }
    }
}
