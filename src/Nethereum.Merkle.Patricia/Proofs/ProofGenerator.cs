using Nethereum.Documentation;
using System.Collections.Generic;
using Nethereum.Util;
using Nethereum.Model;

using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
namespace Nethereum.Merkle.Patricia.Proofs
{
    public static class ProofGenerator
    {
        [NethereumDocExample(DocSection.ChainInfrastructure, "state-proofs", "Generate a root-first EIP-1186 proof for a key")]
        public static List<byte[]> GenerateProof(PatriciaTrie trie, byte[] key)
            => GenerateProof(trie.Root, key.ConvertToNibbles(), trie.Store, new List<byte[]>());

        public static List<byte[]> GenerateProof(Node currentNode, byte[] keyAsNibbles, ITrieNodeStore store, List<byte[]> proofNodes)
        {
            if (currentNode is EmptyNode || currentNode is null)
            {
                return null;
            }

            if (currentNode is HashNode hashNode)
            {
                PatriciaPathWalker.ResolveInner(hashNode, store);
                if (hashNode.InnerNode != null)
                {
                    return GenerateProof(hashNode.InnerNode, keyAsNibbles, store, proofNodes);
                }
                return null;
            }

            proofNodes.Add(currentNode.GetEncodedData());

            if (currentNode is LeafNode leafNode)
            {
                return ProveLeaf(leafNode, keyAsNibbles, proofNodes);
            }

            if (currentNode is BranchNode branchNode)
            {
                return ProveBranch(branchNode, keyAsNibbles, store, proofNodes);
            }

            if (currentNode is ExtendedNode extendedNode)
            {
                return ProveExtension(extendedNode, keyAsNibbles, store, proofNodes);
            }

            return proofNodes;
        }

        private static List<byte[]> ProveLeaf(LeafNode leafNode, byte[] keyAsNibbles, List<byte[]> proofNodes)
        {
            var foundSameNibbles = leafNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
            if (foundSameNibbles.Length != leafNode.Nibbles.Length || foundSameNibbles.Length != keyAsNibbles.Length)
            {
                return null;
            }
            return proofNodes;
        }

        private static List<byte[]> ProveBranch(BranchNode branchNode, byte[] keyAsNibbles, ITrieNodeStore store, List<byte[]> proofNodes)
        {
            if (keyAsNibbles.Length == 0)
            {
                if (branchNode.Value == null)
                {
                    return null;
                }
                return proofNodes;
            }
            return GenerateProof(branchNode.Children[keyAsNibbles[0]], keyAsNibbles.SliceFrom(1), store, proofNodes);
        }

        private static List<byte[]> ProveExtension(ExtendedNode extendedNode, byte[] keyAsNibbles, ITrieNodeStore store, List<byte[]> proofNodes)
        {
            var foundSameNibbles = extendedNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
            if (foundSameNibbles.Length < extendedNode.Nibbles.Length)
            {
                return null;
            }

            return GenerateProof(extendedNode.InnerNode, keyAsNibbles.SliceFrom(foundSameNibbles.Length), store, proofNodes);
        }

        public static List<byte[]> GeneratePathProof(PatriciaTrie trie, byte[] key)
            => GeneratePathProof(trie.Root, key.ConvertToNibbles(), trie.Store);

        public static List<byte[]> GeneratePathProof(Node root, byte[] keyAsNibbles, ITrieNodeStore store)
        {
            var proofNodes = new List<byte[]>();
            var currentNode = root;
            var remaining = keyAsNibbles;

            while (true)
            {
                if (currentNode is HashNode hashNode)
                {
                    PatriciaPathWalker.ResolveInner(hashNode, store);
                    if (hashNode.InnerNode == null || hashNode.InnerNode is EmptyNode)
                    {
                        return IsEmptyTrieRoot(hashNode, proofNodes) ? proofNodes : null;
                    }
                    currentNode = hashNode.InnerNode;
                    continue;
                }

                if (currentNode is EmptyNode || currentNode is null)
                {
                    return proofNodes;
                }

                var encoded = currentNode.GetEncodedData();
                if (proofNodes.Count == 0 || encoded.Length >= 32)
                {
                    proofNodes.Add(encoded);
                }

                if (!TryDescend(ref currentNode, ref remaining))
                {
                    return proofNodes;
                }
            }
        }

        private static bool TryDescend(ref Node currentNode, ref byte[] remaining)
        {
            if (currentNode is BranchNode branchNode)
            {
                if (remaining.Length == 0)
                {
                    return false;
                }
                currentNode = branchNode.Children[remaining[0]];
                remaining = remaining.SliceFrom(1);
                return true;
            }

            if (currentNode is ExtendedNode extendedNode)
            {
                var foundSameNibbles = extendedNode.Nibbles.FindAllTheSameBytesFromTheStart(remaining);
                if (foundSameNibbles.Length < extendedNode.Nibbles.Length)
                {
                    return false;
                }
                currentNode = extendedNode.InnerNode;
                remaining = remaining.SliceFrom(foundSameNibbles.Length);
                return true;
            }

            return false;
        }

        private static bool IsEmptyTrieRoot(HashNode hashNode, List<byte[]> proofNodes)
            => proofNodes.Count == 0 && hashNode.Hash != null && hashNode.Hash.AreTheSame(DefaultValues.EMPTY_TRIE_HASH);
    }
}
