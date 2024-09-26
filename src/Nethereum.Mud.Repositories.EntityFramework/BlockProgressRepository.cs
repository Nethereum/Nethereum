using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Mud.Repositories.EntityFramework    
{
    public class BlockProgressRepository<TDbContext> : IBlockProgressRepository where TDbContext : DbContext, IMudStoreRecordsDbSets
    {
        private readonly TDbContext context;

        public BlockProgressRepository(TDbContext context)
        {
            this.context = context;
        }

        public async Task<BigInteger?> GetLastBlockNumberProcessedAsync()
        {
           
             var max = await context.BlockProgress.MaxAsync(b => b.LastBlockProcessed).ConfigureAwait(false);
             return string.IsNullOrEmpty(max) ? (BigInteger?)null : BigInteger.Parse(max);
            
        }

        public async Task UpsertProgressAsync(BigInteger blockNumber)
        {
            try
            {
              
                var blockRange = blockNumber.MapToStorageEntityForUpsert<BlockProgress>();
                blockRange.LastBlockProcessed = blockNumber.ToString().PadLeft(ColumnLengths.BigIntegerLength, '0');
                context.BlockProgress.Add(blockRange);
                await context.SaveChangesAsync().ConfigureAwait(false);
                
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("String or binary data would be truncated") ?? false)
            {
                throw new DbUpdateException(
                    $"{nameof(BlockProgressRepository<TDbContext>)} Data Truncation Error. Ensure that the LastBlockProcessed column length is {ColumnLengths.BigIntegerLength}."
                    , ex);
            }
        }
    }




}
