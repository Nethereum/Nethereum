using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainStore.EFCore.Repositories
{
    public class BlockProgressRepository : RepositoryBase, IBlockProgressRepository
    {
        public BlockProgressRepository(IBlockchainDbContextFactory contextFactory) : base(contextFactory)
        {
        }

        public async Task<BigInteger?> GetLastBlockNumberProcessedAsync()
        {
            using (var context = _contextFactory.CreateContext())
            {
                var existing = await context.BlockProgress
                    .OrderByDescending(b => b.RowIndex)
                    .FirstOrDefaultAsync().ConfigureAwait(false);
                return existing == null ? (BigInteger?)null : new BigInteger(existing.LastBlockProcessed);
            }
        }

        public async Task UpsertProgressAsync(BigInteger blockNumber)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var value = (long)blockNumber;
                var existing = await context.BlockProgress
                    .OrderByDescending(b => b.RowIndex)
                    .FirstOrDefaultAsync().ConfigureAwait(false);

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

                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}
