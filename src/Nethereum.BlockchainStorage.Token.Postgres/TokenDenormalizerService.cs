using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.Services.SmartContracts;
using Nethereum.BlockchainStorage.Token.Postgres.Repositories;

namespace Nethereum.BlockchainStorage.Token.Postgres
{
    public class TokenDenormalizerService
    {
        private readonly TokenPostgresDbContext _context;
        private readonly ITokenTransferLogRepository _transferLogRepository;
        private readonly DenormalizerProgressRepository _progressRepository;
        private readonly TokenDenormalizerOptions _options;
        private readonly ILogger<TokenDenormalizerService> _logger;

        private static readonly string[] TransferEventHashes =
            TokenDenormalizerProcessingService.GetTransferEventHashes();

        public TokenDenormalizerService(
            TokenPostgresDbContext context,
            ITokenTransferLogRepository transferLogRepository,
            DenormalizerProgressRepository progressRepository,
            IOptions<TokenDenormalizerOptions> options,
            ILogger<TokenDenormalizerService> logger = null)
        {
            _context = context;
            _transferLogRepository = transferLogRepository;
            _progressRepository = progressRepository;
            _options = options.Value;
            _logger = logger;
        }

        public async Task ProcessFromCheckpointAsync(CancellationToken cancellationToken = default)
        {
            var lastRowIndex = await _progressRepository.GetLastProcessedRowIndexAsync().ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                var rawLogs = await _context.IndexedLogs
                    .Where(l => l.RowIndex > lastRowIndex
                        && l.IsCanonical
                        && TransferEventHashes.Contains(l.EventHash))
                    .OrderBy(l => l.RowIndex)
                    .Take(_options.BatchSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (rawLogs.Count == 0)
                    break;

                var processed = await TokenDenormalizerProcessingService
                    .ProcessBatchAsync(rawLogs, _transferLogRepository)
                    .ConfigureAwait(false);

                lastRowIndex = rawLogs[rawLogs.Count - 1].RowIndex;
                await _progressRepository.UpsertProgressAsync(lastRowIndex).ConfigureAwait(false);

                _context.ChangeTracker.Clear();

                _logger?.LogInformation(
                    "Denormalized {Count} transfer logs up to RowIndex {RowIndex}",
                    processed, lastRowIndex);
            }
        }
    }
}
