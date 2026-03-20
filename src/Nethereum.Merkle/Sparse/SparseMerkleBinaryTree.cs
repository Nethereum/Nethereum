using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Util.ByteArrayConvertors;

namespace Nethereum.Merkle.Sparse
{
    public class SparseMerkleBinaryTree<T>
    {
        private readonly ISmtHasher _hasher;
        private readonly ISmtKeyHasher _keyHasher;
        private readonly IByteArrayConvertor<T> _valueConvertor;
        private readonly ISmtNodeStorage _storage;
        private readonly int _depth;
        private readonly int _hashSize;
        private readonly bool _msbFirst;
        private readonly byte[][] _emptyHashes;

        private ISmtNode _root;
        private int _leafCount;

        public int Depth => _depth;
        public byte[] EmptyLeafHash => _emptyHashes[0];
        public int LeafCount => _leafCount;

        public SparseMerkleBinaryTree(ISmtHasher hasher, IByteArrayConvertor<T> valueConvertor,
            ISmtKeyHasher keyHasher = null, ISmtNodeStorage storage = null)
        {
            _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
            _valueConvertor = valueConvertor ?? throw new ArgumentNullException(nameof(valueConvertor));
            _keyHasher = keyHasher ?? new IdentitySmtKeyHasher(256);
            _storage = storage;
            _depth = _keyHasher.PathBitLength;
            _hashSize = (_depth + 7) / 8;
            _msbFirst = hasher.MsbFirst;

            if (_depth <= 0 || _depth > 256)
                throw new ArgumentException("Depth must be between 1 and 256");

            _emptyHashes = new byte[_depth + 1][];
            PrecomputeEmptyHashes();
        }

        #region Synchronous API (in-memory fast path)

        public void Put(byte[] key, T value)
        {
            var path = _keyHasher.ComputePath(key);
            if (value == null)
                _root = Remove(_root, path, 0);
            else
            {
                var valueBytes = _valueConvertor.ConvertToByteArray(value);
                _root = Insert(_root, path, value, valueBytes, 0);
            }
        }

        public T Get(byte[] key)
        {
            var path = _keyHasher.ComputePath(key);
            return Find(_root, path, 0);
        }

        public void Delete(byte[] key)
        {
            var path = _keyHasher.ComputePath(key);
            _root = Remove(_root, path, 0);
        }

        public byte[] ComputeRoot()
        {
            return ComputeHash(_root, _depth);
        }

        public void Clear()
        {
            _root = null;
            _leafCount = 0;
        }

        public void PutBatch(IEnumerable<KeyValuePair<byte[], T>> entries)
        {
            foreach (var entry in entries)
                Put(entry.Key, entry.Value);
        }

        #endregion

        #region Async API (storage-backed)

        public async Task PutAsync(byte[] key, T value)
        {
            var path = _keyHasher.ComputePath(key);
            if (value == null)
                _root = await RemoveAsync(_root, path, 0);
            else
            {
                var valueBytes = _valueConvertor.ConvertToByteArray(value);
                _root = await InsertAsync(_root, path, value, valueBytes, 0);
            }
        }

        public async Task<T> GetAsync(byte[] key)
        {
            var path = _keyHasher.ComputePath(key);
            return await FindAsync(_root, path, 0);
        }

        public async Task DeleteAsync(byte[] key)
        {
            var path = _keyHasher.ComputePath(key);
            _root = await RemoveAsync(_root, path, 0);
        }

        public async Task<byte[]> ComputeRootAsync()
        {
            return await ComputeHashAsync(_root, _depth);
        }

        public async Task PutBatchAsync(IEnumerable<KeyValuePair<byte[], T>> entries)
        {
            foreach (var entry in entries)
                await PutAsync(entry.Key, entry.Value);
        }

        public async Task FlushAsync()
        {
            if (_storage == null) return;
            await FlushNodeAsync(_root, _depth);
        }

        public async Task LoadRootAsync(byte[] rootHash)
        {
            if (_storage == null)
                throw new InvalidOperationException("Storage required for LoadRootAsync");
            if (rootHash == null || IsZero(rootHash))
            {
                _root = null;
                _leafCount = 0;
                return;
            }
            _root = new HashedSmtNode(rootHash);
        }

        #endregion

        #region Sync tree operations

