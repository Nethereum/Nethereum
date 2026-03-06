using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainStorage.Token.Postgres.Repositories
{
    public class TokenPostgresTransactionLogRepository : ITransactionLogRepository, INonCanonicalTransactionLogRepository
    {
        private readonly TokenPostgresDbContext _context;

        public TokenPostgresTransactionLogRepository(TokenPostgresDbContext context)
        {
            _context = context;
        }

        public async Task UpsertAsync(FilterLogVO log)
        {
            var txHash = log.Log.TransactionHash;
            var logIndex = log.Log.LogIndex.Value;

            var idx = (long)logIndex;
            var existing = await _context.IndexedLogs
                .FirstOrDefaultAsync(l => l.TransactionHash == txHash && l.LogIndex == idx)
                .ConfigureAwait(false);

            if (existing != null)
            {
                existing.MapToStorageEntityForUpsert(log);
            }
            else
            {
                var entity = log.MapToStorageEntityForUpsert();
                _context.IndexedLogs.Add(entity);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<ITransactionLogView> FindByTransactionHashAndLogIndexAsync(string hash, BigInteger logIndex)
        {
            var idx = (long)logIndex;
            return await _context.IndexedLogs
                .FirstOrDefaultAsync(l => l.TransactionHash == hash && l.LogIndex == idx)
                .ConfigureAwait(false);
        }

        public async Task MarkNonCanonicalAsync(BigInteger blockNumber)
        {
            var blockNum = (long)blockNumber;
            var logs = await _context.IndexedLogs
                .Where(l => l.BlockNumber == blockNum)
                .ToListAsync()
                .ConfigureAwait(false);

            foreach (var log in logs)
            {
                log.IsCanonical = false;
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
