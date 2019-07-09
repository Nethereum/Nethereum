using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.ProgressRepositories
{
    public interface IBlockProgressRepository
    {
        Task UpsertProgressAsync(BigInteger blockNumber);
        Task<BigInteger?> GetLastBlockNumberProcessedAsync();
    }
}