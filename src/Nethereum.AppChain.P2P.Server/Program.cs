using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain;
using Nethereum.AppChain.Genesis;
using Nethereum.CoreChain.Metrics;
using Nethereum.AppChain.P2P.DotNetty;
using Nethereum.AppChain.Sequencer;
using Nethereum.Consensus.Clique;
using Nethereum.CoreChain;
using Nethereum.CoreChain.P2P;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.Signer;

namespace Nethereum.AppChain.P2P.Server
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var hostOption = new Option<string>("--host", () => "127.0.0.1", "Host to bind HTTP server to");
            var portOption = new Option<int>("--port", () => 8546, "HTTP port to listen on");
            var p2pPortOption = new Option<int>("--p2p-port", () => 30303, "P2P port to listen on");
            var chainIdOption = new Option<long>("--chain-id", () => 420420, "Chain ID");
            var chainNameOption = new Option<string>("--name", () => "CliqueP2P", "Chain name");
            var signerKeyOption = new Option<string>("--signer-key", "Signer private key (Clique PoA)") { IsRequired = true };
            var signersOption = new Option<string[]>("--signers", "Initial signer addresses (comma-separated)")
            {
                AllowMultipleArgumentsPerToken = true
            };
            var peersOption = new Option<string[]>("--peers", "Peer endpoints to connect to (format: nodeId@host:port)")
            {
                AllowMultipleArgumentsPerToken = true
            };
            var blockTimeOption = new Option<int>("--block-time", () => 1000, "Block time in milliseconds");
            var nodeIdOption = new Option<string>("--node-id", () => "node-0", "Node identifier");

            var rootCommand = new RootCommand("Nethereum AppChain P2P Server - HTTP JSON-RPC with Clique PoA consensus")
            {
                hostOption,
                portOption,
                p2pPortOption,
                chainIdOption,
                chainNameOption,
                signerKeyOption,
                signersOption,
                peersOption,
                blockTimeOption,
                nodeIdOption
            };

            rootCommand.SetHandler(async (context) =>
            {
                var config = new P2PServerConfig
                {
                    Host = context.ParseResult.GetValueForOption(hostOption)!,
                    Port = context.ParseResult.GetValueForOption(portOption),
                    P2PPort = context.ParseResult.GetValueForOption(p2pPortOption),
                    ChainId = context.ParseResult.GetValueForOption(chainIdOption),
                    ChainName = context.ParseResult.GetValueForOption(chainNameOption)!,
                    SignerPrivateKey = context.ParseResult.GetValueForOption(signerKeyOption)!,
                    Signers = context.ParseResult.GetValueForOption(signersOption) ?? Array.Empty<string>(),
                    Peers = context.ParseResult.GetValueForOption(peersOption) ?? Array.Empty<string>(),
                    BlockTimeMs = context.ParseResult.GetValueForOption(blockTimeOption),
                    NodeId = context.ParseResult.GetValueForOption(nodeIdOption)!
                };

                await RunServerAsync(config);
            });

            return await rootCommand.InvokeAsync(args);
        }

        private static async Task RunServerAsync(P2PServerConfig config)
        {
            EthECKey.SignRecoverable = true;

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            var logger = loggerFactory.CreateLogger<Program>();

            var signerKey = new EthECKey(config.SignerPrivateKey);
            var signerAddress = signerKey.GetPublicAddress().ToLowerInvariant();

            PrintBanner(config, signerAddress, logger);

            logger.LogInformation("Initializing storage...");

            // Create storage
            var blockStore = new InMemoryBlockStore();
            var transactionStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var stateStore = new InMemoryStateStore();

            // Create AppChain config
            var appChainConfig = AppChainConfig.CreateWithName(config.ChainName, config.ChainId);
            appChainConfig.SequencerAddress = signerAddress;
            appChainConfig.Coinbase = signerAddress;
            appChainConfig.BaseFee = 0;
            appChainConfig.BlockGasLimit = 30_000_000;

            // Create AppChain
            var appChain = new Nethereum.AppChain.AppChain(
                appChainConfig,
                blockStore,
                transactionStore,
                receiptStore,
                logStore,
                stateStore);

            var initialSigners = config.Signers.Length > 0
                ? config.Signers.Select(s => s.ToLowerInvariant()).ToList()
                : new List<string> { signerAddress };

            var genesisOptions = new GenesisOptions
            {
                DeployCreate2Factory = true,
                PrefundedAddresses = initialSigners.ToArray(),
                PrefundBalance = BigInteger.Parse("1000000000000000000000")
            };

            logger.LogInformation("Creating AppChain...");
            await appChain.InitializeAsync(genesisOptions);
            logger.LogInformation("Genesis block created");

            // Create P2P transport
            var transportConfig = new DotNettyConfig
            {
                ListenAddress = config.Host,
                ListenPort = config.P2PPort,
                ChainId = config.ChainId,
                MaxConnections = 50,
                ConnectionTimeoutMs = 5000,
                NodePrivateKey = config.SignerPrivateKey
            };

            var transport = new DotNettyTransport(
                transportConfig,
                loggerFactory.CreateLogger<DotNettyTransport>());

            // Create Clique consensus components
            var cliqueConfig = new CliqueConfig
            {
                BlockPeriodSeconds = config.BlockTimeMs / 1000,
                EpochLength = 30000,
                WiggleTimeMs = 200,
                InitialSigners = initialSigners,
                LocalSignerAddress = signerAddress,
                LocalSignerPrivateKey = config.SignerPrivateKey,
                AllowEmptyBlocks = false,
                EnableVoting = true
            };

            var cliqueEngine = new CliqueEngine(cliqueConfig, loggerFactory.CreateLogger<CliqueEngine>());
            cliqueEngine.ApplyGenesisSigners(initialSigners);

            var cliqueStrategy = new CliqueBlockProductionStrategy(
                appChainConfig,
                cliqueEngine,
                loggerFactory.CreateLogger<CliqueBlockProductionStrategy>());

            // Create Sequencer with Clique strategy
            var sequencerConfig = new SequencerConfig
            {
                BlockTimeMs = config.BlockTimeMs,
                MaxTransactionsPerBlock = 1000,
                AllowEmptyBlocks = false,
                BlockProductionMode = config.BlockTimeMs > 0
                    ? BlockProductionMode.Interval
                    : BlockProductionMode.OnDemand
            };

            var sequencer = new Sequencer.Sequencer(
                appChain,
                sequencerConfig,
                blockProductionStrategy: cliqueStrategy,
                logger: loggerFactory.CreateLogger<Sequencer.Sequencer>());

            // Create ChainNode for RPC
            var chainNode = new AppChainNode(appChain, sequencer);

            // Wire up P2P block broadcast
            cliqueStrategy.BlockFinalized += async (sender, e) =>
            {
                try
                {
                    var blockMsg = CreateNewBlockMessage(e.Header, e.Result);
                    await transport.BroadcastAsync(blockMsg);
                    logger.LogDebug("Broadcast block {Number} to peers", e.Header.BlockNumber);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to broadcast block {Number}", e.Header.BlockNumber);
                }
            };

            // Start P2P transport
            await transport.StartAsync();
            logger.LogInformation("P2P transport started on port {P2PPort}", config.P2PPort);

            // Start sequencer
            await sequencer.StartAsync();
            logger.LogInformation("Sequencer started with Clique consensus");

            // Connect to peers
            foreach (var peer in config.Peers)
            {
                var parts = peer.Split('@');
                if (parts.Length == 2)
                {
                    var peerId = parts[0];
                    var endpoint = parts[1];
                    try
                    {
                        await transport.ConnectAsync(peerId, endpoint);
                        logger.LogInformation("Connected to peer {PeerId} at {Endpoint}", peerId, endpoint);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to connect to peer {PeerId} at {Endpoint}", peerId, endpoint);
                    }
                }
            }

            // Build web app
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddSingleton(chainNode);
            builder.Services.AddSingleton<IChainNode>(chainNode);
            builder.Services.AddSingleton(sequencer);
            builder.Services.AddSingleton<IAppChain>(appChain);
            builder.Services.AddSingleton<IBlockStore>(blockStore);
            builder.Services.AddSingleton<ITransactionStore>(transactionStore);
            builder.Services.AddSingleton<IReceiptStore>(receiptStore);
            builder.Services.AddSingleton<ILogStore>(logStore);
            builder.Services.AddSingleton<IStateStore>(stateStore);
            builder.Services.AddSingleton<IP2PTransport>(transport);
            var rpcMetrics = new RpcMetrics(config.ChainId.ToString(), config.ChainName ?? "Nethereum");
            builder.Services.AddSingleton(rpcMetrics);

            var app = builder.Build();

            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                TypeInfoResolver = CoreChainJsonContext.Default
            };

            var rpcRegistry = new RpcHandlerRegistry();
            rpcRegistry.AddStandardHandlers();

            var rpcContext = new RpcContext(chainNode, config.ChainId, app.Services);
            var rpcDispatcher = new InstrumentedRpcDispatcher(rpcRegistry, rpcContext, rpcMetrics, logger, serializerOptions);

            app.MapPost("/", async (HttpContext httpContext) =>
            {
                using var reader = new StreamReader(httpContext.Request.Body);
                var body = await reader.ReadToEndAsync();

                JsonRpcRequest? jsonRequest;
                try
                {
                    jsonRequest = JsonSerializer.Deserialize<JsonRpcRequest>(body, serializerOptions);
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
                var response = await rpcDispatcher.DispatchAsync(request);

                var jsonResponse = new JsonRpcResponse
                {
                    Id = response.Id,
                    Result = response.HasError ? null : response.Result,
                    Error = response.HasError ? new JsonRpcError { Code = response.Error.Code, Message = response.Error.Message, Data = response.Error.Data } : null
                };

                var responseJson = JsonSerializer.Serialize(jsonResponse, serializerOptions);

                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(responseJson);
            });

            app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

            app.MapGet("/status", () =>
            {
                var peers = transport.ConnectedPeers;
                var status = new P2PServerStatus
                {
                    ChainId = config.ChainId,
                    ChainName = config.ChainName,
                    NodeId = transport.NodeId,
                    BlockNumber = (long)appChain.GetBlockNumberAsync().GetAwaiter().GetResult(),
                    RpcUrl = $"http://{config.Host}:{config.Port}",
                    P2PPort = config.P2PPort,
                    SignerAddress = signerAddress,
                    ConnectedPeers = peers.Count,
                    Peers = peers.ToList()
                };
                return Results.Ok(status);
            });

            var rpcUrl = $"http://{config.Host}:{config.Port}";
            logger.LogInformation("Starting HTTP server at {Url}", rpcUrl);
            PrintReadyBanner(config, signerAddress, logger);

            try
            {
                await app.RunAsync($"http://{config.Host}:{config.Port}");
            }
            finally
            {
                await sequencer.StopAsync();
                await transport.StopAsync();
                transport.Dispose();
                cliqueEngine.Dispose();
            }
        }

        private static P2PMessage CreateNewBlockMessage(BlockHeader header, BlockProductionResult result)
        {
            var headerBytes = BlockHeaderEncoder.Current.Encode(header);
            var txBytes = result.TransactionResults
                .Select(tr => tr.TxHash)
                .Where(h => h != null)
                .ToArray();

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(headerBytes.Length);
            writer.Write(headerBytes);
            writer.Write(txBytes.Length);
            foreach (var txHash in txBytes)
            {
                writer.Write(txHash.Length);
                writer.Write(txHash);
            }

            return new P2PMessage(P2PMessageType.NewBlock, ms.ToArray());
        }

        private static void PrintBanner(P2PServerConfig config, string signerAddress, ILogger logger)
        {
            Console.WriteLine();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                   Nethereum AppChain P2P Server                       ║");
            Console.WriteLine("║                      Clique PoA Consensus                             ║");
            Console.WriteLine("╠═══════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Node ID:            {config.NodeId,-53} ║");
            Console.WriteLine($"║ Chain ID:           {config.ChainId,-53} ║");
            Console.WriteLine($"║ Chain Name:         {config.ChainName,-53} ║");
            Console.WriteLine($"║ Signer:             {signerAddress,-53} ║");
            Console.WriteLine($"║ RPC URL:            http://{config.Host}:{config.Port,-45} ║");
            Console.WriteLine($"║ P2P Port:           {config.P2PPort,-53} ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }

        private static void PrintReadyBanner(P2PServerConfig config, string signerAddress, ILogger logger)
        {
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"  Ready for connections at http://{config.Host}:{config.Port}");
            Console.WriteLine($"  P2P listening on port {config.P2PPort}");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine();
        }
    }

    public class P2PServerConfig
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8546;
        public int P2PPort { get; set; } = 30303;
        public long ChainId { get; set; } = 420420;
        public string ChainName { get; set; } = "CliqueP2P";
        public string SignerPrivateKey { get; set; } = "";
        public string[] Signers { get; set; } = Array.Empty<string>();
        public string[] Peers { get; set; } = Array.Empty<string>();
        public int BlockTimeMs { get; set; } = 1000;
        public string NodeId { get; set; } = "node-0";
    }

    public class P2PServerStatus
    {
        public long ChainId { get; set; }
        public string ChainName { get; set; } = "";
        public string NodeId { get; set; } = "";
        public long BlockNumber { get; set; }
        public string RpcUrl { get; set; } = "";
        public int P2PPort { get; set; }
        public string SignerAddress { get; set; } = "";
        public int ConnectedPeers { get; set; }
        public List<string> Peers { get; set; } = new();
    }
}
