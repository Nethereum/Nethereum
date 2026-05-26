using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.AppChain.Anchoring.Postgres.Entities;

namespace Nethereum.AppChain.Anchoring.Postgres.Repositories
{
    public class PostgresChainRegistrationRepository : IChainRegistrationRepository
    {
        private readonly AnchorIndexDbContext _context;

        public PostgresChainRegistrationRepository(AnchorIndexDbContext context)
        {
            _context = context;
        }

        public async Task UpsertAsync(ChainRegistration registration)
        {
            var existing = await _context.ChainRegistrations
                .FirstOrDefaultAsync(c => c.ChainId == registration.ChainId)
                .ConfigureAwait(false);

            if (existing != null)
                _context.Entry(existing).CurrentValues.SetValues(registration);
            else
                _context.ChainRegistrations.Add(registration);

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<ChainRegistration> GetByChainIdAsync(long chainId)
        {
            return await _context.ChainRegistrations
                .FirstOrDefaultAsync(c => c.ChainId == chainId)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<ChainRegistration>> GetAllAsync()
        {
            return await _context.ChainRegistrations
                .OrderBy(c => c.ChainId)
                .ToListAsync()
                .ConfigureAwait(false);
        }
    }
}
