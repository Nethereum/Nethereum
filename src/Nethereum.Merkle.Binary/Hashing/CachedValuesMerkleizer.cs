using System;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary.Hashing
{
    /// <summary>
    /// Cached 256-value merkle tree for StemNode. Stores all 511 intermediate
    /// hashes. When a value changes at sub-index i, only the 8-node path from
    /// leaf i to root is recomputed (8 hashes instead of 255).
    /// </summary>
    public class CachedValuesMerkleizer
    {
        // Binary tree stored as flat array:
        //   Level 0 (leaves): indices 256..511 (256 leaf hashes)
        //   Level 1: indices 128..255 (128 pair hashes)
        //   ...
        //   Level 8 (root): index 1
        //   Index 0 unused.
        // Parent of node i = i/2. Children of node i = 2i, 2i+1.
        private const int TreeSize = 512;
        private const int LeafOffset = 256;

        private readonly byte[][] _tree = new byte[TreeSize][];
        private readonly bool[] _leafDirty = new bool[256];
        private bool _fullDirty = true;

        public void MarkDirty(int subIndex)
        {
            _leafDirty[subIndex] = true;
        }

        public void MarkFullDirty()
        {
            _fullDirty = true;
        }

        public byte[] ComputeRoot(byte[][] values, IHashProvider hashProvider)
        {
            if (_fullDirty)
            {
                ComputeFull(values, hashProvider);
                _fullDirty = false;
                Array.Clear(_leafDirty, 0, 256);
                return _tree[1] ?? new byte[BinaryTrieConstants.HashSize];
            }

            for (int i = 0; i < 256; i++)
            {
                if (!_leafDirty[i]) continue;
                _leafDirty[i] = false;

                // Recompute leaf hash
                _tree[LeafOffset + i] = values[i] != null
                    ? hashProvider.ComputeHash(values[i])
                    : null;

                // Walk up the 8-level path to root
                int node = (LeafOffset + i) / 2;
                while (node >= 1)
                {
                    var left = _tree[node * 2];
                    var right = _tree[node * 2 + 1];

                    if (left == null && right == null)
                    {
                        _tree[node] = null;
                    }
                    else
                    {
                        var pair = new byte[BinaryTrieConstants.HashSize * 2];
                        if (left != null)
                            Array.Copy(left, 0, pair, 0, BinaryTrieConstants.HashSize);
                        if (right != null)
                            Array.Copy(right, 0, pair, BinaryTrieConstants.HashSize, BinaryTrieConstants.HashSize);
                        _tree[node] = BinaryTrieHash.Compute(hashProvider, pair);
                    }

                    node /= 2;
                }
            }

            return _tree[1] ?? new byte[BinaryTrieConstants.HashSize];
        }

        private void ComputeFull(byte[][] values, IHashProvider hashProvider)
        {
            // Hash all 256 leaves
            for (int i = 0; i < 256; i++)
            {
                _tree[LeafOffset + i] = (values != null && i < values.Length && values[i] != null)
                    ? hashProvider.ComputeHash(values[i])
                    : null;
            }

            // Build tree bottom-up: 255..128 (level 1), 127..64 (level 2), ..., 1 (root)
            for (int node = LeafOffset - 1; node >= 1; node--)
            {
                var left = _tree[node * 2];
                var right = _tree[node * 2 + 1];

                if (left == null && right == null)
                {
                    _tree[node] = null;
                    continue;
                }

                var pair = new byte[BinaryTrieConstants.HashSize * 2];
                if (left != null)
                    Array.Copy(left, 0, pair, 0, BinaryTrieConstants.HashSize);
                if (right != null)
                    Array.Copy(right, 0, pair, BinaryTrieConstants.HashSize, BinaryTrieConstants.HashSize);
                _tree[node] = BinaryTrieHash.Compute(hashProvider, pair);
            }
        }
    }
}
