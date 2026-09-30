using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public class InMemoryInternalTransactionRepository : IInternalTransactionRepository, INonCanonicalInternalTransactionRepository
    {
        public List<IInternalTransactionView> Records { get; set; }

        public InMemoryInternalTransactionRepository(List<IInternalTransactionView> records)
        {
            Records = records;
        }

        public Task<IInternalTransactionView> FindByTransactionHashAndTraceIndexAsync(string txHash, int traceIndex)
        {
            return Task.FromResult(Records.FirstOrDefault(r => r.TransactionHash == txHash && r.TraceIndex == traceIndex));
        }

        public async Task UpsertAsync(InternalTransaction internalTransaction)
        {
            var existing = await FindByTransactionHashAndTraceIndexAsync(
                internalTransaction.TransactionHash, internalTransaction.TraceIndex).ConfigureAwait(false);
            if (existing != null) Records.Remove(existing);
            Records.Add(internalTransaction);
        }

        public Task<List<IInternalTransactionView>> GetByTransactionHashAsync(string txHash)
        {
            var results = Records
                .Where(r => r.TransactionHash == txHash && r.IsCanonical)
                .OrderBy(r => r.TraceIndex)
                .ToList();
            return Task.FromResult(results);
        }

        public Task<List<IInternalTransactionView>> GetByAddressAsync(string address, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;

            var normalizedAddress = address?.ToLowerInvariant();
            var results = Records
                .Where(r => r.IsCanonical &&
                    (r.AddressFrom?.ToLowerInvariant() == normalizedAddress ||
                     r.AddressTo?.ToLowerInvariant() == normalizedAddress))
                .OrderByDescending(r => r.BlockNumber)
                .ThenBy(r => r.TraceIndex)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return Task.FromResult(results);
        }

        public Task<List<TransactionToTrace>> GetContractTransactionsInRangeAsync(BigInteger fromBlock, BigInteger toBlock)
        {
            // In-memory repo doesn't know about outer Transactions records — return nothing.
            // Hosts that care should supply a real repository; this implementation exists so
            // orchestration tests can wire the pipeline without an EF backend.
            return Task.FromResult(new List<TransactionToTrace>());
        }

        public Task MarkNonCanonicalAsync(BigInteger blockNumber)
        {
            var blockNum = (long)blockNumber;
            foreach (var record in Records)
            {
                if (record is InternalTransaction itx && itx.BlockNumber == blockNum)
                {
                    itx.IsCanonical = false;
                }
            }

            return Task.FromResult(0);
        }
    }
}
