using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.BlockchainProcessing;
using Nethereum.Mud.Repositories.Postgres.StoreRecordsNormaliser;

namespace Nethereum.Mud.Repositories.Postgres
{
    public sealed class MudWorldAddressDiscoveryService : BackgroundService
    {
        private readonly ILogger<MudWorldAddressDiscoveryService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public MudWorldAddressDiscoveryService(
            ILogger<MudWorldAddressDiscoveryService> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            string connectionString)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _connectionString = connectionString;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var loadGeneratorUrl = _configuration["services:loadgenerator:http:0"]
                ?? _configuration["services:loadgenerator:https:0"]
                ?? _configuration["MudProcessing:LoadGeneratorUrl"];

            var candidateUrls = new List<string>();
            if (!string.IsNullOrWhiteSpace(loadGeneratorUrl))
                candidateUrls.Add($"{loadGeneratorUrl.TrimEnd('/')}/mud/world-address");

            var devchainUrl = _configuration["services:devchain:http:0"];
            if (!string.IsNullOrWhiteSpace(devchainUrl) && Uri.TryCreate(devchainUrl, UriKind.Absolute, out var devUri))
            {
                for (int port = devUri.Port - 10; port <= devUri.Port + 10; port++)
                {
                    if (port != devUri.Port && port > 0)
                        candidateUrls.Add($"http://{devUri.Host}:{port}/mud/world-address");
                }
            }

            if (candidateUrls.Count == 0)
            {
                _logger.LogWarning("MUD discovery: no LoadGenerator URL found, skipping MUD address polling");
                return;
            }

            _logger.LogInformation("MUD discovery: polling {Count} candidate endpoints for World address...", candidateUrls.Count);

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2);
            string? worldAddress = null;

            while (!stoppingToken.IsCancellationRequested && worldAddress == null)
            {
                foreach (var endpoint in candidateUrls)
                {
                    try
                    {
                        var response = await httpClient.GetAsync(endpoint, stoppingToken);
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync(stoppingToken);
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("address", out var addressElement))
                            {
                                worldAddress = addressElement.GetString();
                                if (!string.IsNullOrWhiteSpace(worldAddress))
                                {
                                    _logger.LogInformation("MUD discovery: found World address at {Endpoint}", endpoint);
                                    break;
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception)
                    {
                    }
                }

                if (worldAddress == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                }
            }

            if (string.IsNullOrWhiteSpace(worldAddress))
                return;

            _logger.LogInformation("MUD discovery: World address discovered — {Address}", worldAddress);

            _configuration["MudProcessing:Address"] = worldAddress;

            var rpcUrl = _configuration["MudProcessing:RpcUrl"]
                ?? _configuration["BlockchainProcessing:BlockchainUrl"]
                ?? "http://localhost:8545";

            var options = MudPostgresProcessingOptions.Load(_configuration.GetSection("MudProcessing"));
            options.Address = worldAddress;
            options.RpcUrl = rpcUrl;

            _logger.LogInformation("MUD discovery: starting MUD Postgres processing and normaliser for {Address}", worldAddress);

            await RetryRunner.RunWithExponentialBackoffAsync(
                async ct =>
                {
                    using var indexerScope = _serviceProvider.CreateScope();
                    var processor = indexerScope.ServiceProvider.GetRequiredService<MudPostgresStoreRecordsProcessingService>();
                    processor.Address = options.Address;
                    processor.RpcUrl = options.RpcUrl;
                    processor.StartAtBlockNumberIfNotProcessed = options.StartAtBlockNumberIfNotProcessed;
                    processor.NumberOfBlocksToProcessPerRequest = options.NumberOfBlocksToProcessPerRequest;
                    processor.RetryWeight = options.RetryWeight;
                    processor.MinimumNumberOfConfirmations = options.MinimumNumberOfConfirmations;
                    processor.ReorgBuffer = options.ReorgBuffer;

                    using var normaliserScope = _serviceProvider.CreateScope();
                    var normaliser = normaliserScope.ServiceProvider.GetRequiredService<MudPostgresNormaliserProcessingService>();
                    normaliser.Address = worldAddress;
                    normaliser.RpcUrl = rpcUrl;

                    await Task.WhenAll(
                        processor.ExecuteAsync(ct),
                        normaliser.ExecuteAsync(ct));
                },
                stoppingToken,
                (ex, attempt, delay) =>
                    _logger.LogError(ex, "MUD processing failed (attempt {Attempt}), retrying in {Delay}s", attempt, delay));

            _logger.LogInformation("MUD discovery: MUD Postgres processing finished");
        }
    }
}
