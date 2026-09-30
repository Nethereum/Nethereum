using Nethereum.Documentation;
using System.Collections.Generic;
using Nethereum.Util;

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
                if (hashNode.InnerNode == null && store != null)
                {
                    hashNode.DecodeInnerNode(store, false);
                }
                if (hashNode.InnerNode != null)
                {
                    return GenerateProof(hashNode.InnerNode, keyAsNibbles, store, proofNodes);
                }
                return null;
            }

            proofNodes.Add(currentNode.GetEncodedData());

            if (currentNode is LeafNode leafNode)
            {
                var foundSameNibbles = leafNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
                if (foundSameNibbles.Length != leafNode.Nibbles.Length || foundSameNibbles.Length != keyAsNibbles.Length)
                {
                    return null;
                }
                return proofNodes;
            }

            if (currentNode is BranchNode branchNode)
            {
               if(keyAsNibbles.Length == 0)
               {
                    if(branchNode.Value == null)
                    {
                        return null;
                    }
                    return proofNodes;
                }
                return GenerateProof(branchNode.Children[keyAsNibbles[0]], keyAsNibbles.SliceFrom(1), store, proofNodes);
            }

            if (currentNode is ExtendedNode extendedNode)
            {
                var foundSameNibbles = extendedNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
                if(foundSameNibbles.Length < extendedNode.Nibbles.Length)
                {
                    return null;
                }

                return GenerateProof(extendedNode.InnerNode, keyAsNibbles.SliceFrom(foundSameNibbles.Length), store, proofNodes);
            }

            return proofNodes;
        }
    }
}
