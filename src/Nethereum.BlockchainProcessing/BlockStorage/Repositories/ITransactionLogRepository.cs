using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface ITransactionLogRepository
    {
        Task UpsertAsync(FilterLogVO log);
        Task<ITransactionLogView> FindByTransactionHashAndLogIndexAsync(string hash, BigInteger logIndex);
    }
}