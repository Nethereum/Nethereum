using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Merkle.Sparse.Storage;

namespace Nethereum.Merkle.Sparse.Storage
{
    /// <summary>
    /// Abstract base class for sparse Merkle tree storage implementations
    /// Provides common functionality shared between in-memory and database storage
    /// </summary>
    public abstract class SparseMerkleTreeStorageBase<T> : ISparseMerkleTreeStorage<T>
    {
        protected readonly ISparseMerkleRepository<T> _repository;

        protected SparseMerkleTreeStorageBase(ISparseMerkleRepository<T> repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public virtual async Task SetLeafAsync(string key, T value)
        {
            if (value == null)
            {
                await _repository.RemoveLeafAsync(key);
            }
            else
            {
                await _repository.SetLeafAsync(key, value);
            }
        }

        public virtual async Task<T> GetLeafAsync(string key)
        {
            return await _repository.GetLeafAsync(key);
        }

        public virtual async Task RemoveLeafAsync(string key)
        {
            await _repository.RemoveLeafAsync(key);
        }

        public virtual async Task<bool> HasLeavesInSubtreeAsync(string nodeKey, int level, int treeDepth)
        {
            // For leaf level (level 0), check if this exact leaf exists
            if (level == 0)
            {
                return await _repository.LeafExistsAsync(nodeKey);
            }

            // For root level, check if any leaves exist
            if (level >= treeDepth)
            {
                var leafCount = await _repository.GetLeafCountAsync();
                return leafCount > 0;
            }

            // For intermediate levels, check if any stored leaf would be in this subtree
            var allLeafKeys = await _repository.GetLeafKeysAsync();
            
            foreach (var leafKey in allLeafKeys)
            {
                if (IsInSubtree(leafKey, nodeKey, level, treeDepth))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsInSubtree(string leafKey, string nodeKey, int level, int treeDepth)
        {
            if (level >= treeDepth) return true;

            // Calculate hex characters needed: each hex char = 4 bits
            var hexCharsNeeded = (treeDepth + 3) / 4;

            // Ensure both keys are properly padded
            var paddedLeafKey = leafKey.PadRight(hexCharsNeeded, '0');
            var paddedNodeKey = nodeKey.PadRight(hexCharsNeeded, '0');

            // At level L, we check if the leaf would be in the subtree defined by nodeKey
            // The key insight: at level L, we need to check if the paths from level L to the leaves match
            // This means checking prefix bits [0, level-1] (path to this node) and 
            // ensuring remaining bits can lead to the leaf
            
            // First, check if they share the same prefix up to this level
            for (int bit = 0; bit < level; bit++)
            {
                int hexIndex = bit / 4;
                int bitInHex = bit % 4;
                
                // Get the hex characters at this position
                char leafHexChar = hexIndex < paddedLeafKey.Length ? paddedLeafKey[hexIndex] : '0';
                char nodeHexChar = hexIndex < paddedNodeKey.Length ? paddedNodeKey[hexIndex] : '0';
                
                // Convert to int to extract specific bit
                int leafHexValue = Convert.ToInt32(leafHexChar.ToString(), 16);
                int nodeHexValue = Convert.ToInt32(nodeHexChar.ToString(), 16);
                
                // Check specific bit within the hex character (MSB first: bit 3,2,1,0)
                int bitMask = 1 << (3 - bitInHex);
                bool leafBit = (leafHexValue & bitMask) != 0;
                bool nodeBit = (nodeHexValue & bitMask) != 0;
                
                if (leafBit != nodeBit)
                {
                    return false;
                }
            }

            return true;
        }

        public virtual async Task SetCachedNodeAsync(string key, byte[] hash)
        {
            await _repository.SetCachedNodeAsync(key, hash);
        }

        public virtual async Task<byte[]> GetCachedNodeAsync(string key)
        {
            return await _repository.GetCachedNodeAsync(key);
        }

        public virtual async Task RemoveCachedNodeAsync(string key)
        {
            await _repository.RemoveCachedNodeAsync(key);
        }

        public virtual async Task ClearAsync()
        {
            await _repository.ClearAllLeavesAsync();
            await _repository.ClearAllCacheAsync();
        }

        public virtual async Task ClearCacheAsync()
        {
            await _repository.ClearAllCacheAsync();
        }

        public virtual async Task<long> GetLeafCountAsync()
        {
            return await _repository.GetLeafCountAsync();
        }

        public virtual async Task<bool> IsOptimizedForLargeDatasets()
        {
            return await _repository.IsOptimizedForLargeDatasets();
        }
    }
}