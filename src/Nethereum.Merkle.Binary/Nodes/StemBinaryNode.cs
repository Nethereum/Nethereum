using System;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary.Nodes
{
    public class StemBinaryNode : IBinaryNode
    {
        public byte[] Stem { get; }
        public byte[][] Values { get; }
        internal int Depth { get; private set; }

        public StemBinaryNode(byte[] stem, byte[][] values, int depth)
        {
            Stem = stem;
            Values = values;
            Depth = depth;
        }

        public byte[] Get(byte[] key, NodeResolverFunc resolver)
        {
            if (!StemEquals(key))
                return null;
            return Values[key[BinaryTrieConstants.StemSize]];
        }

        public IBinaryNode Insert(byte[] key, byte[] value, NodeResolverFunc resolver, int depth)
        {
            if (!StemEquals(key))
                return SplitAndInsert(key, value, depth);

            if (value != null && value.Length != BinaryTrieConstants.HashSize)
                throw new ArgumentException("Value must be 32 bytes");

            Values[key[BinaryTrieConstants.StemSize]] = value;
            return this;
        }

        public byte[][] GetValuesAtStem(byte[] stem, NodeResolverFunc resolver)
        {
            if (!BinaryTrieUtils.ByteArrayEquals(Stem, stem, BinaryTrieConstants.StemSize))
                return null;
            return Values;
        }

        public IBinaryNode InsertValuesAtStem(byte[] stem, byte[][] values, NodeResolverFunc resolver, int depth)
        {
            if (!BinaryTrieUtils.ByteArrayEquals(Stem, stem, BinaryTrieConstants.StemSize))
                return SplitAndInsertStem(stem, values, depth);

            for (int i = 0; i < values.Length && i < BinaryTrieConstants.StemNodeWidth; i++)
            {
                if (values[i] != null)
                    Values[i] = values[i];
            }
            return this;
        }

        public byte[] ComputeHash(IHashProvider hashProvider)
        {
            var valuesRoot = ValuesMerkleizer.Merkleize(Values, hashProvider);

            var preimage = new byte[BinaryTrieConstants.StemSize + 1 + BinaryTrieConstants.HashSize];
            Array.Copy(Stem, 0, preimage, 0, BinaryTrieConstants.StemSize);
            preimage[BinaryTrieConstants.StemSize] = 0;
            Array.Copy(valuesRoot, 0, preimage, BinaryTrieConstants.StemSize + 1, BinaryTrieConstants.HashSize);

            return hashProvider.ComputeHash(preimage);
        }

        public IBinaryNode Copy()
        {
            var stemCopy = new byte[BinaryTrieConstants.StemSize];
            Array.Copy(Stem, 0, stemCopy, 0, BinaryTrieConstants.StemSize);

            var valuesCopy = new byte[BinaryTrieConstants.StemNodeWidth][];
            for (int i = 0; i < BinaryTrieConstants.StemNodeWidth; i++)
            {
                if (Values[i] != null)
                {
                    valuesCopy[i] = new byte[Values[i].Length];
                    Array.Copy(Values[i], 0, valuesCopy[i], 0, Values[i].Length);
                }
            }
            return new StemBinaryNode(stemCopy, valuesCopy, Depth);
        }

        public int GetHeight() => 1;

        private IBinaryNode SplitAndInsert(byte[] key, byte[] value, int depth)
        {
            var values = new byte[BinaryTrieConstants.StemNodeWidth][];
            values[key[BinaryTrieConstants.StemSize]] = value;

            var keyStem = new byte[BinaryTrieConstants.StemSize];
            Array.Copy(key, 0, keyStem, 0, BinaryTrieConstants.StemSize);

            return SplitWith(keyStem, values, depth);
        }

        private IBinaryNode SplitAndInsertStem(byte[] stem, byte[][] values, int depth)
        {
            var stemCopy = new byte[BinaryTrieConstants.StemSize];
            Array.Copy(stem, 0, stemCopy, 0, BinaryTrieConstants.StemSize);

            return SplitWith(stemCopy, values, depth);
        }

        private IBinaryNode SplitWith(byte[] otherStem, byte[][] otherValues, int depth)
        {
            int bitStem = BinaryTrieUtils.GetBit(Stem, Depth);

            var node = new InternalBinaryNode(Depth);
            Depth++;

            int bitKey = BinaryTrieUtils.GetBit(otherStem, node.Depth);

            if (bitKey == bitStem)
            {
                if (bitStem == 0)
                {
                    node.Left = this;
                    node.Right = EmptyBinaryNode.Instance;
                    node.Left = node.Left.InsertValuesAtStem(otherStem, otherValues, null, depth + 1);
                }
                else
                {
                    node.Right = this;
                    node.Left = EmptyBinaryNode.Instance;
                    node.Right = node.Right.InsertValuesAtStem(otherStem, otherValues, null, depth + 1);
                }
            }
            else
            {
                var newStem = new StemBinaryNode(otherStem, otherValues, node.Depth + 1);
                if (bitStem == 0)
                {
                    node.Left = this;
                    node.Right = newStem;
                }
                else
                {
                    node.Right = this;
                    node.Left = newStem;
                }
            }

            return node;
        }

        private bool StemEquals(byte[] key)
        {
            return BinaryTrieUtils.ByteArrayEquals(Stem, key, BinaryTrieConstants.StemSize);
        }
    }
}
