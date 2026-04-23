using System;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary.Nodes
{
    public class InternalBinaryNode : IBinaryNode
    {
        public IBinaryNode Left { get; set; }
        public IBinaryNode Right { get; set; }
        internal int Depth { get; }
        private byte[] _cachedHash;
        private bool _dirty = true;

        public InternalBinaryNode(int depth)
        {
            Depth = depth;
            Left = EmptyBinaryNode.Instance;
            Right = EmptyBinaryNode.Instance;
        }

        public InternalBinaryNode(int depth, IBinaryNode left, IBinaryNode right)
        {
            Depth = depth;
            Left = left ?? EmptyBinaryNode.Instance;
            Right = right ?? EmptyBinaryNode.Instance;
        }

        public byte[] Get(byte[] key, NodeResolverFunc resolver)
        {
            var values = GetValuesAtStem(key, resolver);
            if (values == null)
                return null;
            return values[key[BinaryTrieConstants.StemSize]];
        }

        public IBinaryNode Insert(byte[] key, byte[] value, NodeResolverFunc resolver, int depth)
        {
            int bit = BinaryTrieUtils.GetBit(key, Depth);

            if (bit == 0)
            {
                Left = TryResolve(Left, key, resolver);
                Left = Left.Insert(key, value, resolver, depth + 1);
            }
            else
            {
                Right = TryResolve(Right, key, resolver);
                Right = Right.Insert(key, value, resolver, depth + 1);
            }

            _dirty = true;
            _cachedHash = null;
            return this;
        }

        public byte[][] GetValuesAtStem(byte[] stem, NodeResolverFunc resolver)
        {
            if (Depth > BinaryTrieConstants.StemSize * 8)
                return null;

            int bit = BinaryTrieUtils.GetBit(stem, Depth);

            var child = bit == 0 ? Left : Right;
            child = TryResolve(child, stem, resolver);

            if (bit == 0)
                Left = child;
            else
                Right = child;

            return child.GetValuesAtStem(stem, resolver);
        }

        public IBinaryNode InsertValuesAtStem(byte[] stem, byte[][] values, NodeResolverFunc resolver, int depth)
        {
            int bit = BinaryTrieUtils.GetBit(stem, Depth);

            if (bit == 0)
            {
                Left = TryResolve(Left, stem, resolver);
                Left = Left.InsertValuesAtStem(stem, values, resolver, depth + 1);
            }
            else
            {
                Right = TryResolve(Right, stem, resolver);
                Right = Right.InsertValuesAtStem(stem, values, resolver, depth + 1);
            }

            _dirty = true;
            _cachedHash = null;
            return this;
        }

        public byte[] ComputeHash(IHashProvider hashProvider)
        {
            if (!_dirty && _cachedHash != null)
                return _cachedHash;

            var leftHash = Left.ComputeHash(hashProvider);
            var rightHash = Right.ComputeHash(hashProvider);

            var pair = new byte[BinaryTrieConstants.HashSize * 2];
            Array.Copy(leftHash, 0, pair, 0, BinaryTrieConstants.HashSize);
            Array.Copy(rightHash, 0, pair, BinaryTrieConstants.HashSize, BinaryTrieConstants.HashSize);

            _cachedHash = Hashing.BinaryTrieHash.Compute(hashProvider, pair);
            _dirty = false;
            return _cachedHash;
        }

        public IBinaryNode Copy()
        {
            return new InternalBinaryNode(Depth, Left.Copy(), Right.Copy());
        }

        public int GetHeight()
        {
            int leftHeight = Left.GetHeight();
            int rightHeight = Right.GetHeight();
            return 1 + Math.Max(leftHeight, rightHeight);
        }

        private static IBinaryNode TryResolve(IBinaryNode node, byte[] stem, NodeResolverFunc resolver)
        {
            if (node is HashedBinaryNode hashed && resolver != null)
            {
                var data = resolver(stem, hashed.Hash);
                if (data != null)
                    return CompactBinaryNodeCodec.Decode(data, hashed.NodeDepth);
            }
            return node;
        }
    }
}
