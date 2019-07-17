using Nethereum.BlockchainProcessing.Storage.Entities;
using Nethereum.BlockchainProcessing.Storage.Entities.Mapping;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.Storage.Repositories
{
    public class InMemoryTransactionLogRepository : ITransactionLogRepository
    {
        public List<ITransactionLogView> Records { get; set; } 

        public InMemoryTransactionLogRepository(List<ITransactionLogView> records)
        {
            Records = records;
        }

        public Task<ITransactionLogView> FindByTransactionHashAndLogIndexAsync(string hash, BigInteger logIndex)
        {
            return Task.FromResult(Records.FirstOrDefault(r => r.TransactionHash == hash && r.LogIndex == logIndex.ToString()));
        }

        public async Task UpsertAsync(FilterLogVO log)
        {
            var record = await FindByTransactionHashAndLogIndexAsync(log.Transaction.TransactionHash, log.Log.LogIndex);
            if(record != null) Records.Remove(record);
            Records.Add(log.MapToStorageEntityForUpsert());
        }
    }
}
