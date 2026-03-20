using System.Threading.Tasks;

namespace Nethereum.Merkle.Sparse
{
    public interface ISmtNodeStorage
    {
        Task<byte[]> GetAsync(byte[] hash);
        Task PutAsync(byte[] hash, byte[] data);
        Task DeleteAsync(byte[] hash);
    }
}
