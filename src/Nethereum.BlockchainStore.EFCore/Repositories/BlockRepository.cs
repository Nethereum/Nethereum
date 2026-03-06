using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.Hex.HexTypes;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainStore.EFCore.Repositories
{
    public class BlockRepository : RepositoryBase, IBlockRepository, INonCanonicalBlockRepository
    {
        public BlockRepository(IBlockchainDbContextFactory contextFactory) : base(contextFactory)
        {
        }

        public async Task<IBlockView> FindByBlockNumberAsync(HexBigInteger blockNumber)
        {
            using (var context = _contextFactory.CreateContext())
            {
                return await context.Blocks.FindByBlockNumberAsync(blockNumber).ConfigureAwait(false);
            }
        }

        public async Task<BigInteger?> GetMaxBlockNumberAsync()
        {
            using (var context = _contextFactory.CreateContext())
            {
                var max = await context.Blocks.MaxAsync(b => (long?)b.BlockNumber).ConfigureAwait(false);
                return max.HasValue ? new BigInteger(max.Value) : (BigInteger?)null;
            }
        }

        public async Task UpsertBlockAsync(Nethereum.RPC.Eth.DTOs.Block source)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var block = await context.Blocks.FindByBlockNumberAsync(source.Number).ConfigureAwait(false) ?? new Block();

                block.MapToStorageEntityForUpsert(source);
                block.IsCanonical = true;

                if (block.IsNew())
                    context.Blocks.Add(block);
                else
                    context.Blocks.Update(block);

                await context.SaveChangesAsync().ConfigureAwait(false) ;
            }
        }

        public async Task MarkNonCanonicalAsync(BigInteger blockNumber)
        {
            using (var context = _contextFactory.CreateContext())
            {
                var block = await context.Blocks.FindByBlockNumberAsync(blockNumber.ToHexBigInteger()).ConfigureAwait(false);
                if (block == null)
                {
                    return;
                }

                block.IsCanonical = false;
                context.Blocks.Update(block);
                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}
