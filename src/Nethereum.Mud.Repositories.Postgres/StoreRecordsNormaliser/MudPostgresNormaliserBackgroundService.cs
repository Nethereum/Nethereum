using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.BlockchainProcessing;

namespace Nethereum.Mud.Repositories.Postgres.StoreRecordsNormaliser
{
    public sealed class MudPostgresNormaliserBackgroundService : BackgroundService
    {
        private readonly ILogger<MudPostgresNormaliserBackgroundService> _logger;
        private readonly MudPostgresNormaliserProcessingService _processor;
        private readonly IServiceScopeFactory _scopes;
        private readonly MudPostgresProcessingOptions _options;

        public MudPostgresNormaliserBackgroundService(
            ILogger<MudPostgresNormaliserBackgroundService> logger,
            MudPostgresNormaliserProcessingService processor,
            IOptions<MudPostgresProcessingOptions> options)
        {
            _logger = logger;
            _processor = processor;
            _options = options.Value;
        }

        [ActivatorUtilitiesConstructor]
        public MudPostgresNormaliserBackgroundService(
            ILogger<MudPostgresNormaliserBackgroundService> logger,
            IServiceScopeFactory scopes,
            IOptions<MudPostgresProcessingOptions> options)
        {
            _logger = logger;
            _scopes = scopes;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(_options.Address))
            {
                _logger.LogWarning("MUD normaliser: no Address configured, skipping.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_options.RpcUrl))
            {
                _logger.LogWarning("MUD normaliser: no RpcUrl configured, skipping.");
                return;
            }

            _logger.LogInformation("MUD normaliser background service starting for {Address}", _options.Address);

            await RetryRunner.RunWithExponentialBackoffAsync(
                RunAttemptAsync,
                stoppingToken,
                (ex, attempt, delay) =>
                    _logger.LogError(ex, "MUD normaliser failed (attempt {Attempt}), retrying in {Delay}s", attempt, delay));

            _logger.LogInformation("MUD normaliser background service finished");
        }

        private async Task RunAttemptAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopes?.CreateScope();
            var processor = scope == null
                ? _processor
                : scope.ServiceProvider.GetRequiredService<MudPostgresNormaliserProcessingService>();

            processor.Address = _options.Address;
            processor.RpcUrl = _options.RpcUrl;

            await processor.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
