using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceScopeFactory _scopes;
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

        [ActivatorUtilitiesConstructor]
        public MudPostgresProcessingHostedService(
            ILogger<MudPostgresProcessingHostedService> logger,
            IServiceScopeFactory scopes,
            IOptions<MudPostgresProcessingOptions> options,
            ILogProcessingObserver observer = null)
        {
            _logger = logger;
            _scopes = scopes;
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

            _logger.LogInformation("Mud Postgres processing hosted service starting.");

            await RetryRunner.RunWithExponentialBackoffAsync(
                RunAttemptAsync,
                stoppingToken,
                (ex, attempt, delay) =>
                    _logger.LogError(ex, "MUD processing failed (attempt {Attempt}), retrying in {Delay}s", attempt, delay));

            _logger.LogInformation("Mud Postgres processing hosted service finished.");
        }

        private async Task RunAttemptAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopes?.CreateScope();
            var processor = scope == null
                ? _processor
                : scope.ServiceProvider.GetRequiredService<MudPostgresStoreRecordsProcessingService>();

            processor.Address = _options.Address;
            processor.RpcUrl = _options.RpcUrl;
            processor.StartAtBlockNumberIfNotProcessed = _options.StartAtBlockNumberIfNotProcessed;
            processor.NumberOfBlocksToProcessPerRequest = _options.NumberOfBlocksToProcessPerRequest;
            processor.RetryWeight = _options.RetryWeight;
            processor.MinimumNumberOfConfirmations = _options.MinimumNumberOfConfirmations;
            processor.ReorgBuffer = _options.ReorgBuffer;
            processor.Observer = _observer;

            await processor.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
