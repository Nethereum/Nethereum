using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface IBlockStore
    {
        Task<BlockHeader> GetByHashAsync(byte[] hash);
        Task<BlockHeader> GetByNumberAsync(BigInteger number);
        Task<BlockHeader> GetLatestAsync();
        Task<BigInteger> GetHeightAsync();
        Task SaveAsync(BlockHeader header, byte[] blockHash);
        Task<bool> ExistsAsync(byte[] hash);
        Task<byte[]> GetHashByNumberAsync(BigInteger number);
    }
}
