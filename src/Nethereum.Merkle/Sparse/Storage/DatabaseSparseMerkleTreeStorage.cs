using System;

namespace Nethereum.Merkle.Sparse.Storage
{
    /// <summary>
    /// Database implementation of ISparseMerkleTreeStorage<T>
    /// Uses a repository pattern to work with any database provider
    /// Optimized for large datasets with async operations
    /// </summary>
    public class DatabaseSparseMerkleTreeStorage<T> : SparseMerkleTreeStorageBase<T>
    {
        public DatabaseSparseMerkleTreeStorage(ISparseMerkleRepository<T> repository) 
            : base(repository)
        {
            if (repository == null)
                throw new ArgumentNullException(nameof(repository));
        }

        // All functionality is provided by the abstract base class
        // The repository handles the actual database operations
        
        // This class can be extended in the future to add database-specific optimizations
        // such as:
        // - Connection pooling
        // - Transaction management
        // - Bulk operation optimizations
        // - Database-specific query optimizations
    }
}