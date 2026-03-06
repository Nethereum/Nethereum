using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.BlockchainProcessing;

namespace Nethereum.BlockchainStorage.Processors
{
    public sealed class InternalTransactionProcessingHostedService : BackgroundService
    {
        private readonly ILogger<InternalTransactionProcessingHostedService> _logger;
        private readonly InternalTransactionProcessingService _processor;

        public InternalTransactionProcessingHostedService(
            ILogger<InternalTransactionProcessingHostedService> logger,
            InternalTransactionProcessingService processor)
        {
            _logger = logger;
            _processor = processor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Internal transaction processing hosted service starting.");

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct => _processor.ExecuteAsync(ct),
                stoppingToken,
                (ex, attempt, delay) =>
                    _logger.LogError(ex, "Internal transaction processing failed (attempt {Attempt}), retrying in {Delay}s", attempt, delay));

            _logger.LogInformation("Internal transaction processing hosted service finished.");
        }
    }
}