        private ISmtNode Insert(ISmtNode node, byte[] path, T value, byte[] valueBytes, int bitDepth)
        {
            if (node == null)
            {
                _leafCount++;
                return new SmtLeafNode(path, value, valueBytes);
            }

            if (node is SmtLeafNode leaf)
            {
                if (PathEquals(leaf.Path, path))
                {
                    leaf.Value = value;
                    leaf.ValueBytes = valueBytes;
                    leaf.CachedHash = null;
                    return leaf;
                }
                return SplitLeaf(leaf, path, value, valueBytes, bitDepth);
            }

            if (node is SmtBranchNode branch)
            {
                int bit = GetBit(path, bitDepth);
                if (bit == 0)
                    branch.Left = Insert(branch.Left, path, value, valueBytes, bitDepth + 1);
                else
                    branch.Right = Insert(branch.Right, path, value, valueBytes, bitDepth + 1);
                branch.CachedHash = null;
                return branch;
            }

            return node;
        }

        private ISmtNode SplitLeaf(SmtLeafNode existing, byte[] newPath, T newValue, byte[] newValueBytes, int bitDepth)
        {
            int firstDiff = bitDepth;
            while (firstDiff < _depth && GetBit(existing.Path, firstDiff) == GetBit(newPath, firstDiff))
                firstDiff++;

            if (firstDiff >= _depth)
                return existing;

            _leafCount++;
            var newLeaf = new SmtLeafNode(newPath, newValue, newValueBytes);

            ISmtNode result;
            var splitBranch = new SmtBranchNode();
            if (GetBit(existing.Path, firstDiff) == 0)
            {
                splitBranch.Left = existing;
                splitBranch.Right = newLeaf;
            }
            else
            {
                splitBranch.Left = newLeaf;
                splitBranch.Right = existing;
            }
            result = splitBranch;

            for (int d = firstDiff - 1; d >= bitDepth; d--)
            {
                var parent = new SmtBranchNode();
                if (GetBit(existing.Path, d) == 0)
                    parent.Left = result;
                else
                    parent.Right = result;
                result = parent;
            }

            return result;
        }

        private ISmtNode Remove(ISmtNode node, byte[] path, int bitDepth)
        {
            if (node == null) return null;

            if (node is SmtLeafNode leaf)
            {
                if (PathEquals(leaf.Path, path))
                {
                    _leafCount--;
                    return null;
                }
                return node;
            }

            if (node is SmtBranchNode branch)
            {
                int bit = GetBit(path, bitDepth);
                if (bit == 0)
                    branch.Left = Remove(branch.Left, path, bitDepth + 1);
                else
                    branch.Right = Remove(branch.Right, path, bitDepth + 1);

                branch.CachedHash = null;

                if (branch.Left == null && branch.Right == null) return null;
                if (branch.Left == null && branch.Right is SmtLeafNode) return branch.Right;
                if (branch.Right == null && branch.Left is SmtLeafNode) return branch.Left;

                return branch;
            }

            return node;
        }

        private T Find(ISmtNode node, byte[] path, int bitDepth)
        {
            if (node == null) return default;
            if (node is SmtLeafNode leaf)
                return PathEquals(leaf.Path, path) ? leaf.Value : default;
            if (node is SmtBranchNode branch)
                return Find(GetBit(path, bitDepth) == 0 ? branch.Left : branch.Right, path, bitDepth + 1);
            return default;
        }

        private byte[] ComputeHash(ISmtNode node, int level)
        {
            if (node == null)
                return _emptyHashes[level];

            if (node is SmtLeafNode leaf)
            {
                if (leaf.CachedHash != null) return leaf.CachedHash;
                leaf.CachedHash = ComputeLeafHash(leaf, level);
                return leaf.CachedHash;
            }

            if (node is SmtBranchNode branch)
            {
                if (branch.CachedHash != null) return branch.CachedHash;
                var leftHash = ComputeHash(branch.Left, level - 1);
                var rightHash = ComputeHash(branch.Right, level - 1);
                branch.CachedHash = _hasher.HashNode(leftHash, rightHash);
                return branch.CachedHash;
            }

            if (node is HashedSmtNode hashed)
                return hashed.Hash;

            return _emptyHashes[level];
        }

        #endregion

        #region Async tree operations

