using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Merkle.Sparse.Storage
{
    /// <summary>
    /// Repository interface for sparse Merkle tree data persistence
    /// Provides database-agnostic storage operations for leaves and cached nodes
    /// </summary>
    public interface ISparseMerkleRepository<T>
    {
        // Core leaf operations - store actual values of type T
        Task<T> GetLeafAsync(string key);
        Task SetLeafAsync(string key, T value);
        Task RemoveLeafAsync(string key);
        Task<bool> LeafExistsAsync(string key);
        Task<long> GetLeafCountAsync();

        // Node caching operations - cache computed hashes as byte arrays
        Task<byte[]> GetCachedNodeAsync(string key);
        Task SetCachedNodeAsync(string key, byte[] hash);
        Task RemoveCachedNodeAsync(string key);

        // Bulk operations for performance
        Task SetLeavesBatchAsync(Dictionary<string, T> leaves);
        Task RemoveLeavesBatchAsync(IEnumerable<string> keys);
        Task SetCachedNodesBatchAsync(Dictionary<string, byte[]> nodes);
        Task RemoveCachedNodesBatchAsync(IEnumerable<string> keys);

        // Clear operations
        Task ClearAllLeavesAsync();
        Task ClearAllCacheAsync();

        // Query capabilities for subtree operations
        Task<IEnumerable<string>> GetLeafKeysAsync();
        Task<IEnumerable<string>> GetLeafKeysWithPrefixAsync(string prefix);
        Task<bool> HasAnyLeafWithKeyPrefixAsync(string keyPrefix);

        // Performance and optimization info
        Task<bool> IsOptimizedForLargeDatasets();
    }
}