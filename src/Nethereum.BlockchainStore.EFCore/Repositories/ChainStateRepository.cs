using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.ProgressRepositories;

namespace Nethereum.BlockchainStore.EFCore.Repositories
{
    public class ChainStateRepository : RepositoryBase, IChainStateRepository
    {
        public ChainStateRepository(IBlockchainDbContextFactory contextFactory) : base(contextFactory)
        {
        }

        public async Task<ChainState> GetChainStateAsync()
        {
            using (var context = _contextFactory.CreateContext())
            {
                return await context.ChainStates.FirstOrDefaultAsync().ConfigureAwait(false);
            }
        }

        public async Task UpsertChainStateAsync(ChainState chainState)
        {
            using (var context = _contextFactory.CreateContext())
            {
                chainState.UpdateRowDates();

                if (chainState.IsNew())
                {
                    context.ChainStates.Add(chainState);
                }
                else
                {
                    context.ChainStates.Update(chainState);
                }

                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}
