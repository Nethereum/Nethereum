using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Sync.Metrics;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Metrics;

namespace Nethereum.AppChain.Server.Metrics
{
    public class MetricsCollector : BackgroundService
    {
        private readonly BlockProductionMetrics _blockProduction;
        private readonly TxPoolMetrics? _txPool;
        private readonly SyncMetrics _sync;
        private readonly IAppChain _appChain;
        private readonly ITxPool? _txPoolSource;
        private readonly ILogger<MetricsCollector>? _logger;
        private readonly TimeSpan _collectionInterval;

        public MetricsCollector(
            BlockProductionMetrics blockProduction,
            SyncMetrics sync,
            IAppChain appChain,
            MetricsConfig config,
            TxPoolMetrics? txPool = null,
            ITxPool? txPoolSource = null,
            ILogger<MetricsCollector>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(blockProduction);
            ArgumentNullException.ThrowIfNull(sync);
            ArgumentNullException.ThrowIfNull(appChain);
            ArgumentNullException.ThrowIfNull(config);

            _blockProduction = blockProduction;
            _txPool = txPool;
            _sync = sync;
            _appChain = appChain;
            _txPoolSource = txPoolSource;
            _logger = logger;
            _collectionInterval = TimeSpan.FromMilliseconds(config.CollectionIntervalMs);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger?.LogInformation("Metrics collector started, collecting every {Interval}s",
                _collectionInterval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CollectMetricsAsync();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error collecting metrics");
                }

                try
                {
                    await Task.Delay(_collectionInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger?.LogInformation("Metrics collector stopped");
        }

        private async Task CollectMetricsAsync()
        {
            var blockNumber = await _appChain.GetBlockNumberAsync();
            _blockProduction.SetCurrentBlockNumber((long)blockNumber);

            if (_txPool != null && _txPoolSource != null)
            {
                _txPool.SetPendingCount(_txPoolSource.PendingCount);
            }

            _sync.SetLocalHead((long)blockNumber);
        }
    }
}
