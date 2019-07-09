using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.Storage.Entities;
using Transaction = Nethereum.RPC.Eth.DTOs.Transaction;

namespace Nethereum.BlockchainProcessing.Storage.Repositories
{
    public interface IContractRepository
    {
        Task FillCache();
        Task UpsertAsync(string contractAddress, string code, Transaction transaction);
        Task<bool> ExistsAsync(string contractAddress);

        Task<IContractView> FindByAddressAsync(string contractAddress);
        bool IsCached(string contractAddress);
    }
}