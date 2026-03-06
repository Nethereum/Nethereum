using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainStorage.Token.Postgres.Repositories
{
    public class DenormalizerProgress : TableRow
    {
        public long LastProcessedRowIndex { get; set; }
    }

    public class DenormalizerProgressRepository
    {
        private readonly TokenPostgresDbContext _context;

        public DenormalizerProgressRepository(TokenPostgresDbContext context)
        {
            _context = context;
        }

        public async Task<long> GetLastProcessedRowIndexAsync()
        {
            var record = await _context.DenormalizerProgress
                .OrderByDescending(d => d.RowIndex)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            return record?.LastProcessedRowIndex ?? 0;
        }

        public async Task UpsertProgressAsync(long lastRowIndex)
        {
            var record = await _context.DenormalizerProgress
                .OrderByDescending(d => d.RowIndex)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (record != null)
            {
                record.LastProcessedRowIndex = lastRowIndex;
                record.UpdateRowDates();
            }
            else
            {
                record = new DenormalizerProgress
                {
                    LastProcessedRowIndex = lastRowIndex
                };
                record.UpdateRowDates();
                _context.DenormalizerProgress.Add(record);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
