using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Mud.Repositories.EntityFramework
{
    public class BlockProgressRepository<TDbContext> : IBlockProgressRepository where TDbContext : DbContext, IMudStoreRecordsDbSets
    {
        private readonly TDbContext context;
        private readonly CancellationToken cancellationToken;

        public BlockProgressRepository(TDbContext context) : this(context, default)
        {
        }

        public BlockProgressRepository(TDbContext context, CancellationToken cancellationToken)
        {
            this.context = context;
            this.cancellationToken = cancellationToken;
        }

        public async Task<BigInteger?> GetLastBlockNumberProcessedAsync()
        {
            var existing = await context.BlockProgress
                .OrderByDescending(b => b.RowIndex)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            return existing == null ? (BigInteger?)null : new BigInteger(existing.LastBlockProcessed);
        }

        public async Task UpsertProgressAsync(BigInteger blockNumber)
        {
            var value = (long)blockNumber;
            var existing = await context.BlockProgress
                .OrderByDescending(b => b.RowIndex)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (existing != null)
            {
                existing.LastBlockProcessed = value;
                existing.UpdateRowDates();
            }
            else
            {
                var blockRange = new BlockProgress { LastBlockProcessed = value };
                blockRange.UpdateRowDates();
                context.BlockProgress.Add(blockRange);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
