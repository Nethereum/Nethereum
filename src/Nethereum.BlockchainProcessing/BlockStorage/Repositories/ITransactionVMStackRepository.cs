using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Newtonsoft.Json.Linq;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface ITransactionVMStackRepository
    { 
        Task UpsertAsync(string transactionHash, string address, JObject stackTrace);
        Task<ITransactionVmStackView> FindByTransactionHashAync(string hash);
        Task<ITransactionVmStackView> FindByAddressAndTransactionHashAync(string address, string hash);
    }
}