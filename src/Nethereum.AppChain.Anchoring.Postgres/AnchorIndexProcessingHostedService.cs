using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.AppChain.Anchoring.Postgres.Metrics;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    public sealed class AnchorIndexProcessingHostedService : BackgroundService
    {
        private readonly ILogger<AnchorIndexProcessingHostedService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AnchorIndexProcessingOptions _options;
        private readonly AnchorIndexingMetrics _metrics;

        public AnchorIndexProcessingHostedService(
            ILogger<AnchorIndexProcessingHostedService> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<AnchorIndexProcessingOptions> options,
            AnchorIndexingMetrics metrics = null)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _metrics = metrics;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(_options.RpcUrl))
                throw new InvalidOperationException("Missing AnchorIndexing RpcUrl configuration value.");

            _logger.LogInformation("Anchor index processing hosted service starting.");

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<AnchorIndexProcessingService>();

            processor.RpcUrl = _options.RpcUrl;
            processor.AnchorContractAddress = _options.AnchorContractAddress;
            processor.NumberOfBlocksPerRequest = _options.NumberOfBlocksPerRequest;
            processor.StartAtBlockNumberIfNotProcessed = _options.StartAtBlockNumberIfNotProcessed;
            processor.MinimumBlockConfirmations = _options.MinimumBlockConfirmations;
            processor.ReorgBuffer = _options.ReorgBuffer;
            processor.PollIntervalMs = _options.PollIntervalMs;
            processor.ChainIdFilter = _options.ChainIdFilter;
            processor.Metrics = _metrics;

            await processor.ExecuteAsync(stoppingToken).ConfigureAwait(false);

            _logger.LogInformation("Anchor index processing hosted service finished.");
        }
    }
}
