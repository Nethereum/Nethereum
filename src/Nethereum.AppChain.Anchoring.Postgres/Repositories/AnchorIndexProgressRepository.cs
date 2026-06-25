using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.AppChain.Anchoring.Postgres.Entities;

namespace Nethereum.AppChain.Anchoring.Postgres.Repositories
{
    public class AnchorIndexProgressRepository
    {
        private const string ProgressKey = "anchor-indexer";
        private readonly AnchorIndexDbContext _context;

        public AnchorIndexProgressRepository(AnchorIndexDbContext context)
        {
            _context = context;
        }

        public async Task<long> GetLastProcessedBlockAsync(long defaultValue = 0)
        {
            var record = await _context.IndexProgress
                .FindAsync(ProgressKey)
                .ConfigureAwait(false);

            return record?.LastBlockProcessed ?? defaultValue;
        }

        public async Task UpsertAsync(long blockNumber)
        {
            var record = await _context.IndexProgress
                .FindAsync(ProgressKey)
                .ConfigureAwait(false);

            if (record != null)
            {
                record.LastBlockProcessed = blockNumber;
            }
            else
            {
                _context.IndexProgress.Add(new AnchorIndexProgress
                {
                    Id = ProgressKey,
                    LastBlockProcessed = blockNumber
                });
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task ResetAllAsync()
        {
            await _context.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE anchor_records, anchor_chain_registrations, anchor_block_proofs, anchor_chain_summaries, anchor_denormalizer_progress RESTART IDENTITY")
                .ConfigureAwait(false);

            var progress = await _context.IndexProgress
                .FindAsync(ProgressKey)
                .ConfigureAwait(false);

            if (progress != null)
            {
                progress.LastBlockProcessed = 0;
                await _context.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}
