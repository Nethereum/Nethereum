using System.Threading.Tasks;

namespace Nethereum.Merkle.Sparse
{
    /// <summary>
    /// High-performance storage interface for sparse Merkle tree data
    /// Stores actual values of type T, with async support for database backends
    /// </summary>
    public interface ISparseMerkleTreeStorage<T>
    {
        // Core leaf operations - store actual values of type T
        Task SetLeafAsync(string key, T value);
        Task<T> GetLeafAsync(string key);
        Task RemoveLeafAsync(string key);
        Task<bool> HasLeavesInSubtreeAsync(string nodeKey, int level, int treeDepth);

        // Node caching for performance - cache computed hashes as byte arrays
        Task SetCachedNodeAsync(string key, byte[] hash);
        Task<byte[]> GetCachedNodeAsync(string key);
        Task RemoveCachedNodeAsync(string key);

        // Bulk operations
        Task ClearAsync();
        Task ClearCacheAsync();
        
        // Statistics
        Task<long> GetLeafCountAsync();
        Task<bool> IsOptimizedForLargeDatasets();
    }

}
