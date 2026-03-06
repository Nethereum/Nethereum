using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.BlockchainProcessing;
using Nethereum.BlockchainProcessing.Metrics;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class HubLogProcessingWorker : BackgroundService
    {
        private readonly IMessageIndexStore _store;
        private readonly ulong _targetChainId;
        private readonly MessagingConfig _config;
        private readonly IBlockValidator _blockValidator;
        private readonly ILogger<HubLogProcessingWorker> _logger;
        private readonly ILogProcessingObserver _observer;

        public HubLogProcessingWorker(
            IMessageIndexStore store,
            ulong targetChainId,
            MessagingConfig config,
            IBlockValidator blockValidator = null,
            ILogger<HubLogProcessingWorker> logger = null,
            ILogProcessingObserver observer = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _targetChainId = targetChainId;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _blockValidator = blockValidator;
            _logger = logger;
            _observer = observer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.Enabled || _config.SourceChains.Count == 0)
            {
                _logger?.LogInformation("Hub log processing worker disabled or no source chains configured");
                return;
            }

            _logger?.LogInformation("Hub log processing worker starting for {Count} source chain(s)", _config.SourceChains.Count);

            var tasks = new List<Task>();
            foreach (var source in _config.SourceChains)
            {
                tasks.Add(RunProcessorWithRetryAsync(source, stoppingToken));
            }

            await Task.WhenAll(tasks);
            _logger?.LogInformation("Hub log processing worker stopped");
        }

        private async Task RunProcessorWithRetryAsync(SourceChainConfig source, CancellationToken cancellationToken)
        {
            await RetryRunner.RunWithExponentialBackoffAsync(
                async ct =>
                {
                    var processor = new HubMessageLogProcessor(
                        _store,
                        source.ChainId,
                        _targetChainId,
                        _blockValidator,
                        _logger)
                    {
                        RpcUrl = source.RpcUrl,
                        HubContractAddress = source.HubContractAddress,
                        StartAtBlockNumberIfNotProcessed = _config.StartAtBlockNumber,
                        NumberOfBlocksPerRequest = _config.BlocksPerRequest,
                        RetryWeight = _config.RetryWeight,
                        MinimumBlockConfirmations = _config.MinimumBlockConfirmations,
                        ReorgBuffer = _config.ReorgBuffer,
                        Observer = _observer
                    };

                    _logger?.LogInformation("Starting log processor for source chain {ChainId} (rpc={RpcUrl}, hub={Hub})",
                        source.ChainId, source.RpcUrl, source.HubContractAddress);

                    await processor.ExecuteAsync(ct);
                },
                cancellationToken,
                (ex, attempt, delay) =>
                    _logger?.LogError(ex, "Log processor for source chain {ChainId} failed (attempt {Attempt}), retrying in {Delay}s",
                        source.ChainId, attempt, delay));
        }
    }
}
