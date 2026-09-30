using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public class InMemoryBlockAccessListRepository : IBlockAccessListRepository
    {
        private readonly object _lock = new object();

        public List<BlockAccessListAccount> Records { get; set; }

        public InMemoryBlockAccessListRepository(List<BlockAccessListAccount> records)
        {
            Records = records;
        }

        public Task UpsertAsync(AccountAccess account, long blockNumber, string blockHash)
        {
            lock (_lock)
            {
                var existing = Records.FirstOrDefault(r => r.BlockNumber == blockNumber && r.Address == account.Address);
                if (existing != null) Records.Remove(existing);

                var entity = existing ?? new BlockAccessListAccount();
                entity.MapToStorageEntityForUpsert(account, blockNumber, blockHash);

                Records.Add(entity);
            }

            return Task.FromResult(0);
        }

        public Task<IReadOnlyList<AccountAccess>> GetForBlockAsync(long blockNumber)
        {
            lock (_lock)
            {
                IReadOnlyList<AccountAccess> result = Records
                    .Where(r => r.BlockNumber == blockNumber && r.IsCanonical)
                    .Select(r => r.ToAccountAccess())
                    .ToList();

                return Task.FromResult(result);
            }
        }

        public Task MarkNonCanonicalAsync(System.Numerics.BigInteger blockNumber)
        {
            lock (_lock)
            {
                var blockNum = (long)blockNumber;
                foreach (var record in Records)
                {
                    if (record.BlockNumber == blockNum)
                    {
                        record.IsCanonical = false;
                    }
                }
            }

            return Task.FromResult(0);
        }
    }
}
