using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface IContractRepository
    {
        Task FillCache();
        Task UpsertAsync(ContractCreationVO contractCreation);
        Task<bool> ExistsAsync(string contractAddress);

        Task<IContractView> FindByAddressAsync(string contractAddress);
        bool IsCached(string contractAddress);
    }
}