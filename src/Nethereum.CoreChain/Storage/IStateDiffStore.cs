using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface IStateDiffStore
    {
        Task SaveBlockDiffAsync(BlockStateDiff diff);

        Task<(bool Found, Account PreValue)> GetFirstAccountPreValueAfterBlockAsync(string address, BigInteger blockNumber);

        Task<(bool Found, byte[] PreValue)> GetFirstStoragePreValueAfterBlockAsync(string address, BigInteger slot, BigInteger blockNumber);

        Task DeleteDiffsAboveBlockAsync(BigInteger blockNumber);

        Task DeleteDiffsBelowBlockAsync(BigInteger blockNumber);

        Task<BigInteger?> GetOldestDiffBlockAsync();

        Task<BigInteger?> GetNewestDiffBlockAsync();
    }
}
