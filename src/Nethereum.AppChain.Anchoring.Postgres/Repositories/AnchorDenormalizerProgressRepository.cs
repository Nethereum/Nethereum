using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.AppChain.Anchoring.Postgres.Entities;

namespace Nethereum.AppChain.Anchoring.Postgres.Repositories
{
    public class AnchorDenormalizerProgressRepository
    {
        private readonly AnchorIndexDbContext _context;

        public AnchorDenormalizerProgressRepository(AnchorIndexDbContext context)
        {
            _context = context;
        }

        public async Task<long> GetLastProcessedRowIndexAsync()
        {
            var record = await _context.DenormalizerProgress
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            return record?.LastProcessedAnchorId ?? 0;
        }

        public async Task UpsertProgressAsync(long lastAnchorId)
        {
            var record = await _context.DenormalizerProgress
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (record != null)
            {
                record.LastProcessedAnchorId = lastAnchorId;
            }
            else
            {
                record = new AnchorDenormalizerProgress { LastProcessedAnchorId = lastAnchorId };
                _context.DenormalizerProgress.Add(record);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
