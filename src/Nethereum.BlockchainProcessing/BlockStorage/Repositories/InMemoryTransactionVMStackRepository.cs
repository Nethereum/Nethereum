using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.Util;
using Newtonsoft.Json.Linq;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public class InMemoryTransactionVMStackRepository : ITransactionVMStackRepository
    {
        public List<ITransactionVmStackView> Records { get; set;}

        public InMemoryTransactionVMStackRepository(List<ITransactionVmStackView> records)
        {
            Records = records;
        }

        public Task<ITransactionVmStackView> FindByAddressAndTransactionHashAync(string address, string hash)
        {
            return Task.FromResult(
                Records
                .FirstOrDefault(r => AddressUtil.Current.AreAddressesTheSame(r.Address, address) 
                    && r.TransactionHash == hash));
        }

        public Task<ITransactionVmStackView> FindByTransactionHashAync(string hash)
        {
            return Task.FromResult(
                Records
                .FirstOrDefault(r => r.TransactionHash == hash));
        }

        public async Task UpsertAsync(string transactionHash, string address, JObject stackTrace)
        {
            var record = await FindByAddressAndTransactionHashAync(address, transactionHash);
            if(record != null) Records.Remove(record);
            Records.Add(stackTrace.MapToStorageEntityForUpsert<TransactionVmStack>(transactionHash, address));
        }
    }
}
