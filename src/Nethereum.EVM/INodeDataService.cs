using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.EVM
{
    public interface INodeDataService
    {
        Task<BigInteger> GetBalanceAsync(byte[] address);
        Task<byte[]> GetCodeAsync(byte[] address);
        Task<byte[]> GetBlockHashAsync(BigInteger blockNumber);
        Task<byte[]> GetStorageAtAsync(byte[] address, BigInteger position);
    }
}