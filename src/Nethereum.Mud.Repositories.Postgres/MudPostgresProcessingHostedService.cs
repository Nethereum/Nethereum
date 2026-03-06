using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.BlockchainProcessing;
using Nethereum.BlockchainProcessing.Metrics;

namespace Nethereum.Mud.Repositories.Postgres
{
    public sealed class MudPostgresProcessingHostedService : BackgroundService
    {
        private readonly ILogger<MudPostgresProcessingHostedService> _logger;
        private readonly MudPostgresStoreRecordsProcessingService _processor;
        private readonly MudPostgresProcessingOptions _options;
        private readonly ILogProcessingObserver _observer;

        public MudPostgresProcessingHostedService(
            ILogger<MudPostgresProcessingHostedService> logger,
            MudPostgresStoreRecordsProcessingService processor,
            IOptions<MudPostgresProcessingOptions> options,
            ILogProcessingObserver observer = null)
        {
            _logger = logger;
            _processor = processor;
            _options = options.Value;
            _observer = observer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(_options.Address))
            {
                throw new InvalidOperationException("Missing Mud processing Address configuration value.");
            }

            if (string.IsNullOrWhiteSpace(_options.RpcUrl))
            {
                throw new InvalidOperationException("Missing Mud processing RpcUrl configuration value.");
            }

            _processor.Address = _options.Address;
            _processor.RpcUrl = _options.RpcUrl;
            _processor.StartAtBlockNumberIfNotProcessed = _options.StartAtBlockNumberIfNotProcessed;
            _processor.NumberOfBlocksToProcessPerRequest = _options.NumberOfBlocksToProcessPerRequest;
            _processor.RetryWeight = _options.RetryWeight;
            _processor.MinimumNumberOfConfirmations = _options.MinimumNumberOfConfirmations;
            _processor.ReorgBuffer = _options.ReorgBuffer;
            _processor.Observer = _observer;

            _logger.LogInformation("Mud Postgres processing hosted service starting.");

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct => _processor.ExecuteAsync(ct),
                stoppingToken,
                (ex, attempt, delay) =>
                    _logger.LogError(ex, "MUD processing failed (attempt {Attempt}), retrying in {Delay}s", attempt, delay));

            _logger.LogInformation("Mud Postgres processing hosted service finished.");
        }
    }
}
