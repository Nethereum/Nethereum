using Nethereum.Documentation;
using System.Threading.Tasks;

namespace Nethereum.Merkle.Sparse
{
    [NethereumDocExample(DocSection.SmartContracts, "sparse-merkle-tree", "Persistence for sparse binary Merkle tree nodes")]
    public interface ISmtNodeStorage
    {
        Task<byte[]> GetAsync(byte[] hash);
        Task PutAsync(byte[] hash, byte[] data);
        Task DeleteAsync(byte[] hash);
    }
}
