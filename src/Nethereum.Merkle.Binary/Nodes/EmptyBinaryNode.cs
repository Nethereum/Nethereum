using System;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary.Nodes
{
    public sealed class EmptyBinaryNode : IBinaryNode
    {
        public static readonly EmptyBinaryNode Instance = new EmptyBinaryNode();

        private EmptyBinaryNode() { }

        public byte[] Get(byte[] key, NodeResolverFunc resolver)
        {
            return null;
        }

        public IBinaryNode Insert(byte[] key, byte[] value, NodeResolverFunc resolver, int depth)
        {
            var values = new byte[BinaryTrieConstants.StemNodeWidth][];
            values[key[BinaryTrieConstants.StemSize]] = value;
            var stem = new byte[BinaryTrieConstants.StemSize];
            Array.Copy(key, 0, stem, 0, BinaryTrieConstants.StemSize);
            return new StemBinaryNode(stem, values, depth);
        }

        public byte[][] GetValuesAtStem(byte[] stem, NodeResolverFunc resolver)
        {
            return new byte[BinaryTrieConstants.StemNodeWidth][];
        }

        public IBinaryNode InsertValuesAtStem(byte[] stem, byte[][] values, NodeResolverFunc resolver, int depth)
        {
            var stemCopy = new byte[BinaryTrieConstants.StemSize];
            Array.Copy(stem, 0, stemCopy, 0, BinaryTrieConstants.StemSize);
            return new StemBinaryNode(stemCopy, values, depth);
        }

        public byte[] ComputeHash(IHashProvider hashProvider)
        {
            return new byte[BinaryTrieConstants.HashSize];
        }

        public IBinaryNode Copy()
        {
            return Instance;
        }

        public int GetHeight()
        {
            return 0;
        }
    }
}
