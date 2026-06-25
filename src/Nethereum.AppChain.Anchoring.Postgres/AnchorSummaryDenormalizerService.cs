using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.AppChain.Anchoring.Postgres.Entities;
using Nethereum.AppChain.Anchoring.Postgres.Metrics;
using Nethereum.AppChain.Anchoring.Postgres.Repositories;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    public class AnchorSummaryDenormalizerService
    {
        private readonly AnchorIndexDbContext _context;
        private readonly IChainAnchoringSummaryRepository _summaryRepo;
        private readonly AnchorDenormalizerProgressRepository _progressRepo;
        private readonly AnchorSummaryDenormalizerOptions _options;
        private readonly AnchorIndexingMetrics _metrics;
        private readonly ILogger<AnchorSummaryDenormalizerService> _logger;

        public AnchorSummaryDenormalizerService(
            AnchorIndexDbContext context,
            IChainAnchoringSummaryRepository summaryRepo,
            AnchorDenormalizerProgressRepository progressRepo,
            IOptions<AnchorSummaryDenormalizerOptions> options,
            AnchorIndexingMetrics metrics = null,
            ILogger<AnchorSummaryDenormalizerService> logger = null)
        {
            _context = context;
            _summaryRepo = summaryRepo;
            _progressRepo = progressRepo;
            _options = options.Value;
            _metrics = metrics;
            _logger = logger;
        }

        public async Task ProcessFromCheckpointAsync(CancellationToken ct = default)
        {
            var lastProcessedId = await _progressRepo.GetLastProcessedRowIndexAsync().ConfigureAwait(false);

            while (!ct.IsCancellationRequested)
            {
                var newAnchors = await _context.Anchors
                    .Where(a => a.Id > lastProcessedId)
                    .OrderBy(a => a.Id)
                    .Take(_options.BatchSize)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                if (newAnchors.Count == 0)
                    break;

                var affectedChainIds = newAnchors.Select(a => a.ChainId).Distinct().ToList();

                foreach (var chainId in affectedChainIds)
                {
                    var latest = await _context.Anchors
                        .Where(a => a.ChainId == chainId)
                        .OrderByDescending(a => a.EndBlock)
                        .FirstOrDefaultAsync(ct).ConfigureAwait(false);

                    if (latest == null) continue;

                    var totalAnchors = await _context.Anchors
                        .CountAsync(a => a.ChainId == chainId, ct).ConfigureAwait(false);
                    var totalProofs = await _context.BlockProofs
                        .CountAsync(p => p.ChainId == chainId, ct).ConfigureAwait(false);

                    double avgInterval = 0;
                    if (totalAnchors >= 2)
                    {
                        var timestamps = await _context.Anchors
                            .Where(a => a.ChainId == chainId)
                            .OrderByDescending(a => a.EndBlock)
                            .Take(10)
                            .Select(a => a.Timestamp)
                            .ToListAsync(ct).ConfigureAwait(false);

                        if (timestamps.Count >= 2)
                        {
                            var intervals = timestamps.Zip(timestamps.Skip(1), (a, b) => Math.Abs(a - b));
                            avgInterval = intervals.Average();
                        }
                    }

                    await _summaryRepo.UpsertAsync(new ChainAnchoringSummary
                    {
                        ChainId = chainId,
                        LatestAnchoredBlock = latest.EndBlock,
                        TotalAnchors = totalAnchors,
                        TotalProvenBlocks = totalProofs,
                        CurrentProofSystem = latest.ProofSystem,
                        LastAnchorTimestamp = latest.Timestamp,
                        AverageAnchorIntervalSeconds = avgInterval,
                        LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    }).ConfigureAwait(false);
                }

                lastProcessedId = newAnchors[newAnchors.Count - 1].Id;
                await _progressRepo.UpsertProgressAsync(lastProcessedId).ConfigureAwait(false);

                _context.ChangeTracker.Clear();

                _metrics?.RecordDenormalization(newAnchors.Count);
                _logger?.LogInformation(
                    "Denormalized {Count} anchors for {Chains} chains up to Id {Id}",
                    newAnchors.Count, affectedChainIds.Count, lastProcessedId);
            }
        }
    }
}
