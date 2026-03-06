using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.Services;

namespace Nethereum.BlockchainStore.EFCore.Repositories
{
    public class InternalTransactionRepository : RepositoryBase, IInternalTransactionRepository, INonCanonicalInternalTransactionRepository
    {
        public InternalTransactionRepository(IBlockchainDbContextFactory contextFactory) : base(contextFactory)
        {
        }

        public async Task UpsertAsync(InternalTransaction internalTransaction)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var existing = await context.InternalTransactions
                    .FirstOrDefaultAsync(i => i.TransactionHash == internalTransaction.TransactionHash
                        && i.TraceIndex == internalTransaction.TraceIndex)
                    .ConfigureAwait(false);

                if (existing != null)
                {
                    existing.Depth = internalTransaction.Depth;
                    existing.Type = internalTransaction.Type;
                    existing.AddressFrom = internalTransaction.AddressFrom;
                    existing.AddressTo = internalTransaction.AddressTo;
                    existing.Value = internalTransaction.Value;
                    existing.Gas = internalTransaction.Gas;
                    existing.GasUsed = internalTransaction.GasUsed;
                    existing.Input = internalTransaction.Input;
                    existing.Output = internalTransaction.Output;
                    existing.Error = internalTransaction.Error;
                    existing.RevertReason = internalTransaction.RevertReason;
                    existing.BlockNumber = internalTransaction.BlockNumber;
                    existing.BlockHash = internalTransaction.BlockHash;
                    existing.IsCanonical = internalTransaction.IsCanonical;
                    existing.UpdateRowDates();
                    context.InternalTransactions.Update(existing);
                }
                else
                {
                    internalTransaction.UpdateRowDates();
                    context.InternalTransactions.Add(internalTransaction);
                }

                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task<List<IInternalTransactionView>> GetByTransactionHashAsync(string txHash)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var results = await context.InternalTransactions
                    .AsNoTracking()
                    .Where(i => i.TransactionHash == txHash && i.IsCanonical)
                    .OrderBy(i => i.TraceIndex)
                    .ToListAsync()
                    .ConfigureAwait(false);
                return results.Cast<IInternalTransactionView>().ToList();
            }
        }

        public async Task<List<IInternalTransactionView>> GetByAddressAsync(string address, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;

            var normalizedAddress = address?.ToLowerInvariant();
            using (var context = _contextFactory.CreateContext())
            {
                var results = await context.InternalTransactions
                    .AsNoTracking()
                    .Where(i => i.IsCanonical &&
                        (i.AddressFrom == normalizedAddress || i.AddressTo == normalizedAddress))
                    .OrderByDescending(i => i.RowIndex)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync()
                    .ConfigureAwait(false);
                return results.Cast<IInternalTransactionView>().ToList();
            }
        }

        public async Task MarkNonCanonicalAsync(BigInteger blockNumber)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var blockNum = (long)blockNumber;
                var records = await context.InternalTransactions
                    .Where(i => i.BlockNumber == blockNum && i.IsCanonical)
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (records.Count == 0) return;

                foreach (var record in records)
                {
                    record.IsCanonical = false;
                }

                context.InternalTransactions.UpdateRange(records);
                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task<List<TransactionToTrace>> GetContractTransactionsInRangeAsync(BigInteger fromBlock, BigInteger toBlock)
        {
            var from = (long)fromBlock;
            var to = (long)toBlock;

            using (var context = _contextFactory.CreateContext())
            {
                var tracedHashes = context.InternalTransactions
                    .AsNoTracking()
                    .Select(i => i.TransactionHash)
                    .Distinct();

                var results = await context.Transactions
                    .AsNoTracking()
                    .Where(t => t.IsCanonical
                        && t.Input != null
                        && t.Input != "0x"
                        && t.BlockNumber >= from
                        && t.BlockNumber <= to
                        && !tracedHashes.Contains(t.Hash))
                    .OrderBy(t => t.BlockNumber)
                    .Select(t => new TransactionToTrace
                    {
                        TransactionHash = t.Hash,
                        BlockNumber = t.BlockNumber.ToString(),
                        BlockHash = t.BlockHash
                    })
                    .ToListAsync()
                    .ConfigureAwait(false);

                return results;
            }
        }
    }
}
