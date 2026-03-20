using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Util.HashProviders;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Merkle.Sparse
{
    public class SparseMerkleTree<T>
    {
        private readonly ISparseMerkleTreeStorage<T> _storage;
        private readonly IHashProvider _hashProvider;
        private readonly ISmtHasher _hasher;
        private readonly IByteArrayConvertor<T> _byteArrayConvertor;
        private readonly int _depth;
        private readonly string[] _emptyHashes;
        private readonly int _hexCharsNeeded;
        private readonly bool _msbFirst;
        private readonly bool _collapseSingleLeaf;

        private string _cachedRoot;
        private bool _isDirty = true;

        private readonly Dictionary<string, int> _populatedNodeRefCounts = new Dictionary<string, int>();

        public int Depth => _depth;
        public string EmptyLeafHash => _emptyHashes[0];
        public IHashProvider HashProvider => _hashProvider;

        public SparseMerkleTree(int depth, IHashProvider hashProvider, IByteArrayConvertor<T> byteArrayConvertor, ISparseMerkleTreeStorage<T> storage)
            : this(depth, hashProvider, byteArrayConvertor, storage, new DefaultSmtHasher(hashProvider))
        {
        }

        public SparseMerkleTree(int depth, IHashProvider hashProvider, IByteArrayConvertor<T> byteArrayConvertor, ISparseMerkleTreeStorage<T> storage, ISmtHasher hasher)
        {
            if (depth <= 0 || depth > 256)
                throw new ArgumentException("Depth must be between 1 and 256");

            _depth = depth;
            _hashProvider = hashProvider ?? throw new ArgumentNullException(nameof(hashProvider));
            _byteArrayConvertor = byteArrayConvertor ?? throw new ArgumentNullException(nameof(byteArrayConvertor));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
            _msbFirst = hasher.MsbFirst;
            _collapseSingleLeaf = hasher.CollapseSingleLeaf;

            _hexCharsNeeded = (depth + 3) / 4;

            _emptyHashes = new string[depth + 1];
            PrecomputeEmptyHashes();
        }

        public async Task SetLeafAsync(string key, T value)
        {
            var normalizedKey = key.PadRight(_hexCharsNeeded, '0');
            var existingValue = await _storage.GetLeafAsync(key);
            var hadValue = existingValue != null;
            var willHaveValue = value != null;

            if (value == null)
            {
                await _storage.RemoveLeafAsync(key);
            }
            else
            {
                await _storage.SetLeafAsync(key, value);
            }

            if (!hadValue && willHaveValue)
            {
                IncrementPathRefCounts(normalizedKey);
            }
            else if (hadValue && !willHaveValue)
            {
                DecrementPathRefCounts(normalizedKey);
            }

            await InvalidatePathToRootAsync(key);

            _isDirty = true;
            _cachedRoot = null;
        }

        public async Task<T> GetLeafAsync(string key)
        {
            return await _storage.GetLeafAsync(key);
        }

        private async Task<string> GetLeafHashAsync(string key)
        {
            var storedValue = await _storage.GetLeafAsync(key);
            if (storedValue == null)
                return GetEmptyHash(0);

            var valueBytes = _byteArrayConvertor.ConvertToByteArray(storedValue);
            var pathBytes = key.HexToByteArray();
            var hash = _hasher.HashLeaf(pathBytes, valueBytes);
            return hash.ToHex();
        }

        public async Task<string> GetRootHashAsync()
        {
            if (!_isDirty && _cachedRoot != null)
                return _cachedRoot;

            var rootKey = new string('0', _hexCharsNeeded);
            _cachedRoot = await ComputeNodeHashAsync(rootKey, _depth);
            _isDirty = false;

            return _cachedRoot;
        }

        private async Task<string> ComputeNodeHashAsync(string nodeKey, int level)
        {
            if (level == 0)
            {
                return await GetLeafHashAsync(nodeKey);
            }

            var cacheKey = $"{level}_{nodeKey}";
            var cached = await _storage.GetCachedNodeAsync(cacheKey);
            if (cached != null)
            {
                return cached.ToHex();
            }

            if (!_populatedNodeRefCounts.ContainsKey(cacheKey))
            {
                return GetEmptyHash(level);
            }

            if (_collapseSingleLeaf && _populatedNodeRefCounts[cacheKey] == 1)
            {
                return await FindSingleLeafHashAsync(nodeKey, level);
            }

            int bitIndex = _msbFirst ? (_depth - level) : (level - 1);
            var leftKey = ClearBit(nodeKey, bitIndex);
            var rightKey = SetBit(nodeKey, bitIndex);

            var leftHash = await ComputeNodeHashAsync(leftKey, level - 1);
            var rightHash = await ComputeNodeHashAsync(rightKey, level - 1);

            var nodeHash = _hasher.HashNode(leftHash.HexToByteArray(), rightHash.HexToByteArray()).ToHex();
            await _storage.SetCachedNodeAsync(cacheKey, nodeHash.HexToByteArray());

            return nodeHash;
        }

        private async Task<string> FindSingleLeafHashAsync(string nodeKey, int level)
        {
            if (level == 0)
                return await GetLeafHashAsync(nodeKey);

            int bitIndex = _msbFirst ? (_depth - level) : (level - 1);
            var leftKey = ClearBit(nodeKey, bitIndex);
            var rightKey = SetBit(nodeKey, bitIndex);

            var leftCacheKey = $"{level - 1}_{leftKey}";

            if (_populatedNodeRefCounts.ContainsKey(leftCacheKey))
                return await FindSingleLeafHashAsync(leftKey, level - 1);
            else
                return await FindSingleLeafHashAsync(rightKey, level - 1);
        }

        private string SetBit(string hexKey, int bitPosition)
        {
            var workingKey = hexKey.PadRight(_hexCharsNeeded, '0');
            var chars = workingKey.ToCharArray();

            var hexIndex = bitPosition / 4;
            var bitInHex = bitPosition % 4;

            if (hexIndex < chars.Length)
            {
                var hexValue = Convert.ToInt32(chars[hexIndex].ToString(), 16);
                hexValue |= (1 << (3 - bitInHex));
                chars[hexIndex] = hexValue.ToString("x")[0];
            }

            return new string(chars);
        }

        private string ClearBit(string hexKey, int bitPosition)
        {
            var workingKey = hexKey.PadRight(_hexCharsNeeded, '0');
            var chars = workingKey.ToCharArray();

            var hexIndex = bitPosition / 4;
            var bitInHex = bitPosition % 4;

            if (hexIndex < chars.Length)
            {
                var hexValue = Convert.ToInt32(chars[hexIndex].ToString(), 16);
                hexValue &= ~(1 << (3 - bitInHex));
                chars[hexIndex] = hexValue.ToString("x")[0];
            }

            return new string(chars);
        }

        private void PrecomputeEmptyHashes()
        {
            _emptyHashes[0] = _hasher.EmptyLeaf.ToHex();

            if (_hasher.UseFixedEmptyHash)
            {
                for (int i = 1; i <= _depth; i++)
                    _emptyHashes[i] = _emptyHashes[0];
            }
            else
            {
                for (int i = 1; i <= _depth; i++)
                {
                    var prev = _emptyHashes[i - 1].HexToByteArray();
                    _emptyHashes[i] = _hasher.HashNode(prev, prev).ToHex();
                }
            }
        }

        private string GetEmptyHash(int level) => _emptyHashes[level];

        public async Task ClearAsync()
        {
            await _storage.ClearAsync();
            _populatedNodeRefCounts.Clear();
            _isDirty = true;
            _cachedRoot = null;
        }

        public async Task<long> GetLeafCountAsync()
        {
            return await _storage.GetLeafCountAsync();
        }

        public async Task SetLeavesAsync(Dictionary<string, T> keyValuePairs)
        {
            if (keyValuePairs == null || keyValuePairs.Count == 0)
                return;

            var affectedPaths = new HashSet<string>();

            foreach (var kvp in keyValuePairs)
            {
                var normalizedKey = kvp.Key.PadRight(_hexCharsNeeded, '0');
                var existingValue = await _storage.GetLeafAsync(kvp.Key);
                var hadValue = existingValue != null;
                var willHaveValue = kvp.Value != null;

                if (kvp.Value == null)
                {
                    await _storage.RemoveLeafAsync(kvp.Key);
                }
                else
                {
                    await _storage.SetLeafAsync(kvp.Key, kvp.Value);
                }

                if (!hadValue && willHaveValue)
                {
                    IncrementPathRefCounts(normalizedKey);
                }
                else if (hadValue && !willHaveValue)
                {
                    DecrementPathRefCounts(normalizedKey);
                }

                for (int level = 0; level <= _depth; level++)
                {
                    var nodeKey = GetNodeKeyAtLevel(normalizedKey, level);
                    var cacheKey = $"{level}_{nodeKey}";
                    affectedPaths.Add(cacheKey);
                }
            }

            foreach (var cacheKey in affectedPaths)
            {
                await _storage.RemoveCachedNodeAsync(cacheKey);
            }

            _isDirty = true;
            _cachedRoot = null;
        }

        private async Task InvalidatePathToRootAsync(string leafKey)
        {
            var normalizedKey = leafKey.PadRight(_hexCharsNeeded, '0');

            for (int level = 0; level <= _depth; level++)
            {
                var nodeKey = GetNodeKeyAtLevel(normalizedKey, level);
                var cacheKey = $"{level}_{nodeKey}";
                await _storage.RemoveCachedNodeAsync(cacheKey);
            }
        }

        private void IncrementPathRefCounts(string normalizedKey)
        {
            for (int level = 0; level <= _depth; level++)
            {
                var nodeKey = GetNodeKeyAtLevel(normalizedKey, level);
                var cacheKey = $"{level}_{nodeKey}";
                if (_populatedNodeRefCounts.TryGetValue(cacheKey, out var count))
                    _populatedNodeRefCounts[cacheKey] = count + 1;
                else
                    _populatedNodeRefCounts[cacheKey] = 1;
            }
        }

        private void DecrementPathRefCounts(string normalizedKey)
        {
            for (int level = 0; level <= _depth; level++)
            {
                var nodeKey = GetNodeKeyAtLevel(normalizedKey, level);
                var cacheKey = $"{level}_{nodeKey}";
                if (_populatedNodeRefCounts.TryGetValue(cacheKey, out var count))
                {
                    if (count <= 1)
                        _populatedNodeRefCounts.Remove(cacheKey);
                    else
                        _populatedNodeRefCounts[cacheKey] = count - 1;
                }
            }
        }

        private string GetNodeKeyAtLevel(string leafKey, int level)
        {
            if (level == 0)
                return leafKey;

            if (level >= _depth)
                return new string('0', _hexCharsNeeded);

            var chars = leafKey.ToCharArray();

            if (_msbFirst)
            {
                for (int bit = _depth - level; bit < _depth; bit++)
                {
                    var hexIndex = bit / 4;
                    var bitInHex = 3 - (bit % 4);

                    if (hexIndex < chars.Length)
                    {
                        var hexValue = Convert.ToInt32(chars[hexIndex].ToString(), 16);
                        hexValue &= ~(1 << bitInHex);
                        chars[hexIndex] = hexValue.ToString("x")[0];
                    }
                }
            }
            else
            {
                for (int bit = 0; bit < level; bit++)
                {
                    var hexIndex = bit / 4;
                    var bitInHex = 3 - (bit % 4);

                    if (hexIndex < chars.Length)
                    {
                        var hexValue = Convert.ToInt32(chars[hexIndex].ToString(), 16);
                        hexValue &= ~(1 << bitInHex);
                        chars[hexIndex] = hexValue.ToString("x")[0];
                    }
                }
            }

            return new string(chars);
        }
    }
}
