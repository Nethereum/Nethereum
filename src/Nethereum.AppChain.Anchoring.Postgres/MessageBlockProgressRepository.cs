using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.ProgressRepositories;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    public class MessageBlockProgressRepository : IBlockProgressRepository
    {
        private readonly MessageIndexDbContext _context;
        private readonly long _sourceChainId;

        public MessageBlockProgressRepository(MessageIndexDbContext context, long sourceChainId)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _sourceChainId = sourceChainId;
        }

        public async Task<BigInteger?> GetLastBlockNumberProcessedAsync()
        {
            var progress = await _context.MessageBlockProgress
                .FirstOrDefaultAsync(p => p.SourceChainId == _sourceChainId);

            if (progress?.LastBlockProcessed is null || string.IsNullOrWhiteSpace(progress.LastBlockProcessed))
                return null;

            return BigInteger.Parse(progress.LastBlockProcessed);
        }

        public async Task UpsertProgressAsync(BigInteger blockNumber)
        {
            var paddedBlock = blockNumber.ToString().PadLeft(ColumnLengths.BigIntegerLength, '0');

            var existing = await _context.MessageBlockProgress
                .FirstOrDefaultAsync(p => p.SourceChainId == _sourceChainId);

            if (existing == null)
            {
                _context.MessageBlockProgress.Add(new MessageBlockProgress
                {
                    SourceChainId = _sourceChainId,
                    LastBlockProcessed = paddedBlock,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.LastBlockProcessed = paddedBlock;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
    }
}
