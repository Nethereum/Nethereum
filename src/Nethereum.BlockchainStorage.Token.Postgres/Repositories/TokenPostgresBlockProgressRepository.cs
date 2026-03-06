using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.ProgressRepositories;

namespace Nethereum.BlockchainStorage.Token.Postgres.Repositories
{
    public class TokenPostgresBlockProgressRepository : IBlockProgressRepository
    {
        private readonly TokenPostgresDbContext _context;

        public TokenPostgresBlockProgressRepository(TokenPostgresDbContext context)
        {
            _context = context;
        }

        public async Task<BigInteger?> GetLastBlockNumberProcessedAsync()
        {
            var existing = await _context.BlockProgress
                .OrderByDescending(b => b.RowIndex)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            return existing == null ? (BigInteger?)null : new BigInteger(existing.LastBlockProcessed);
        }

        public async Task UpsertProgressAsync(BigInteger blockNumber)
        {
            var value = (long)blockNumber;
            var existing = await _context.BlockProgress
                .OrderByDescending(b => b.RowIndex)
                .FirstOrDefaultAsync().ConfigureAwait(false);

            if (existing != null)
            {
                existing.LastBlockProcessed = value;
                existing.UpdateRowDates();
            }
            else
            {
                var blockRange = new BlockProgress
                {
                    LastBlockProcessed = value
                };
                blockRange.UpdateRowDates();
                _context.BlockProgress.Add(blockRange);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
