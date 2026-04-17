using System;
using System.Collections.Generic;
using Nethereum.Merkle.Binary.Nodes;
using Nethereum.Merkle.Binary.Storage;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary
{
    public class BinaryTrie
    {
        private IBinaryNode _root;
        private readonly IHashProvider _hashProvider;
        private readonly NodeResolverFunc _resolver;

        public BinaryTrie() : this(BinaryTrieOptions.Default) { }

        public BinaryTrie(BinaryTrieOptions options)
        {
            _root = EmptyBinaryNode.Instance;
            _hashProvider = options.HashProvider ?? new Sha256HashProvider();
            _resolver = options.NodeResolver;
        }

        public BinaryTrie(IHashProvider hashProvider) : this(new BinaryTrieOptions
        {
            HashProvider = hashProvider
        })
        { }

        public byte[] Get(byte[] key)
        {
            ValidateKey(key);
            return _root.Get(key, _resolver);
        }

        public void Put(byte[] key, byte[] value)
        {
            ValidateKey(key);
            if (value != null && value.Length != BinaryTrieConstants.HashSize)
                throw new ArgumentException("Value must be 32 bytes");
            _root = _root.Insert(key, value, _resolver, 0);
        }

        public void Delete(byte[] key)
        {
            ValidateKey(key);
            var zeroVal = new byte[BinaryTrieConstants.HashSize];
            _root = _root.Insert(key, zeroVal, _resolver, 0);
        }

        public byte[] ComputeRoot()
        {
            return _root.ComputeHash(_hashProvider);
        }

        public byte[][] GetValuesAtStem(byte[] stem)
        {
            if (stem == null || stem.Length < BinaryTrieConstants.StemSize)
                throw new ArgumentException("Stem must be at least 31 bytes");
            return _root.GetValuesAtStem(stem, _resolver);
        }

        public void PutStem(byte[] stem, byte[][] values)
        {
            if (stem == null || stem.Length < BinaryTrieConstants.StemSize)
                throw new ArgumentException("Stem must be at least 31 bytes");
            _root = _root.InsertValuesAtStem(stem, values, _resolver, 0);
        }

        public void ApplyBatch(IEnumerable<KeyValuePair<byte[], byte[]>> entries)
        {
            foreach (var entry in entries)
            {
                ValidateKey(entry.Key);
                _root = _root.Insert(entry.Key, entry.Value, _resolver, 0);
            }
        }

        public BinaryTrie Copy()
        {
            var copy = new BinaryTrie(new BinaryTrieOptions
            {
                HashProvider = _hashProvider,
                NodeResolver = _resolver
            });
            copy._root = _root.Copy();
            return copy;
        }

        public int GetHeight()
        {
            return _root.GetHeight();
        }

        public List<IBinaryNode> FindPath(byte[] stem)
        {
            if (stem == null || stem.Length < BinaryTrieConstants.StemSize)
                throw new ArgumentException("Stem must be at least 31 bytes");

            var path = new List<IBinaryNode>();
            CollectPath(_root, stem, 0, path);
            return path;
        }

        public IHashProvider HashProvider => _hashProvider;

        internal IBinaryNode Root => _root;

        public void SaveToStorage(IBinaryTrieStorage storage)
        {
            SaveNode(_root, storage, 0);
        }

        private void SaveNode(IBinaryNode node, IBinaryTrieStorage storage, int depth)
        {
            if (node is EmptyBinaryNode || node is HashedBinaryNode)
                return;

            var hash = node.ComputeHash(_hashProvider);
            var encoded = CompactBinaryNodeCodec.Encode(node, _hashProvider);

            if (storage is IBinaryTrieNodeStore nodeStore)
            {
                byte nodeType = node is StemBinaryNode ? BinaryTrieConstants.NodeTypeStem : BinaryTrieConstants.NodeTypeInternal;
                byte[] stem = node is StemBinaryNode stemNode ? stemNode.Stem : null;
                nodeStore.PutNode(hash, encoded, depth, nodeType, stem);
            }
            else
            {
                storage.Put(hash, encoded);
            }

            if (node is InternalBinaryNode internalNode)
            {
                SaveNode(internalNode.Left, storage, depth + 1);
                SaveNode(internalNode.Right, storage, depth + 1);
            }
        }

        private void CollectPath(IBinaryNode node, byte[] stem, int depth, List<IBinaryNode> path)
        {
            if (node is HashedBinaryNode hashed && _resolver != null)
            {
                var data = _resolver(stem, hashed.Hash);
                if (data != null)
                    node = CompactBinaryNodeCodec.Decode(data, hashed.NodeDepth);
            }

            path.Add(node);

            if (node is InternalBinaryNode internalNode)
            {
                int bit = BinaryTrieUtils.GetBit(stem, depth);
                var child = bit == 0 ? internalNode.Left : internalNode.Right;
                CollectPath(child, stem, depth + 1, path);
            }
        }

        private static void ValidateKey(byte[] key)
        {
            if (key == null || key.Length != BinaryTrieConstants.HashSize)
                throw new ArgumentException("Key must be 32 bytes");
        }
    }
}
