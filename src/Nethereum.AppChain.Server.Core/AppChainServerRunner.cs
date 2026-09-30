using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain;
using Nethereum.AppChain.Genesis;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sequencer.ProducerAuthority;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.Rpc;
using Nethereum.Signer;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.AppChain.Anchoring;
using Nethereum.AppChain.Server.Anchoring;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.AppChain.Server.Endpoints;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.AppChain.Anchoring.Metrics;
using Nethereum.AppChain.Sequencer.Metrics;
using Nethereum.AppChain.Server.Metrics;
using Nethereum.CoreChain.Metrics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.AppChain.Anchoring.Messaging;
using Nethereum.AppChain.Anchoring.Rpc;
using Nethereum.Consensus.Clique;
using Nethereum.CoreChain.Rpc.Subscriptions;
using Nethereum.Model;
using Nethereum.DevP2P.Sync;
using SequencerHosting = Nethereum.AppChain.Sequencer.Hosting;
using RocksDbHosting = Nethereum.CoreChain.RocksDB.Hosting;

using Nethereum.CoreChain.Sync;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.AppChain.Server
{
    public static class AppChainServerRunner
    {
        public static async Task RunAsync(AppChainServerConfig config)
        {
            using var loggerFactory = BuildLoggerFactory();

            var logger = loggerFactory.CreateLogger("Nethereum.AppChain.Server");

            PrintBanner(config, logger);

            if (ChainNodeMaintenanceRunner.AnyRequested(config.Node.Maintenance))
            {
                Environment.Exit(await RunMaintenanceAsync(config, logger));
                return;
            }

            logger.LogInformation("Initializing storage...");

            var composed = await AppChainComposition.ComposeAsync(config, loggerFactory, CancellationToken.None);

            if (composed.MudResult != null)
            {
                PrintMudDeployment(composed.MudResult, logger);
            }

            var builder = BuildWebApplicationBuilder(config, composed);
            RegisterMessaging(builder.Services, config, composed, loggerFactory, logger);

            var app = builder.Build();

            var rpc = WireRpc(app, config, composed, logger);
            MapEndpoints(app, config, composed, rpc);

            await RunUntilShutdownAsync(app, config, composed, logger);

            if (composed.ChainNode.Listener != null)
                await composed.ChainNode.Listener.DisposeAsync();
        }

        private static async Task<int> RunMaintenanceAsync(AppChainServerConfig config, ILogger logger)
        {
            await using var storage = ChainNodeStorage.Open(config.Node, logger);
            return await ChainNodeMaintenanceRunner.RunAndReportExitCodeAsync(
                storage.Bundle, config.Node.Maintenance, logger, CancellationToken.None);
        }

        private static ILoggerFactory BuildLoggerFactory()
        {
            return LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(options =>
                {
                    options.TimestampFormat = "[HH:mm:ss.fff] ";
                    options.SingleLine = true;
                    options.IncludeScopes = false;
                });
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddFilter("Nethereum.AppChain.Sequencer", LogLevel.Information);
                builder.AddFilter("Nethereum.Consensus.Clique", LogLevel.Information);
            });
        }

        private static WebApplicationBuilder BuildWebApplicationBuilder(AppChainServerConfig config, AppChainComposedNode composed)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.Logging.AddFilter("Nethereum", LogLevel.Information);
            builder.Services.AddAppChainServer(config);

            builder.Services.AddSingleton(composed.Metrics.BlockProduction);
            builder.Services.AddSingleton(composed.Metrics.TxPool);
            builder.Services.AddSingleton(composed.Metrics.Rpc);
            builder.Services.AddSingleton(composed.Metrics.Storage);
            builder.Services.AddSingleton(composed.Metrics.Sync);
            builder.Services.AddSingleton(composed.Metrics.Sequencer);
            builder.Services.AddSingleton(composed.Metrics.HA);
            builder.Services.AddSingleton(composed.Metrics.Anchoring);
            builder.Services.AddSingleton(new MetricsConfig());

            builder.Services.AddAppChainOpenTelemetry(config);
            builder.Services.AddAppChainHealthChecks();
            builder.AddPrometheusMetrics(config.Node.Rpc.MetricsPort);

            builder.Services.AddSingleton(composed.Node);
            builder.Services.AddSingleton<IAppChain>(composed.AppChain);
            if (composed.Sequencer != null)
            {
                builder.Services.AddSingleton<ISequencer>(composed.Sequencer);
            }
            if (composed.ProducerAuthority != null)
            {
                builder.Services.AddSingleton(composed.ProducerAuthority);
                if (composed.ProducerAuthorityNodeId != null)
                {
                    builder.Services.AddSingleton(new ProducerAuthorityIdentity(composed.ProducerAuthorityNodeId));
                }
            }
            builder.Services.AddSingleton<IBlockStore>(composed.Bundle.Blocks);
            builder.Services.AddSingleton<ITransactionStore>(composed.Bundle.Transactions);
            builder.Services.AddSingleton<IReceiptStore>(composed.Bundle.Receipts);
            builder.Services.AddSingleton<ILogStore>(composed.Bundle.Logs);
            builder.Services.AddSingleton<IStateStore>(composed.Bundle.State);
            builder.Services.AddSingleton<IFinalityTracker>(composed.FinalityTracker);
            if (composed.MudResult != null)
            {
                builder.Services.AddSingleton(composed.MudResult);
            }
            var rocksDbManager = composed.ChainNode.Storage.Manager;
            if (rocksDbManager != null)
            {
                builder.Services.AddSingleton(rocksDbManager);
            }

            builder.Services.AddSingleton<IMessageResultStore>(composed.MessageResultStore);
            if (composed.ChainNode.Sync?.Pool != null)
            {
                builder.Services.AddSingleton<DevP2P.Sync.Abstractions.IPeerPool>(composed.ChainNode.Sync.Pool);
            }
            builder.Services.AddSingleton<IMessageMerkleAccumulator>(composed.MessageAccumulator);
            if (composed.Sequencer != null)
            {
                builder.Services.AddSingleton<IHostedService>(sp =>
                    new SequencerHosting.SequencerHostedService(composed.Sequencer, alreadyStarted: true,
                        sp.GetService<ILoggerFactory>()?.CreateLogger<SequencerHosting.SequencerHostedService>()));
            }
            builder.Services.AddSingleton<IHostedService>(sp =>
                new Server.Metrics.MetricsCollector(
                    composed.Metrics.BlockProduction,
                    composed.Metrics.Sync,
                    composed.AppChain,
                    sp.GetRequiredService<MetricsConfig>(),
                    composed.Metrics.TxPool,
                    composed.Sequencer?.TxPool,
                    sp.GetService<ILoggerFactory>()?.CreateLogger<Server.Metrics.MetricsCollector>()));
            if (rocksDbManager != null)
            {
                builder.Services.AddSingleton<IHostedService>(sp =>
                    new RocksDbHosting.RocksDbLifetimeService(rocksDbManager,
                        sp.GetService<ILoggerFactory>()?.CreateLogger<RocksDbHosting.RocksDbLifetimeService>()));
            }

            return builder;
        }

        private static void RegisterMessaging(IServiceCollection services, AppChainServerConfig config, AppChainComposedNode composed, ILoggerFactory loggerFactory, ILogger logger)
        {
            if (config.Messaging.Enabled && composed.Produces)
            {
                var messagingConfig = new MessagingConfig
                {
                    Enabled = true,
                    PollIntervalMs = config.Messaging.PollIntervalMs,
                    MaxMessagesPerPoll = config.Messaging.MaxMessagesPerPoll
                };

                foreach (var entry in config.Messaging.HubSourceChains)
                {
                    var parsed = ParseHubSourceChainEntry(entry);
                    if (parsed != null)
                    {
                        messagingConfig.SourceChains.Add(parsed);
                    }
                    else
                    {
                        logger.LogWarning("Invalid hub-source-chain format: '{Entry}' (expected chainId|rpcUrl|hubAddress)", entry);
                    }
                }

                if (messagingConfig.SourceChains.Count > 0)
                {
                    var messageIndexStore = new InMemoryMessageIndexStore();

                    var logProcessingWorker = new HubLogProcessingWorker(
                        messageIndexStore,
                        (ulong)config.ChainId,
                        messagingConfig,
                        blockValidator: null,
                        loggerFactory.CreateLogger<HubLogProcessingWorker>());

                    services.AddSingleton<IHostedService>(logProcessingWorker);

                    var messagingService = new MessagingService(
                        (ulong)config.ChainId,
                        messagingConfig,
                        messageIndexStore,
                        composed.MessageQueue,
                        loggerFactory.CreateLogger<MessagingService>(),
                        composed.MessageAccumulator);

                    var messagingWorker = new MessagingWorker(
                        messagingService,
                        messagingConfig,
                        loggerFactory.CreateLogger<MessagingWorker>());

                    services.AddSingleton<IHostedService>(messagingWorker);
                    logger.LogInformation("Messaging worker registered for {Count} source chains with log processing", messagingConfig.SourceChains.Count);

                    if (config.Messaging.AcknowledgmentEnabled && !string.IsNullOrEmpty(config.Consensus.Sequencer.PrivateKey))
                    {
                        var ackServices = new Dictionary<ulong, IMessageAcknowledgmentService>();
                        foreach (var source in messagingConfig.SourceChains)
                        {
                            ackServices[source.ChainId] = new HubMessageAcknowledgmentService(
                                (ulong)config.ChainId,
                                source.RpcUrl,
                                source.HubContractAddress,
                                config.Consensus.Sequencer.PrivateKey,
                                source.ChainId,
                                loggerFactory.CreateLogger<HubMessageAcknowledgmentService>());
                        }

                        var ackConfig = new MessageAcknowledgmentConfig
                        {
                            Enabled = true,
                            IntervalMs = config.Messaging.AcknowledgmentIntervalMs,
                            MaxRetries = 3,
                            RetryDelayMs = 2000
                        };

                        var ackWorker = new MessageAcknowledgmentWorker(
                            composed.MessageAccumulator,
                            ackServices,
                            ackConfig,
                            loggerFactory.CreateLogger<MessageAcknowledgmentWorker>());

                        services.AddSingleton<IHostedService>(ackWorker);
                        logger.LogInformation("Acknowledgment worker registered for {Count} source chains (interval: {Ms}ms)",
                            ackServices.Count, config.Messaging.AcknowledgmentIntervalMs);
                    }
                }
            }
        }

        public static SourceChainConfig ParseHubSourceChainEntry(string entry)
        {
            var parts = entry.Contains('|') ? TrySplitPipeDelimited(entry) : TrySplitColonDelimited(entry);

            if (parts.Length >= 3 && ulong.TryParse(parts[0], out var srcChainId))
            {
                return new SourceChainConfig
                {
                    ChainId = srcChainId,
                    RpcUrl = parts[1],
                    HubContractAddress = parts[2]
                };
            }

            return null;
        }

        public static string[] TrySplitPipeDelimited(string entry) => entry.Split('|');

        public static string[] TrySplitColonDelimited(string entry)
        {
            var lastColon = entry.LastIndexOf(":0x", StringComparison.OrdinalIgnoreCase);
            if (lastColon < 0) lastColon = entry.LastIndexOf(':');
            var firstColon = entry.IndexOf(':');
            if (lastColon > firstColon && firstColon > 0)
            {
                return new[]
                {
                    entry.Substring(0, firstColon),
                    entry.Substring(firstColon + 1, lastColon - firstColon - 1),
                    entry.Substring(lastColon + 1)
                };
            }

            return entry.Split(':');
        }

        private static AppChainRpcWiring WireRpc(WebApplication app, AppChainServerConfig config, AppChainComposedNode composed, ILogger logger)
        {
            app.UseWebSockets();

            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                TypeInfoResolver = CoreChainJsonContext.Default
            };

            var rpcRegistry = new RpcHandlerRegistry();
            rpcRegistry.AddStandardHandlers();
            rpcRegistry.AddMessageProofHandlers();
            rpcRegistry.AddAdminHandlers();

            var rpcServices = app.Services;
            var rpcContext = new RpcContext(composed.Node, (long)config.ChainId, rpcServices);
            if (composed.Sequencer != null)
            {
                rpcContext.TxPool = composed.Sequencer.TxPool;
            }
            var rpcDispatcher = new InstrumentedRpcDispatcher(rpcRegistry, rpcContext, composed.Metrics.Rpc, logger, serializerOptions);

            var subscriptionManager = new SubscriptionManager();
            var wsHandler = new WebSocketRpcHandler(subscriptionManager, rpcRegistry, rpcContext, serializerOptions);

            if (composed.Sequencer != null)
            {
                composed.Sequencer.BlockProduced += async (sender, result) =>
                {
                    try
                    {
                        var blockLogs = await composed.Bundle.Logs.GetLogsByBlockNumberAsync(result.Header.BlockNumber);
                        await wsHandler.BroadcastBlockAsync(result.Header, result.BlockHash, blockLogs);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to broadcast WebSocket notifications for block {BlockNumber}", result.Header.BlockNumber);
                    }
                };
            }

            return new AppChainRpcWiring(serializerOptions, rpcDispatcher, wsHandler);
        }

        private static void MapEndpoints(WebApplication app, AppChainServerConfig config, AppChainComposedNode composed, AppChainRpcWiring rpc)
        {
            app.MapPost("/", async (HttpContext httpContext) =>
            {
                using var reader = new StreamReader(httpContext.Request.Body);
                var body = await reader.ReadToEndAsync();

                JsonRpcRequest? jsonRequest;
                try
                {
                    jsonRequest = JsonSerializer.Deserialize<JsonRpcRequest>(body, rpc.SerializerOptions);
                }
                catch
                {
                    httpContext.Response.StatusCode = 400;
                    httpContext.Response.ContentType = "application/json";
                    await httpContext.Response.WriteAsync("{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32700,\"message\":\"Parse error\"}}");
                    return;
                }

                if (jsonRequest == null || string.IsNullOrEmpty(jsonRequest.Method))
                {
                    httpContext.Response.StatusCode = 400;
                    httpContext.Response.ContentType = "application/json";
                    await httpContext.Response.WriteAsync("{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32600,\"message\":\"Invalid Request\"}}");
                    return;
                }

                var request = new RpcRequestMessage(jsonRequest.Id, jsonRequest.Method);
                if (jsonRequest.Params.HasValue)
                {
                    request.RawParameters = jsonRequest.Params.Value;
                }
                var response = await rpc.Dispatcher.DispatchAsync(request);

                var jsonResponse = response.ToJsonRpcResponse();

                var responseJson = JsonSerializer.Serialize(jsonResponse, rpc.SerializerOptions);

                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(responseJson);
            });

            app.MapWebSocketEndpoint(rpc.WebSocketHandler);

            app.MapHealthChecks("/health");

            app.MapLiveBlockEndpoints();

            app.MapGet("/status", async () =>
            {
                var status = new AppChainStatus
                {
                    ChainId = (long)config.ChainId,
                    ChainName = config.ChainName,
                    BlockNumber = (long)await composed.AppChain.GetBlockNumberAsync(),
                    RpcUrl = config.RpcUrl,
                    ConsensusMode = Configuration.AppChainConsensusModeParser.NameOf(config.Consensus.Mode),
                    IsCurrentSequencer = composed.ProducerAuthority != null
                        ? string.Equals(
                            composed.ProducerAuthority.CurrentProducer(),
                            composed.ProducerAuthorityNodeId,
                            StringComparison.OrdinalIgnoreCase)
                        : null,
                    LeaseFencingToken = (composed.ProducerAuthority as ArbiterBackedProducerAuthority)?.LastKnownFencingToken,
                    Accounts = new AccountsStatus
                    {
                        GenesisOwner = config.Genesis.Owner.Address!,
                        Sequencer = config.Consensus.Sequencer.Address!
                    },
                    Contracts = composed.MudResult != null ? new ContractsStatus
                    {
                        Create2Factory = composed.MudResult.Create2FactoryAddress,
                        WorldFactory = composed.MudResult.WorldFactoryAddress,
                        World = composed.MudResult.WorldAddress,
                        InitModule = composed.MudResult.InitModuleAddress,
                        AccessManagementSystem = composed.MudResult.AccessManagementSystemAddress,
                        BalanceTransferSystem = composed.MudResult.BalanceTransferSystemAddress,
                        BatchCallSystem = composed.MudResult.BatchCallSystemAddress,
                        RegistrationSystem = composed.MudResult.RegistrationSystemAddress
                    } : null,
                    Anchoring = !string.IsNullOrEmpty(config.Anchoring.L1RpcUrl) ? new AnchoringStatus
                    {
                        Enabled = true,
                        L1RpcUrl = config.Anchoring.L1RpcUrl,
                        AnchorContract = config.Anchoring.ContractAddress,
                        LastAnchoredBlock = 0
                    } : null,
                };

                return Results.Ok(status);
            });
        }

        private static async Task RunUntilShutdownAsync(WebApplication app, AppChainServerConfig config, AppChainComposedNode composed, ILogger logger)
        {
            logger.LogInformation("Starting HTTP server at {Url}", config.RpcUrl);
            PrintReadyBanner(config, composed.MudResult, logger);

            app.MapPrometheusMetrics(config.Node.Rpc.Host, config.Node.Rpc.Port, config.Node.Rpc.MetricsPort);

            if (config.Node.Rpc.MetricsPort > 0 || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
                await app.RunAsync();
            else
                await app.RunAsync($"http://{config.Node.Rpc.Host}:{config.Node.Rpc.Port}");
        }

        private sealed class AppChainRpcWiring
        {
            public AppChainRpcWiring(JsonSerializerOptions serializerOptions, InstrumentedRpcDispatcher dispatcher, WebSocketRpcHandler webSocketHandler)
            {
                SerializerOptions = serializerOptions;
                Dispatcher = dispatcher;
                WebSocketHandler = webSocketHandler;
            }

            public JsonSerializerOptions SerializerOptions { get; }
            public InstrumentedRpcDispatcher Dispatcher { get; }
            public WebSocketRpcHandler WebSocketHandler { get; }
        }

        private static void PrintBanner(AppChainServerConfig config, ILogger logger)
        {
            Console.WriteLine();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                      Nethereum AppChain Server                        ║");
            Console.WriteLine("╠═══════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Chain ID:           {config.ChainId,-53} ║");
            Console.WriteLine($"║ Chain Name:         {config.ChainName,-53} ║");
            Console.WriteLine($"║ RPC URL:            {config.RpcUrl,-53} ║");
            Console.WriteLine($"║ Consensus:          {Configuration.AppChainConsensusModeParser.NameOf(config.Consensus.Mode),-53} ║");
            Console.WriteLine($"║ Storage:            {(config.Node.Storage.InMemory ? "In-Memory" : $"RocksDB ({config.Node.Storage.DataDirectory})"),-53} ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }

        private static void PrintMudDeployment(Nethereum.AppChain.MudGenesisResult result, ILogger logger)
        {
            Console.WriteLine();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                        MUD World Deployed                              ║");
            Console.WriteLine("╠═══════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ World:              {result.WorldAddress,-53} ║");
            Console.WriteLine($"║ WorldFactory:       {result.WorldFactoryAddress,-53} ║");
            Console.WriteLine($"║ Create2Factory:     {result.Create2FactoryAddress,-53} ║");
            Console.WriteLine("╠═══════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ InitModule:         {result.InitModuleAddress,-53} ║");
            Console.WriteLine($"║ AccessManagement:   {result.AccessManagementSystemAddress,-53} ║");
            Console.WriteLine($"║ BalanceTransfer:    {result.BalanceTransferSystemAddress,-53} ║");
            Console.WriteLine($"║ BatchCall:          {result.BatchCallSystemAddress,-53} ║");
            Console.WriteLine($"║ Registration:       {result.RegistrationSystemAddress,-53} ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }

        private static void PrintReadyBanner(AppChainServerConfig config, Nethereum.AppChain.MudGenesisResult? mudResult, ILogger logger)
        {
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"  Ready for connections at {config.RpcUrl}");
            if (mudResult != null)
            {
                Console.WriteLine($"  MUD World Address: {mudResult.WorldAddress}");
            }
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine();
        }
    }

    public class AppChainStatus
    {
        public long ChainId { get; set; }
        public string ChainName { get; set; } = "";
        public long BlockNumber { get; set; }
        public string RpcUrl { get; set; } = "";
        public string ConsensusMode { get; set; } = "";
        public string P2PMode { get; set; } = "";
        public bool? IsCurrentSequencer { get; set; }
        public long? LeaseFencingToken { get; set; }
        public AccountsStatus? Accounts { get; set; }
        public ContractsStatus? Contracts { get; set; }
        public AnchoringStatus? Anchoring { get; set; }
    }

    public class AccountsStatus
    {
        public string GenesisOwner { get; set; } = "";
        public string Sequencer { get; set; } = "";
    }

    public class ContractsStatus
    {
        public string Create2Factory { get; set; } = "";
        public string WorldFactory { get; set; } = "";
        public string World { get; set; } = "";
        public string InitModule { get; set; } = "";
        public string AccessManagementSystem { get; set; } = "";
        public string BalanceTransferSystem { get; set; } = "";
        public string BatchCallSystem { get; set; } = "";
        public string RegistrationSystem { get; set; } = "";
    }

    public class AnchoringStatus
    {
        public bool Enabled { get; set; }
        public string? L1RpcUrl { get; set; }
        public string? AnchorContract { get; set; }
        public long LastAnchoredBlock { get; set; }
    }

}
