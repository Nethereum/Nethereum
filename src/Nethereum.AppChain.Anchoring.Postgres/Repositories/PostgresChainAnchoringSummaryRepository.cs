using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.AppChain.Anchoring.Postgres.Entities;

namespace Nethereum.AppChain.Anchoring.Postgres.Repositories
{
    public class PostgresChainAnchoringSummaryRepository : IChainAnchoringSummaryRepository
    {
        private readonly AnchorIndexDbContext _context;

        public PostgresChainAnchoringSummaryRepository(AnchorIndexDbContext context)
        {
            _context = context;
        }

        public async Task UpsertAsync(ChainAnchoringSummary summary)
        {
            var existing = await _context.ChainSummaries
                .FindAsync(summary.ChainId)
                .ConfigureAwait(false);

            if (existing != null)
                _context.Entry(existing).CurrentValues.SetValues(summary);
            else
                _context.ChainSummaries.Add(summary);

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<ChainAnchoringSummary> GetAsync(long chainId)
        {
            return await _context.ChainSummaries
                .FindAsync(chainId)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<ChainAnchoringSummary>> GetAllAsync()
        {
            return await _context.ChainSummaries
                .OrderBy(s => s.ChainId)
                .ToListAsync()
                .ConfigureAwait(false);
        }
    }
}
