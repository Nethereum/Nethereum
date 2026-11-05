using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;

namespace Nethereum.MainnetChain.Observability
{
    public sealed class RocksDbCacheStatsReporter : BackgroundService
    {
        public const int BlocksPerReport = 100;
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

        private readonly IChainStoreBundle _bundle;
        private readonly ILogger<RocksDbCacheStatsReporter> _logger;

        public RocksDbCacheStatsReporter(
            IChainStoreBundle bundle,
            ILogger<RocksDbCacheStatsReporter>? logger = null)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _logger = logger ?? NullLogger<RocksDbCacheStatsReporter>.Instance;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!(_bundle is RocksDbChainStoreBundle rocksBundle) || !rocksBundle.Rocks.StatisticsEnabled)
                return;

            var rocks = rocksBundle.Rocks;
            BigInteger lastReported = await _bundle.Blocks.GetHeightAsync().ConfigureAwait(false);
            using var timer = new PeriodicTimer(PollInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    var height = await _bundle.Blocks.GetHeightAsync().ConfigureAwait(false);
                    if (height - lastReported < BlocksPerReport) continue;
                    lastReported = height;
                    var s = rocks.ReadCacheStats();
                    _logger.LogInformation(
                        "rocksdb.cache.stats block={Block} index_hit={IndexHit} index_miss={IndexMiss} filter_hit={FilterHit} filter_miss={FilterMiss} data_hit={DataHit} data_miss={DataMiss} usage_mb={UsageMb}",
                        height, s.IndexHit, s.IndexMiss, s.FilterHit, s.FilterMiss, s.DataHit, s.DataMiss,
                        s.UsageBytes / (1024 * 1024));
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }
}
