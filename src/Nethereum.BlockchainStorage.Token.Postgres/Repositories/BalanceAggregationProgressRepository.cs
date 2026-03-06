using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainStorage.Token.Postgres.Repositories
{
    public class BalanceAggregationProgress : TableRow
    {
        public long LastProcessedRowIndex { get; set; }
    }

    public class BalanceAggregationProgressRepository
    {
        private readonly TokenPostgresDbContext _context;

        public BalanceAggregationProgressRepository(TokenPostgresDbContext context)
        {
            _context = context;
        }

        public async Task<long> GetLastProcessedRowIndexAsync()
        {
            var record = await _context.BalanceAggregationProgress
                .OrderByDescending(d => d.RowIndex)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            return record?.LastProcessedRowIndex ?? 0;
        }

        public async Task UpsertProgressAsync(long lastRowIndex)
        {
            var record = await _context.BalanceAggregationProgress
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
                record = new BalanceAggregationProgress
                {
                    LastProcessedRowIndex = lastRowIndex
                };
                record.UpdateRowDates();
                _context.BalanceAggregationProgress.Add(record);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
