using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Util.HashProviders;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Merkle.Sparse
{
    /// <summary>
    /// High-performance sparse merkle tree with pluggable storage
    /// Maintains binary tree structure but uses hex strings for efficient storage/indexing
    /// Optimized for millions of records with async database support
    /// </summary>
    public class SparseMerkleTree<T>
    {
        private readonly ISparseMerkleTreeStorage<T> _storage;
        private readonly IHashProvider _hashProvider;
        private readonly IByteArrayConvertor<T> _byteArrayConvertor;
        private readonly int _depth;
        private readonly string[] _emptyHashes;
        private readonly int _hexCharsNeeded;
        
        // PERFORMANCE OPTIMIZATION: Lazy root computation
        private string _cachedRoot;
        private bool _isDirty = true; // Start dirty to force initial computation

        public int Depth => _depth;
        public string EmptyLeafHash => _emptyHashes[0];
        public IHashProvider HashProvider => _hashProvider;

        public SparseMerkleTree(int depth, IHashProvider hashProvider, IByteArrayConvertor<T> byteArrayConvertor, ISparseMerkleTreeStorage<T> storage)
        {
            if (depth <= 0 || depth > 256)
                throw new ArgumentException("Depth must be between 1 and 256");
            
            _depth = depth;
            _hashProvider = hashProvider ?? throw new ArgumentNullException(nameof(hashProvider));
            _byteArrayConvertor = byteArrayConvertor ?? throw new ArgumentNullException(nameof(byteArrayConvertor));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            
            // Calculate hex characters needed: each hex char = 4 bits
            _hexCharsNeeded = (depth + 3) / 4; // Round up to next hex boundary
            
            // Precompute empty hashes
            _emptyHashes = new string[depth + 1];
            PrecomputeEmptyHashes();
        }

        public async Task SetLeafAsync(string key, T value)
        {
            if (value == null)
            {
                await _storage.RemoveLeafAsync(key);
            }
            else
            {
                await _storage.SetLeafAsync(key, value);
            }
            
            // OPTIMIZATION: Only invalidate the path from this leaf to root (O(log N) instead of O(N))
            await InvalidatePathToRootAsync(key);
            
            // Mark root as needing recomputation
            _isDirty = true;
            _cachedRoot = null;
        }

        public async Task<T> GetLeafAsync(string key)
        {
            var storedValue = await _storage.GetLeafAsync(key);
            return storedValue;
        }

        private async Task<string> GetLeafHashAsync(string key)
        {
            var storedValue = await _storage.GetLeafAsync(key);
            if (storedValue == null)
                return GetEmptyHash(0);
            
            // Convert value to bytes for hashing
            var valueBytes = _byteArrayConvertor.ConvertToByteArray(storedValue);
            var hash = _hashProvider.ComputeHash(valueBytes);
            return hash.ToHex();
        }

        // Synchronous methods for test compatibility
        public void SetLeaf(string key, T value)
        {
            SetLeafAsync(key, value).Wait();
        }

        public T GetLeaf(string key)
        {
            return GetLeafAsync(key).Result;
        }

        public string GetRootHash()
        {
            return GetRootHashAsync().Result;
        }

        public async Task<string> GetRootHashAsync()
        {
            // PERFORMANCE OPTIMIZATION: Return cached root if tree hasn't changed
            if (!_isDirty && _cachedRoot != null)
                return _cachedRoot;
            
            // Compute root only when needed
            var rootKey = new string('0', _hexCharsNeeded);
            _cachedRoot = await ComputeNodeHashAsync(rootKey, _depth);
            _isDirty = false;
            
            return _cachedRoot;
        }

        private async Task<string> ComputeNodeHashAsync(string nodeKey, int level)
        {
            // Base case: leaf level
            if (level == 0)
            {
                return await GetLeafHashAsync(nodeKey);
            }

            // Check cache
            var cacheKey = $"{level}_{nodeKey}";
            var cached = await _storage.GetCachedNodeAsync(cacheKey);
            if (cached != null)
            {
                return cached.ToHex();
            }

            // Check if subtree is empty
            if (!await _storage.HasLeavesInSubtreeAsync(nodeKey, level, _depth))
            {
                var emptyHash = GetEmptyHash(level);
                await _storage.SetCachedNodeAsync(cacheKey, emptyHash.HexToByteArray());
                return emptyHash;
            }

            // Compute from children
            var leftKey = ClearBit(nodeKey, level - 1);
            var rightKey = SetBit(nodeKey, level - 1);

            var leftHash = await ComputeNodeHashAsync(leftKey, level - 1);
            var rightHash = await ComputeNodeHashAsync(rightKey, level - 1);

            var nodeHash = CombineHashes(leftHash, rightHash);
            await _storage.SetCachedNodeAsync(cacheKey, nodeHash.HexToByteArray());

            return nodeHash;
        }

        private string SetBit(string hexKey, int bitPosition)
        {
            // Ensure we have enough hex characters
            var workingKey = hexKey.PadRight(_hexCharsNeeded, '0');
            var chars = workingKey.ToCharArray();
            
            var hexIndex = bitPosition / 4;
            var bitInHex = bitPosition % 4;
            
            if (hexIndex < chars.Length)
            {
                var hexValue = Convert.ToInt32(chars[hexIndex].ToString(), 16);
                hexValue |= (1 << (3 - bitInHex)); // MSB first within hex digit
                chars[hexIndex] = hexValue.ToString("x")[0];
            }
            
            return new string(chars);
        }

        private string ClearBit(string hexKey, int bitPosition)
        {
            // Ensure we have enough hex characters
            var workingKey = hexKey.PadRight(_hexCharsNeeded, '0');
            var chars = workingKey.ToCharArray();
            
            var hexIndex = bitPosition / 4;
            var bitInHex = bitPosition % 4;
            
            if (hexIndex < chars.Length)
            {
                var hexValue = Convert.ToInt32(chars[hexIndex].ToString(), 16);
                hexValue &= ~(1 << (3 - bitInHex)); // MSB first within hex digit
                chars[hexIndex] = hexValue.ToString("x")[0];
            }
            
            return new string(chars);
        }

        private void PrecomputeEmptyHashes()
        {
            _emptyHashes[0] = Hash("");

            for (int i = 1; i <= _depth; i++)
            {
                _emptyHashes[i] = CombineHashes(_emptyHashes[i - 1], _emptyHashes[i - 1]);
            }
        }

        private string GetEmptyHash(int level) => _emptyHashes[level];

        private string Hash(string input)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            return _hashProvider.ComputeHash(bytes).ToHex();
        }

        private string CombineHashes(string left, string right)
        {
            var leftBytes = left.HexToByteArray();
            var rightBytes = right.HexToByteArray();
            var combined = new byte[leftBytes.Length + rightBytes.Length];
            Array.Copy(leftBytes, 0, combined, 0, leftBytes.Length);
            Array.Copy(rightBytes, 0, combined, leftBytes.Length, rightBytes.Length);
            return _hashProvider.ComputeHash(combined).ToHex();
        }

        public async Task ClearAsync()
        {
            await _storage.ClearAsync();
        }

        public async Task<long> GetLeafCountAsync()
        {
            return await _storage.GetLeafCountAsync();
        }

        /// <summary>
        /// PERFORMANCE OPTIMIZATION: Batch update multiple leaves efficiently
        /// Critical for processing blocks with many transactions
        /// Minimizes cache invalidations and root computations
        /// </summary>
        public async Task SetLeavesAsync(Dictionary<string, T> keyValuePairs)
        {
            if (keyValuePairs == null || keyValuePairs.Count == 0)
                return;

            var affectedPaths = new HashSet<string>();

            // Process all leaf updates first
            foreach (var kvp in keyValuePairs)
            {
                if (kvp.Value == null)
                {
                    await _storage.RemoveLeafAsync(kvp.Key);
                }
                else
                {
                    await _storage.SetLeafAsync(kvp.Key, kvp.Value);
                }

                // Collect all affected paths for batch invalidation
                var normalizedKey = kvp.Key.PadRight(_hexCharsNeeded, '0');
                for (int level = 0; level <= _depth; level++)
                {
                    var nodeKey = GetNodeKeyAtLevel(normalizedKey, level);
                    var cacheKey = $"{level}_{nodeKey}"; // FIX: Match the format used in ComputeNodeHashAsync
                    affectedPaths.Add(cacheKey);
                }
            }

            // Batch invalidate all affected paths (more efficient than individual invalidations)
            foreach (var cacheKey in affectedPaths)
            {
                await _storage.RemoveCachedNodeAsync(cacheKey);
            }

            // Mark root as needing recomputation
            _isDirty = true;
            _cachedRoot = null;
        }

        /// <summary>
        /// PERFORMANCE OPTIMIZATION: Only invalidate the path from changed leaf to root
        /// This reduces cache invalidation from O(N) to O(log N) operations
        /// Critical for handling millions of records efficiently
        /// </summary>
        private async Task InvalidatePathToRootAsync(string leafKey)
        {
            // Normalize the key to ensure proper length
            var normalizedKey = leafKey.PadRight(_hexCharsNeeded, '0');
            
            // For each level from leaf (0) to root (depth), invalidate only the affected node
            for (int level = 0; level <= _depth; level++)
            {
                var nodeKey = GetNodeKeyAtLevel(normalizedKey, level);
                var cacheKey = $"{level}_{nodeKey}"; // FIX: Match the format used in ComputeNodeHashAsync
                await _storage.RemoveCachedNodeAsync(cacheKey);
            }
        }

        /// <summary>
        /// Calculate the node key at a specific level for a given leaf key
        /// This determines which nodes are affected by a leaf change
        /// </summary>
        private string GetNodeKeyAtLevel(string leafKey, int level)
        {
            if (level == 0)
                return leafKey; // Leaf level - exact key
            
            if (level >= _depth)
                return new string('0', _hexCharsNeeded); // Root level - all zeros
            
            // For intermediate levels, mask out the bits that haven't been "decided" yet
            // At level L, bits [0, L-1] are variable, bits [L, depth-1] are fixed
            var chars = leafKey.ToCharArray();
            
            // Clear the bits that are variable at this level
            for (int bit = 0; bit < level; bit++)
            {
                var hexIndex = bit / 4;
                var bitInHex = 3 - (bit % 4); // MSB first within hex digit
                
                if (hexIndex < chars.Length)
                {
                    var hexValue = Convert.ToInt32(chars[hexIndex].ToString(), 16);
                    hexValue &= ~(1 << bitInHex); // Clear this bit
                    chars[hexIndex] = hexValue.ToString("x")[0];
                }
            }
            
            return new string(chars);
        }
    }
}