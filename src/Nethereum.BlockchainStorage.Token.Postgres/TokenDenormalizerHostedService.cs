using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nethereum.BlockchainStorage.Token.Postgres
{
    public sealed class TokenDenormalizerHostedService : BackgroundService
    {
        private readonly ILogger<TokenDenormalizerHostedService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TokenDenormalizerOptions _options;

        public TokenDenormalizerHostedService(
            ILogger<TokenDenormalizerHostedService> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<TokenDenormalizerOptions> options)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Token denormalizer hosted service starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var denormalizer = scope.ServiceProvider.GetRequiredService<TokenDenormalizerService>();
                    await denormalizer.ProcessFromCheckpointAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during token denormalization.");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(_options.ProcessingIntervalSeconds),
                    stoppingToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Token denormalizer hosted service finished.");
        }
    }
}
