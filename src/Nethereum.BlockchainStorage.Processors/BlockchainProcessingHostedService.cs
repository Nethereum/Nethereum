using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.BlockchainProcessing;

namespace Nethereum.BlockchainStorage.Processors
{
    public sealed class BlockchainProcessingHostedService : BackgroundService
    {
        private readonly ILogger<BlockchainProcessingHostedService> _logger;
        private readonly BlockchainProcessingService _processor;

        public BlockchainProcessingHostedService(
            ILogger<BlockchainProcessingHostedService> logger,
            BlockchainProcessingService processor)
        {
            _logger = logger;
            _processor = processor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Blockchain processing hosted service starting.");

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct => _processor.ExecuteAsync(ct),
                stoppingToken,
                (ex, attempt, delay) =>
                    _logger.LogError(ex, "Blockchain processing failed (attempt {Attempt}), retrying in {Delay}s", attempt, delay));

            _logger.LogInformation("Blockchain processing hosted service finished.");
        }
    }
}