        private async Task<ISmtNode> ResolveAsync(ISmtNode node, int bitDepth)
        {
            if (node is HashedSmtNode hashed && _storage != null)
            {
                var data = await _storage.GetAsync(hashed.Hash);
                if (data == null) return null;
                return DecodeNode(data);
            }
            return node;
        }

        private async Task<ISmtNode> InsertAsync(ISmtNode node, byte[] path, T value, byte[] valueBytes, int bitDepth)
        {
            node = await ResolveAsync(node, bitDepth);

            if (node == null)
            {
                _leafCount++;
                return new SmtLeafNode(path, value, valueBytes);
            }

            if (node is SmtLeafNode leaf)
            {
                if (PathEquals(leaf.Path, path))
                {
                    leaf.Value = value;
                    leaf.ValueBytes = valueBytes;
                    leaf.CachedHash = null;
                    return leaf;
                }
                return SplitLeaf(leaf, path, value, valueBytes, bitDepth);
            }

            if (node is SmtBranchNode branch)
            {
                int bit = GetBit(path, bitDepth);
                if (bit == 0)
                    branch.Left = await InsertAsync(branch.Left, path, value, valueBytes, bitDepth + 1);
                else
                    branch.Right = await InsertAsync(branch.Right, path, value, valueBytes, bitDepth + 1);
                branch.CachedHash = null;
                return branch;
            }

            return node;
        }

        private async Task<ISmtNode> RemoveAsync(ISmtNode node, byte[] path, int bitDepth)
        {
            node = await ResolveAsync(node, bitDepth);
            if (node == null) return null;

            if (node is SmtLeafNode leaf)
            {
                if (PathEquals(leaf.Path, path))
                {
                    _leafCount--;
                    return null;
                }
                return node;
            }

            if (node is SmtBranchNode branch)
            {
                int bit = GetBit(path, bitDepth);
                if (bit == 0)
                    branch.Left = await RemoveAsync(branch.Left, path, bitDepth + 1);
                else
                    branch.Right = await RemoveAsync(branch.Right, path, bitDepth + 1);

                branch.CachedHash = null;

                if (branch.Left == null && branch.Right == null) return null;
                if (branch.Left == null && branch.Right is SmtLeafNode) return branch.Right;
                if (branch.Right == null && branch.Left is SmtLeafNode) return branch.Left;

                return branch;
            }

            return node;
        }

        private async Task<T> FindAsync(ISmtNode node, byte[] path, int bitDepth)
        {
            node = await ResolveAsync(node, bitDepth);
            if (node == null) return default;
            if (node is SmtLeafNode leaf)
                return PathEquals(leaf.Path, path) ? leaf.Value : default;
            if (node is SmtBranchNode branch)
                return await FindAsync(GetBit(path, bitDepth) == 0 ? branch.Left : branch.Right, path, bitDepth + 1);
            return default;
        }

        private async Task<byte[]> ComputeHashAsync(ISmtNode node, int level)
        {
            node = await ResolveAsync(node, _depth - level);
            if (node == null)
                return _emptyHashes[level];

            if (node is SmtLeafNode leaf)
            {
                if (leaf.CachedHash != null) return leaf.CachedHash;
                leaf.CachedHash = ComputeLeafHash(leaf, level);
                return leaf.CachedHash;
            }

            if (node is SmtBranchNode branch)
            {
                if (branch.CachedHash != null) return branch.CachedHash;
                var leftHash = await ComputeHashAsync(branch.Left, level - 1);
                var rightHash = await ComputeHashAsync(branch.Right, level - 1);
                branch.CachedHash = _hasher.HashNode(leftHash, rightHash);
                return branch.CachedHash;
            }

            if (node is HashedSmtNode hashed)
                return hashed.Hash;

            return _emptyHashes[level];
        }

        private async Task FlushNodeAsync(ISmtNode node, int level)
        {
            if (node == null || node is HashedSmtNode) return;

            if (node is SmtLeafNode leaf)
            {
                var hash = ComputeHash(leaf, level);
                var encoded = SmtNodeCodec.EncodeLeaf(leaf.Path, leaf.ValueBytes);
                await _storage.PutAsync(hash, encoded);
            }
            else if (node is SmtBranchNode branch)
            {
                await FlushNodeAsync(branch.Left, level - 1);
                await FlushNodeAsync(branch.Right, level - 1);

                var leftHash = ComputeHash(branch.Left, level - 1);
                var rightHash = ComputeHash(branch.Right, level - 1);
                var hash = ComputeHash(branch, level);
                var encoded = SmtNodeCodec.EncodeBranch(leftHash, rightHash);
                await _storage.PutAsync(hash, encoded);
            }
        }

