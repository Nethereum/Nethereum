using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.AppChain.Anchoring.Postgres.Entities;

namespace Nethereum.AppChain.Anchoring.Postgres.Repositories
{
    public class PostgresBlockProofRecordRepository : IBlockProofRecordRepository
    {
        private readonly AnchorIndexDbContext _context;

        public PostgresBlockProofRecordRepository(AnchorIndexDbContext context)
        {
            _context = context;
        }

        public async Task UpsertAsync(BlockProofRecord record)
        {
            var existing = await _context.BlockProofs
                .FirstOrDefaultAsync(p => p.ChainId == record.ChainId && p.BlockNumber == record.BlockNumber)
                .ConfigureAwait(false);

            if (existing != null)
                _context.Entry(existing).CurrentValues.SetValues(record);
            else
                _context.BlockProofs.Add(record);

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<BlockProofRecord>> GetByChainAsync(long chainId, int skip = 0, int take = 50)
        {
            return await _context.BlockProofs
                .Where(p => p.ChainId == chainId)
                .OrderByDescending(p => p.BlockNumber)
                .Skip(skip).Take(take)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<bool> IsBlockProvenAsync(long chainId, long blockNumber)
        {
            return await _context.BlockProofs
                .AnyAsync(p => p.ChainId == chainId && p.BlockNumber == blockNumber)
                .ConfigureAwait(false);
        }
    }
}
