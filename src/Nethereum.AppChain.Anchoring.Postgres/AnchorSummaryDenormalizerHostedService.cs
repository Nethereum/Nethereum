using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    public sealed class AnchorSummaryDenormalizerHostedService : BackgroundService
    {
        private readonly ILogger<AnchorSummaryDenormalizerHostedService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AnchorSummaryDenormalizerOptions _options;

        public AnchorSummaryDenormalizerHostedService(
            ILogger<AnchorSummaryDenormalizerHostedService> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<AnchorSummaryDenormalizerOptions> options)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Anchor summary denormalizer hosted service starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var denormalizer = scope.ServiceProvider
                        .GetRequiredService<AnchorSummaryDenormalizerService>();
                    await denormalizer.ProcessFromCheckpointAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during anchor summary denormalization.");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(_options.ProcessingIntervalSeconds),
                    stoppingToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Anchor summary denormalizer hosted service finished.");
        }
    }
}