        #endregion

        #region Shared helpers

        private byte[] ComputeLeafHash(SmtLeafNode leaf, int level)
        {
            byte[] hash;
            if (_hasher.CollapseSingleLeaf)
            {
                hash = _hasher.HashLeaf(leaf.Path, leaf.ValueBytes);
            }
            else
            {
                hash = _hasher.HashLeaf(leaf.Path, leaf.ValueBytes);
                int leafBitDepth = _depth - level;
                for (int d = _depth - 1; d >= leafBitDepth; d--)
                {
                    int bit = GetBit(leaf.Path, d);
                    int emptyLevel = _depth - 1 - d;
                    if (bit == 0)
                        hash = _hasher.HashNode(hash, _emptyHashes[emptyLevel]);
                    else
                        hash = _hasher.HashNode(_emptyHashes[emptyLevel], hash);
                }
            }
            return hash;
        }

        private ISmtNode DecodeNode(byte[] data)
        {
            if (SmtNodeCodec.IsLeaf(data))
            {
                SmtNodeCodec.DecodeLeaf(data, out var path, out var valueBytes);
                var value = _valueConvertor.ConvertFromByteArray(valueBytes);
                return new SmtLeafNode(path, value, valueBytes);
            }

            if (SmtNodeCodec.IsBranch(data))
            {
                SmtNodeCodec.DecodeBranch(data, _hashSize, out var leftHash, out var rightHash);
                var branch = new SmtBranchNode();
                branch.Left = IsZero(leftHash) ? null : (ISmtNode)new HashedSmtNode(leftHash);
                branch.Right = IsZero(rightHash) ? null : (ISmtNode)new HashedSmtNode(rightHash);
                return branch;
            }

            return null;
        }

        private void PrecomputeEmptyHashes()
        {
            _emptyHashes[0] = _hasher.EmptyLeaf;
            if (_hasher.UseFixedEmptyHash)
            {
                for (int i = 1; i <= _depth; i++)
                    _emptyHashes[i] = _emptyHashes[0];
            }
            else
            {
                for (int i = 1; i <= _depth; i++)
                    _emptyHashes[i] = _hasher.HashNode(_emptyHashes[i - 1], _emptyHashes[i - 1]);
            }
        }

        private int GetBit(byte[] data, int bitIndex)
        {
            if (_msbFirst)
            {
                int byteIdx = bitIndex / 8;
                int bitInByte = 7 - (bitIndex % 8);
                if (byteIdx >= data.Length) return 0;
                return (data[byteIdx] >> bitInByte) & 1;
            }
            else
            {
                int byteIdx = data.Length - 1 - (bitIndex / 8);
                int bitInByte = bitIndex % 8;
                if (byteIdx < 0) return 0;
                return (data[byteIdx] >> bitInByte) & 1;
            }
        }

        private static bool PathEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private static bool IsZero(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
                if (data[i] != 0) return false;
            return true;
        }

        #endregion

        #region Node types

        internal interface ISmtNode { byte[] CachedHash { get; set; } }

        internal sealed class SmtLeafNode : ISmtNode
        {
            public byte[] Path;
            public T Value;
            public byte[] ValueBytes;
            public byte[] CachedHash { get; set; }

            public SmtLeafNode(byte[] path, T value, byte[] valueBytes)
            {
                Path = path;
                Value = value;
                ValueBytes = valueBytes;
            }
        }

        internal sealed class SmtBranchNode : ISmtNode
        {
            public ISmtNode Left;
            public ISmtNode Right;
            public byte[] CachedHash { get; set; }
        }

        internal sealed class HashedSmtNode : ISmtNode
        {
            public byte[] Hash { get; }
            public byte[] CachedHash { get => Hash; set { } }

            public HashedSmtNode(byte[] hash)
            {
                Hash = hash;
            }
        }

        #endregion
    }
}
