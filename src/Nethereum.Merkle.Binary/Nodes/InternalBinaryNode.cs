using System;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary.Nodes
{
    public class InternalBinaryNode : IBinaryNode
    {
        public IBinaryNode Left { get; set; }
        public IBinaryNode Right { get; set; }
        internal int Depth { get; }

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
            var values = new byte[BinaryTrieConstants.StemNodeWidth][];
            values[key[BinaryTrieConstants.StemSize]] = value;
            return InsertValuesAtStem(key, values, resolver, depth);
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

            return this;
        }

        public byte[] ComputeHash(IHashProvider hashProvider)
        {
            var leftHash = Left.ComputeHash(hashProvider);
            var rightHash = Right.ComputeHash(hashProvider);

            var pair = new byte[BinaryTrieConstants.HashSize * 2];
            Array.Copy(leftHash, 0, pair, 0, BinaryTrieConstants.HashSize);
            Array.Copy(rightHash, 0, pair, BinaryTrieConstants.HashSize, BinaryTrieConstants.HashSize);

            return hashProvider.ComputeHash(pair);
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
