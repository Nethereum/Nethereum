using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Newtonsoft.Json.Linq;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface ITransactionVMStackRepository
    { 
        Task UpsertAsync(string transactionHash, string address, JObject stackTrace);
        Task<ITransactionVmStackView> FindByTransactionHashAsync(string hash);
        Task<ITransactionVmStackView> FindByAddressAndTransactionHashAsync(string address, string hash);
    }
}