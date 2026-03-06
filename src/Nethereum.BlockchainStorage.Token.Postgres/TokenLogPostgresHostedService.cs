using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.BlockchainProcessing.Metrics;

namespace Nethereum.BlockchainStorage.Token.Postgres
{
    public sealed class TokenLogPostgresHostedService : BackgroundService
    {
        private readonly ILogger<TokenLogPostgresHostedService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TokenLogProcessingOptions _options;
        private readonly ILogProcessingObserver _observer;

        public TokenLogPostgresHostedService(
            ILogger<TokenLogPostgresHostedService> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<TokenLogProcessingOptions> options,
            ILogProcessingObserver observer = null)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _observer = observer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(_options.RpcUrl))
            {
                throw new InvalidOperationException("Missing Token log processing RpcUrl configuration value.");
            }

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<TokenLogPostgresProcessingService>();

            processor.RpcUrl = _options.RpcUrl;
            processor.ContractAddresses = _options.ContractAddresses;
            processor.StartAtBlockNumberIfNotProcessed = _options.StartAtBlockNumberIfNotProcessed;
            processor.NumberOfBlocksToProcessPerRequest = _options.NumberOfBlocksToProcessPerRequest;
            processor.RetryWeight = _options.RetryWeight;
            processor.MinimumNumberOfConfirmations = _options.MinimumNumberOfConfirmations;
            processor.ReorgBuffer = _options.ReorgBuffer;
            processor.Observer = _observer;

            _logger.LogInformation("Token log Postgres processing hosted service starting.");
            await processor.ExecuteAsync(stoppingToken);
            _logger.LogInformation("Token log Postgres processing hosted service finished.");
        }
    }
}
