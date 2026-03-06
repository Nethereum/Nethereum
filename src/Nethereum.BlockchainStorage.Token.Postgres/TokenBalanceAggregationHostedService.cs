using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nethereum.BlockchainStorage.Token.Postgres
{
    public sealed class TokenBalanceAggregationHostedService : BackgroundService
    {
        private readonly ILogger<TokenBalanceAggregationHostedService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TokenBalanceAggregationOptions _options;

        public TokenBalanceAggregationHostedService(
            ILogger<TokenBalanceAggregationHostedService> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<TokenBalanceAggregationOptions> options)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(_options.RpcUrl))
            {
                _logger.LogWarning(
                    "TokenBalanceAggregation:RpcUrl is not configured. " +
                    "Token balance aggregation will be skipped until an RPC URL is provided.");
            }

            _logger.LogInformation("Token balance aggregation hosted service starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var aggregator = scope.ServiceProvider.GetRequiredService<TokenBalanceRpcAggregationService>();
                    await aggregator.ProcessFromCheckpointAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during token balance aggregation.");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(_options.ProcessingIntervalSeconds),
                    stoppingToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Token balance aggregation hosted service finished.");
        }
    }
}
