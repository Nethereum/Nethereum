using Nethereum.Merkle.Sparse.Storage;

namespace Nethereum.Merkle.Sparse
{
    /// <summary>
    /// In-memory implementation of ISparseMerkleTreeStorage<T>
    /// Suitable for development, testing, and small datasets
    /// Uses the abstract base class with an in-memory repository
    /// </summary>
    public class InMemorySparseMerkleTreeStorage<T> : SparseMerkleTreeStorageBase<T>
    {
        public InMemorySparseMerkleTreeStorage() 
            : base(new InMemorySparseMerkleRepository<T>())
        {
        }

        public InMemorySparseMerkleTreeStorage(ISparseMerkleRepository<T> repository) 
            : base(repository)
        {
        }
    }
}