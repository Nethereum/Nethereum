using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.EVM.BlockchainState
{
    public interface INodeDataService
    {
        Task<BigInteger> GetBalanceAsync(byte[] address);
        Task<BigInteger> GetBalanceAsync(string address);
        Task<byte[]> GetCodeAsync(byte[] address);
        Task<byte[]> GetCodeAsync(string address);
        Task<byte[]> GetBlockHashAsync(BigInteger blockNumber);
        Task<byte[]> GetStorageAtAsync(byte[] address, BigInteger position);
        Task<byte[]> GetStorageAtAsync(string address, BigInteger position);
        Task<BigInteger> GetTransactionCount(byte[] address);
        Task<BigInteger> GetTransactionCount(string address);
    }
}