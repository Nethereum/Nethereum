using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public class InMemoryTokenTransferLogRepository : ITokenTransferLogRepository, INonCanonicalTokenTransferLogRepository
    {
        public List<TokenTransferLog> Records { get; set; }

        public InMemoryTokenTransferLogRepository()
        {
            Records = new List<TokenTransferLog>();
        }

        public InMemoryTokenTransferLogRepository(List<TokenTransferLog> records)
        {
            Records = records;
        }

        public Task<ITokenTransferLogView> FindByTransactionHashAndLogIndexAsync(string hash, BigInteger logIndex)
        {
            var idx = (long)logIndex;
            return Task.FromResult<ITokenTransferLogView>(
                Records.FirstOrDefault(r => r.TransactionHash == hash && r.LogIndex == idx));
        }

        public async Task UpsertAsync(TokenTransferLog log)
        {
            var existing = await FindByTransactionHashAndLogIndexAsync(log.TransactionHash, new BigInteger(log.LogIndex)).ConfigureAwait(false);
            if (existing != null) Records.Remove((TokenTransferLog)existing);
            log.UpdateRowDates();
            Records.Add(log);
        }

        public Task<IEnumerable<ITokenTransferLogView>> GetByAddressAsync(string address, int page, int pageSize)
        {
            var addressLower = address?.ToLowerInvariant();
            var results = Records
                .Where(r => r.IsCanonical &&
                    (r.FromAddress?.ToLowerInvariant() == addressLower ||
                     r.ToAddress?.ToLowerInvariant() == addressLower))
                .OrderByDescending(r => r.BlockNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Cast<ITokenTransferLogView>();
            return Task.FromResult(results);
        }

        public Task<IEnumerable<ITokenTransferLogView>> GetByContractAsync(string contractAddress, int page, int pageSize)
        {
            var addressLower = contractAddress?.ToLowerInvariant();
            var results = Records
                .Where(r => r.IsCanonical && r.ContractAddress?.ToLowerInvariant() == addressLower)
                .OrderByDescending(r => r.BlockNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Cast<ITokenTransferLogView>();
            return Task.FromResult(results);
        }

        public Task<IEnumerable<ITokenTransferLogView>> GetByBlockNumberAsync(long blockNumber)
        {
            var results = Records
                .Where(r => r.IsCanonical && r.BlockNumber == blockNumber)
                .Cast<ITokenTransferLogView>();
            return Task.FromResult(results);
        }

        public Task MarkNonCanonicalAsync(BigInteger blockNumber)
        {
            var blockNum = (long)blockNumber;
            foreach (var record in Records)
            {
                if (record.BlockNumber == blockNum)
                {
                    record.IsCanonical = false;
                }
            }
            return Task.FromResult(0);
        }
    }
}
