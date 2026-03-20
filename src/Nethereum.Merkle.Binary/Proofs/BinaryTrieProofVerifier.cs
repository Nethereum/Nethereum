using System;
using System.Collections.Generic;
using Nethereum.Merkle.Binary.Nodes;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary.Proofs
{
    public class BinaryTrieProofVerifier
    {
        private readonly IHashProvider _hashProvider;

        public BinaryTrieProofVerifier(IHashProvider hashProvider)
        {
            _hashProvider = hashProvider ?? throw new ArgumentNullException(nameof(hashProvider));
        }

        public byte[] VerifyProof(byte[] rootHash, byte[] key, BinaryTrieProof proof)
        {
            if (proof == null || proof.Nodes == null || proof.Nodes.Length == 0)
                return null;
            if (rootHash == null || key == null || key.Length != BinaryTrieConstants.HashSize)
                return null;
            if (BinaryTrieConstants.IsZeroHash(rootHash))
                return null;

            var nodesByHash = new Dictionary<byte[], IBinaryNode>(new ByteArrayComparer());

            for (int i = 0; i < proof.Nodes.Length; i++)
            {
                if (proof.Nodes[i] == null || proof.Nodes[i].Length == 0)
                    continue;
                var node = CompactBinaryNodeCodec.Decode(proof.Nodes[i], 0);
                var hash = node.ComputeHash(_hashProvider);
                nodesByHash[hash] = node;
            }

            if (!nodesByHash.TryGetValue(rootHash, out var current))
                return null;

            int depth = 0;
            while (current is InternalBinaryNode internalNode)
            {
                int bit = BinaryTrieUtils.GetBit(key, depth);
                var child = bit == 0 ? internalNode.Left : internalNode.Right;
                var childHash = child.ComputeHash(_hashProvider);

                if (BinaryTrieConstants.IsZeroHash(childHash))
                    return null;

                if (!nodesByHash.TryGetValue(childHash, out current))
                    return null;

                depth++;
            }

            if (current is StemBinaryNode stemNode)
            {
                if (!BinaryTrieUtils.ByteArrayEquals(stemNode.Stem, key, BinaryTrieConstants.StemSize))
                    return null;
                return stemNode.Values[key[BinaryTrieConstants.StemSize]];
            }

            return null;
        }
    }
}
