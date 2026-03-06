using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.ProgressRepositories;

namespace Nethereum.Mud.Repositories.EntityFramework
{
    public class MudStoreRecordsRepositoryFactory<TDbContext> : IBlockProgressRepositoryFactory, IChainStateRepositoryFactory
        where TDbContext : DbContext, IMudStoreRecordsDbSets
    {
        private readonly TDbContext _context;

        public MudStoreRecordsRepositoryFactory(TDbContext context)
        {
            _context = context;
        }

        public IBlockProgressRepository CreateBlockProgressRepository()
        {
            return new BlockProgressRepository<TDbContext>(_context);
        }

        public IChainStateRepository CreateChainStateRepository()
        {
            return new ChainStateRepository<TDbContext>(_context);
        }
    }
}
