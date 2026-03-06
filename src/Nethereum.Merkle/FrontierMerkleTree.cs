using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Merkle.StrategyOptions.PairingConcat;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle
{
    public class FrontierMerkleTree
    {
        private readonly int _depth;
        private readonly byte[][] _zeros;
        private readonly byte[][] _filledSubtrees;
        private readonly IHashProvider _hashProvider;
        private readonly IPairConcatStrategy _pairConcatStrategy;
        private int _nextIndex;

        public byte[] Root { get; private set; }
        public int NextIndex => _nextIndex;
        public int Capacity => 1 << _depth;

        public FrontierMerkleTree(
            int depth,
            IHashProvider hashProvider,
            PairingConcatType pairingConcatType = PairingConcatType.Sorted)
        {
            if (depth < 1 || depth > 30)
                throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be between 1 and 30");
            _depth = depth;
            _hashProvider = hashProvider ?? throw new ArgumentNullException(nameof(hashProvider));
            _pairConcatStrategy = PairingConcatFactory.GetPairConcatStrategy(pairingConcatType);

            _zeros = new byte[_depth][];
            _zeros[0] = _hashProvider.ComputeHash(new byte[32]);
            for (int i = 1; i < _depth; i++)
            {
                _zeros[i] = HashPair(_zeros[i - 1], _zeros[i - 1]);
            }

            _filledSubtrees = new byte[_depth][];
            for (int i = 0; i < _depth; i++)
            {
                _filledSubtrees[i] = _zeros[i];
            }

            // Root of a fully empty tree is one more level above _zeros[depth-1]
            Root = HashPair(_zeros[_depth - 1], _zeros[_depth - 1]);
            _nextIndex = 0;
        }

        public void Append(byte[] leafHash)
        {
            if (leafHash == null) throw new ArgumentNullException(nameof(leafHash));
            if (_nextIndex >= Capacity)
                throw new InvalidOperationException($"Tree is full (capacity: {Capacity})");

            var currentHash = leafHash;
            int idx = _nextIndex;

            for (int level = 0; level < _depth; level++)
            {
                if (idx % 2 == 0)
                {
                    _filledSubtrees[level] = currentHash;
                    currentHash = HashPair(currentHash, _zeros[level]);
                }
                else
                {
                    currentHash = HashPair(_filledSubtrees[level], currentHash);
                }
                idx >>= 1;
            }

            Root = currentHash;
            _nextIndex++;
        }

        public static bool VerifyProof(
            MerkleProof proof,
            byte[] root,
            byte[] leafHash,
            IHashProvider hashProvider,
            PairingConcatType pairingConcatType = PairingConcatType.Sorted)
        {
            if (proof == null) throw new ArgumentNullException(nameof(proof));
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (leafHash == null) throw new ArgumentNullException(nameof(leafHash));

            var pairStrategy = PairingConcatFactory.GetPairConcatStrategy(pairingConcatType);
            var computedHash = leafHash;
            foreach (var node in proof.ProofNodes)
            {
                var combined = pairStrategy.Concat(computedHash, node);
                computedHash = hashProvider.ComputeHash(combined);
            }
            return computedHash.SequenceEqual(root);
        }

        private byte[] HashPair(byte[] left, byte[] right)
        {
            var combined = _pairConcatStrategy.Concat(left, right);
            return _hashProvider.ComputeHash(combined);
        }
    }
}
