using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.ProgressRepositories;

namespace Nethereum.Mud.Repositories.EntityFramework
{
    public class ChainStateRepository<TDbContext> : IChainStateRepository where TDbContext : DbContext, IMudStoreRecordsDbSets
    {
        private readonly TDbContext _context;

        public ChainStateRepository(TDbContext context)
        {
            _context = context;
        }

        public async Task<ChainState> GetChainStateAsync()
        {
            return await _context.ChainStates.FirstOrDefaultAsync().ConfigureAwait(false);
        }

        public async Task UpsertChainStateAsync(ChainState chainState)
        {
            chainState.UpdateRowDates();

            if (chainState.IsNew())
            {
                _context.ChainStates.Add(chainState);
            }
            else
            {
                _context.ChainStates.Update(chainState);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
