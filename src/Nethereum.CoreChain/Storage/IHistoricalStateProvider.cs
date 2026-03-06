using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface IHistoricalStateProvider
    {
        void SetCurrentBlockNumber(BigInteger blockNumber);
        Task ClearCurrentBlockNumberAsync();
        Task<Account> GetAccountAtBlockAsync(string address, BigInteger blockNumber);
        Task<byte[]> GetStorageAtBlockAsync(string address, BigInteger slot, BigInteger blockNumber);
    }
}
