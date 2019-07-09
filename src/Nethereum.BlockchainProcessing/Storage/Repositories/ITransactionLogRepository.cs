using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.Storage.Entities;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Storage.Repositories
{
    public interface ITransactionLogRepository
    {
        Task UpsertAsync(FilterLogVO log);
        Task<ITransactionLogView> FindByTransactionHashAndLogIndexAsync(string hash, long idx);
    }
}