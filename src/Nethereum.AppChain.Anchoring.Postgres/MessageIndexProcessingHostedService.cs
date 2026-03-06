using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.AppChain.Anchoring.Messaging;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    public sealed class MessageIndexProcessingHostedService : BackgroundService
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly PostgresMessageIndexStore _store;
        private readonly MessageIndexProcessingOptions _options;

        public MessageIndexProcessingHostedService(
            ILoggerFactory loggerFactory,
            PostgresMessageIndexStore store,
            IOptions<MessageIndexProcessingOptions> options)
        {
            _loggerFactory = loggerFactory;
            _store = store;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_options.SourceChains.Count == 0)
            {
                throw new InvalidOperationException("No source chains configured for message index processing.");
            }

            var logger = _loggerFactory.CreateLogger<MessageIndexProcessingHostedService>();
            logger.LogInformation("Message index processing hosted service starting for {Count} source chain(s)", _options.SourceChains.Count);

            var tasks = new List<Task>();
            foreach (var source in _options.SourceChains)
            {
                tasks.Add(RunProcessorWithRetryAsync(source, logger, stoppingToken));
            }

            await Task.WhenAll(tasks);
            logger.LogInformation("Message index processing hosted service finished");
        }

        private async Task RunProcessorWithRetryAsync(
            SourceChainOption source,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var retryDelay = TimeSpan.FromSeconds(5);
            var maxRetryDelay = TimeSpan.FromMinutes(5);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var processor = new HubMessageLogProcessor(
                        _store,
                        source.ChainId,
                        _options.TargetChainId,
                        blockValidator: null,
                        logger)
                    {
                        RpcUrl = source.RpcUrl,
                        HubContractAddress = source.HubContractAddress,
                        StartAtBlockNumberIfNotProcessed = _options.StartAtBlockNumber,
                        NumberOfBlocksPerRequest = _options.BlocksPerRequest,
                        RetryWeight = _options.RetryWeight,
                        MinimumBlockConfirmations = _options.MinimumBlockConfirmations,
                        ReorgBuffer = _options.ReorgBuffer
                    };

                    logger.LogInformation("Starting log processor for source chain {ChainId}", source.ChainId);
                    await processor.ExecuteAsync(cancellationToken);
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Log processor for source chain {ChainId} failed, retrying in {Delay}s",
                        source.ChainId, retryDelay.TotalSeconds);

                    await Task.Delay(retryDelay, cancellationToken);
                    retryDelay = TimeSpan.FromTicks(Math.Min(retryDelay.Ticks * 2, maxRetryDelay.Ticks));
                }
            }
        }
    }
}
