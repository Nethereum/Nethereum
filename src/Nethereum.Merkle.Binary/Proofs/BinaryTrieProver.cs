using System;
using System.Collections.Generic;
using Nethereum.Merkle.Binary.Nodes;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary.Proofs
{
    public class BinaryTrieProver
    {
        private readonly BinaryTrie _trie;
        private readonly IHashProvider _hashProvider;

        public BinaryTrieProver(BinaryTrie trie)
        {
            _trie = trie ?? throw new ArgumentNullException(nameof(trie));
            _hashProvider = trie.HashProvider;
        }

        public BinaryTrieProof BuildProof(byte[] key)
        {
            if (key == null || key.Length != BinaryTrieConstants.HashSize)
                throw new ArgumentException("Key must be 32 bytes");

            var stem = new byte[BinaryTrieConstants.StemSize];
            Array.Copy(key, 0, stem, 0, BinaryTrieConstants.StemSize);

            var path = _trie.FindPath(stem);
            var serialized = new List<byte[]>();

            for (int i = 0; i < path.Count; i++)
            {
                var encoded = CompactBinaryNodeCodec.Encode(path[i], _hashProvider);
                if (encoded.Length > 0)
                    serialized.Add(encoded);
            }

            return new BinaryTrieProof { Nodes = serialized.ToArray() };
        }
    }
}
