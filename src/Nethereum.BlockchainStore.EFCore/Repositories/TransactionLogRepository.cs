using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.RPC.Eth.DTOs;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainStore.EFCore.Repositories
{
    public class TransactionLogRepository : RepositoryBase, ITransactionLogRepository, INonCanonicalTransactionLogRepository
    {
        public TransactionLogRepository(IBlockchainDbContextFactory contextFactory) : base(contextFactory)
        {
        }

        public async Task<ITransactionLogView> FindByTransactionHashAndLogIndexAsync(string hash, BigInteger idx)
        {
            using (var context = _contextFactory.CreateContext())
            {
                return await context.TransactionLogs.FindByTransactionHashAndLogIndexAsync(hash, idx).ConfigureAwait(false);
            }
        }

        public async Task UpsertAsync(FilterLog log)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var transactionLog = await context.TransactionLogs.FindByTransactionHashAndLogIndexAsync(log.TransactionHash, log.LogIndex.ToLong()).ConfigureAwait(false) 
                          ?? new TransactionLog();

                transactionLog.Map(log);
                transactionLog.UpdateRowDates();

                if (transactionLog.IsNew())
                    context.TransactionLogs.Add(transactionLog);
                else
                    context.TransactionLogs.Update(transactionLog);

                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task UpsertAsync(FilterLogVO log)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var transactionLog = await context.TransactionLogs.FindByTransactionHashAndLogIndexAsync(log.Log.TransactionHash, log.Log.LogIndex.ToLong()).ConfigureAwait(false)
                          ?? new TransactionLog();

                transactionLog.MapToStorageEntityForUpsert(log);

                if (transactionLog.IsNew())
                    context.TransactionLogs.Add(transactionLog);
                else
                    context.TransactionLogs.Update(transactionLog);

                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task MarkNonCanonicalAsync(BigInteger blockNumber)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var blockNum = (long)blockNumber;
                var logs = await context.TransactionLogs
                    .Where(l => l.BlockNumber == blockNum && l.IsCanonical)
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (logs.Count == 0)
                {
                    return;
                }

                foreach (var log in logs)
                {
                    log.IsCanonical = false;
                }

                context.TransactionLogs.UpdateRange(logs);
                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}
