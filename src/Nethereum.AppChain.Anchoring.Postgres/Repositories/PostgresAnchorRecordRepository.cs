using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.AppChain.Anchoring.Postgres.Entities;

namespace Nethereum.AppChain.Anchoring.Postgres.Repositories
{
    public class PostgresAnchorRecordRepository : IAnchorRecordRepository
    {
        private readonly AnchorIndexDbContext _context;

        public PostgresAnchorRecordRepository(AnchorIndexDbContext context)
        {
            _context = context;
        }

        public async Task UpsertAsync(AnchorRecord record)
        {
            var existing = await _context.Anchors
                .FirstOrDefaultAsync(a => a.ChainId == record.ChainId && a.EndBlock == record.EndBlock)
                .ConfigureAwait(false);

            if (existing != null)
                _context.Entry(existing).CurrentValues.SetValues(record);
            else
                _context.Anchors.Add(record);

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<AnchorRecord> GetLatestAsync(long chainId)
        {
            return await _context.Anchors
                .Where(a => a.ChainId == chainId)
                .OrderByDescending(a => a.EndBlock)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public async Task<AnchorRecord> GetByEndBlockAsync(long chainId, long endBlock)
        {
            return await _context.Anchors
                .FirstOrDefaultAsync(a => a.ChainId == chainId && a.EndBlock == endBlock)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<AnchorRecord>> GetByChainAsync(long chainId, int skip = 0, int take = 50)
        {
            return await _context.Anchors
                .Where(a => a.ChainId == chainId)
                .OrderByDescending(a => a.EndBlock)
                .Skip(skip).Take(take)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<int> GetCountByChainAsync(long chainId)
        {
            return await _context.Anchors
                .CountAsync(a => a.ChainId == chainId)
                .ConfigureAwait(false);
        }
    }
}
