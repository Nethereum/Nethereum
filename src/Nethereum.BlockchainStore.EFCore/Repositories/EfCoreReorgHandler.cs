using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;

namespace Nethereum.BlockchainStore.EFCore.Repositories
{
    public class EfCoreReorgHandler : RepositoryBase, IReorgHandler
    {
        public EfCoreReorgHandler(IBlockchainDbContextFactory contextFactory) : base(contextFactory)
        {
        }

        public async Task MarkBlockRangeNonCanonicalAsync(BigInteger fromBlock, BigInteger toBlock)
        {
            using (var context = _contextFactory.CreateContext())
            {
                using (var transaction = await context.Database.BeginTransactionAsync().ConfigureAwait(false))
                {
                    try
                    {
                        for (var blockNumber = fromBlock; blockNumber <= toBlock; blockNumber++)
                        {
                            var blockNum = (long)blockNumber;

                            var blocks = await context.Blocks
                                .Where(b => b.BlockNumber == blockNum && b.IsCanonical)
                                .ToListAsync()
                                .ConfigureAwait(false);
                            foreach (var block in blocks)
                                block.IsCanonical = false;
                            if (blocks.Count > 0)
                                context.Blocks.UpdateRange(blocks);

                            var transactions = await context.Transactions
                                .Where(t => t.BlockNumber == blockNum && t.IsCanonical)
                                .ToListAsync()
                                .ConfigureAwait(false);
                            foreach (var tx in transactions)
                                tx.IsCanonical = false;
                            if (transactions.Count > 0)
                                context.Transactions.UpdateRange(transactions);

                            var logs = await context.TransactionLogs
                                .Where(l => l.BlockNumber == blockNum && l.IsCanonical)
                                .ToListAsync()
                                .ConfigureAwait(false);
                            foreach (var log in logs)
                                log.IsCanonical = false;
                            if (logs.Count > 0)
                                context.TransactionLogs.UpdateRange(logs);

                            var internalTxs = await context.InternalTransactions
                                .Where(i => i.BlockNumber == blockNum && i.IsCanonical)
                                .ToListAsync()
                                .ConfigureAwait(false);
                            foreach (var itx in internalTxs)
                                itx.IsCanonical = false;
                            if (internalTxs.Count > 0)
                                context.InternalTransactions.UpdateRange(internalTxs);
                        }

                        await context.SaveChangesAsync().ConfigureAwait(false);
                        await transaction.CommitAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        await transaction.RollbackAsync().ConfigureAwait(false);
                        throw;
                    }
                }
            }
        }
    }
}
