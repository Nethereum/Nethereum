using System;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary.Nodes
{
    public class HashedBinaryNode : IBinaryNode
    {
        public byte[] Hash { get; }
        internal int NodeDepth { get; }

        public HashedBinaryNode(byte[] hash, int depth)
        {
            Hash = hash;
            NodeDepth = depth;
        }

        public byte[] Get(byte[] key, NodeResolverFunc resolver)
        {
            var resolved = Resolve(key, resolver);
            return resolved.Get(key, resolver);
        }

        public IBinaryNode Insert(byte[] key, byte[] value, NodeResolverFunc resolver, int depth)
        {
            var resolved = Resolve(key, resolver);
            return resolved.Insert(key, value, resolver, depth);
        }

        private IBinaryNode Resolve(byte[] keyOrStem, NodeResolverFunc resolver)
        {
            if (resolver == null)
                throw new InvalidOperationException("Resolver required for hashed node");
            var data = resolver(keyOrStem, Hash);
            if (data == null)
                throw new InvalidOperationException($"Failed to resolve node {BitConverter.ToString(Hash, 0, Math.Min(4, Hash.Length))}");
            return CompactBinaryNodeCodec.Decode(data, NodeDepth);
        }

        public byte[][] GetValuesAtStem(byte[] stem, NodeResolverFunc resolver)
        {
            throw new InvalidOperationException("Cannot get values from unresolved hashed node");
        }

        public IBinaryNode InsertValuesAtStem(byte[] stem, byte[][] values, NodeResolverFunc resolver, int depth)
        {
            if (resolver == null)
                throw new InvalidOperationException("Resolver required to insert into hashed node");

            var data = resolver(stem, Hash);
            if (data == null)
                throw new InvalidOperationException("Failed to resolve hashed node");

            var node = CompactBinaryNodeCodec.Decode(data, NodeDepth);
            return node.InsertValuesAtStem(stem, values, resolver, depth);
        }

        public byte[] ComputeHash(IHashProvider hashProvider)
        {
            return Hash;
        }

        public IBinaryNode Copy()
        {
            var hashCopy = new byte[Hash.Length];
            Array.Copy(Hash, 0, hashCopy, 0, Hash.Length);
            return new HashedBinaryNode(hashCopy, NodeDepth);
        }

        public int GetHeight()
        {
            throw new InvalidOperationException("Cannot get height of unresolved hashed node");
        }
    }
}
