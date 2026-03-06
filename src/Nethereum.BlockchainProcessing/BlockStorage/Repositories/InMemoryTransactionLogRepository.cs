using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public class InMemoryTransactionLogRepository : ITransactionLogRepository, INonCanonicalTransactionLogRepository
    {
        private readonly object _lock = new object();

        public List<ITransactionLogView> Records { get; set; }

        public InMemoryTransactionLogRepository(List<ITransactionLogView> records)
        {
            Records = records;
        }

        public Task<ITransactionLogView> FindByTransactionHashAndLogIndexAsync(string hash, BigInteger logIndex)
        {
            lock (_lock)
            {
                var idx = (long)logIndex;
                return Task.FromResult(Records.FirstOrDefault(r => r.TransactionHash == hash && r.LogIndex == idx));
            }
        }

        public async Task UpsertAsync(FilterLogVO log)
        {
            var record = await FindByTransactionHashAndLogIndexAsync(log.Log.TransactionHash, log.Log.LogIndex).ConfigureAwait(false);
            lock (_lock)
            {
                if (record != null) Records.Remove(record);
                Records.Add(log.MapToStorageEntityForUpsert());
            }
        }

        public Task MarkNonCanonicalAsync(BigInteger blockNumber)
        {
            lock (_lock)
            {
                var blockNum = (long)blockNumber;
                foreach (var record in Records)
                {
                    if (record is TransactionLog txLog && txLog.BlockNumber == blockNum)
                    {
                        txLog.IsCanonical = false;
                    }
                }
            }

            return Task.FromResult(0);
        }
    }
}
