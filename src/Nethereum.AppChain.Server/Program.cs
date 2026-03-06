using System.CommandLine;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain;
using Nethereum.AppChain.Genesis;
using Nethereum.AppChain.Sequencer;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Rpc;
using Nethereum.Signer;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.AppChain.Anchoring;
using Nethereum.AppChain.Sync;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.AppChain.Server.Endpoints;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.AppChain.Anchoring.Metrics;
using Nethereum.AppChain.Sequencer.Metrics;
using Nethereum.AppChain.Server.Metrics;
using Nethereum.AppChain.Sync.Metrics;
using Nethereum.CoreChain.Metrics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.AppChain.P2P;
using Nethereum.AppChain.Anchoring.Messaging;
using Nethereum.AppChain.Anchoring.Rpc;
using Nethereum.AppChain.P2P.BlockHandling;
using Nethereum.AppChain.P2P.DotNetty;
using Nethereum.Consensus.Clique;
using Nethereum.CoreChain.P2P;
using Nethereum.CoreChain.Rpc.Subscriptions;
using Nethereum.Model;
using P2PHosting = Nethereum.AppChain.P2P.Hosting;
using SequencerHosting = Nethereum.AppChain.Sequencer.Hosting;
using SyncHosting = Nethereum.AppChain.Sync.Hosting;
using RocksDbHosting = Nethereum.CoreChain.RocksDB.Hosting;

namespace Nethereum.AppChain.Server
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var hostOption = new Option<string>("--host", () => "127.0.0.1", "Host to bind to");
            var portOption = new Option<int>("--port", () => 8546, "Port to listen on");
            var chainIdOption = new Option<long>("--chain-id", () => 420420, "Chain ID");
            var chainNameOption = new Option<string>("--name", () => "AppChain", "Chain name");
            var genesisOwnerKeyOption = new Option<string?>("--genesis-owner-key", "Genesis owner private key (deploys MUD, owns root namespace)");
            var genesisOwnerAddressOption = new Option<string?>("--genesis-owner-address", "Genesis owner address (for follower mode, no private key needed)");
            var sequencerKeyOption = new Option<string?>("--sequencer-key", "Sequencer private key (produces blocks)");
            var sequencerAddressOption = new Option<string?>("--sequencer-address", "Sequencer address (for follower mode, no private key needed)");
            var blockTimeOption = new Option<int>("--block-time", () => 1000, "Block time in milliseconds");
            var allowEmptyBlocksOption = new Option<bool>("--allow-empty-blocks", () => false, "Produce blocks even when no pending transactions");
            var deployMudOption = new Option<bool>("--deploy-mud-world", () => false, "Deploy MUD World contracts");
            var worldSaltOption = new Option<string?>("--world-salt", "MUD World salt (32 bytes hex)");
            var dbPathOption = new Option<string>("--db-path", () => "./datachain-data", "Database path");
            var inMemoryOption = new Option<bool>("--in-memory", () => false, "Use in-memory storage");
            var l1RpcOption = new Option<string?>("--l1-rpc", "L1 RPC URL for anchoring");
            var anchorContractOption = new Option<string?>("--anchor-contract", "Anchor contract address on L1");
            var anchorCadenceOption = new Option<int>("--anchor-cadence", () => 100, "Anchor every N blocks");
            var batchProductionOption = new Option<bool>("--batch-production", () => false, "Enable batch production");
            var batchCadenceOption = new Option<int>("--batch-cadence", () => 100, "Create batch every N blocks");
            var batchOutputOption = new Option<string>("--batch-output", () => "./batches", "Batch output directory");

            // Consensus and P2P mode options
            var consensusModeOption = new Option<string>("--consensus", () => "single-sequencer", "Consensus mode: single-sequencer or clique");
            var p2pModeOption = new Option<string>("--p2p", () => "none", "P2P mode: none or dotnetty");
            var p2pPortOption = new Option<int>("--p2p-port", () => 30303, "P2P listen port");
            var p2pListenOption = new Option<string>("--p2p-listen", () => "0.0.0.0", "P2P listen address");
            var bootstrapNodesOption = new Option<string[]>("--bootstrap-nodes", Array.Empty<string>, "Bootstrap node endpoints (host:port)");
            var signerKeyOption = new Option<string?>("--signer-key", "Clique signer private key");
            var initialSignersOption = new Option<string[]>("--initial-signers", Array.Empty<string>, "Initial Clique signers (addresses)");
            var cliquePeriodOption = new Option<int>("--clique-period", () => 15, "Clique block period in seconds");
            var cliqueEpochOption = new Option<int>("--clique-epoch", () => 30000, "Clique epoch length");

            // HTTP Sync options (alternative to P2P)
            var syncPeersOption = new Option<string[]>("--sync-peers", Array.Empty<string>, "HTTP sync peer URLs (e.g., http://localhost:8545,http://localhost:8546)");
            var syncPollIntervalOption = new Option<int>("--sync-poll-interval", () => 1000, "Sync poll interval in milliseconds");
            var autoSyncOption = new Option<bool>("--auto-sync", () => true, "Automatically sync on startup");
            var enableStateSyncOption = new Option<bool>("--enable-state-sync", () => true, "Re-execute transactions during sync to build local state");

            // Cross-chain messaging options
            var enableMessagingOption = new Option<bool>("--enable-messaging", () => false, "Enable cross-chain message processing");
            var hubSourceChainsOption = new Option<string[]>("--hub-source-chains", Array.Empty<string>, "Hub source chains (format: chainId:rpcUrl:hubAddress)");
            var messagePollIntervalOption = new Option<int>("--message-poll-interval", () => 5000, "Message poll interval in milliseconds");
            var maxMessagesPerPollOption = new Option<int>("--max-messages-per-poll", () => 100, "Max messages per poll cycle");
            var enableAcknowledgmentOption = new Option<bool>("--enable-acknowledgment", () => false, "Enable message acknowledgment back to source Hubs");
            var acknowledgmentIntervalOption = new Option<int>("--acknowledgment-interval", () => 30000, "Message acknowledgment interval in milliseconds");

            var otlpEndpointOption = new Option<string?>("--otlp-endpoint", "OpenTelemetry OTLP endpoint URL (falls back to OTEL_EXPORTER_OTLP_ENDPOINT env var)");

            var rootCommand = new RootCommand("Nethereum AppChain Server - HTTP JSON-RPC server with MUD World deployment")
            {
                hostOption,
                portOption,
                chainIdOption,
                chainNameOption,
                genesisOwnerKeyOption,
                genesisOwnerAddressOption,
                sequencerKeyOption,
                sequencerAddressOption,
                blockTimeOption,
                allowEmptyBlocksOption,
                deployMudOption,
                worldSaltOption,
                dbPathOption,
                inMemoryOption,
                l1RpcOption,
                anchorContractOption,
                anchorCadenceOption,
                batchProductionOption,
                batchCadenceOption,
                batchOutputOption,
                consensusModeOption,
                p2pModeOption,
                p2pPortOption,
                p2pListenOption,
                bootstrapNodesOption,
                signerKeyOption,
                initialSignersOption,
                cliquePeriodOption,
                cliqueEpochOption,
                syncPeersOption,
                syncPollIntervalOption,
                autoSyncOption,
                enableStateSyncOption,
                enableMessagingOption,
                hubSourceChainsOption,
                messagePollIntervalOption,
                maxMessagesPerPollOption,
                enableAcknowledgmentOption,
                acknowledgmentIntervalOption,
                otlpEndpointOption
            };

            rootCommand.SetHandler(async (context) =>
            {
                var config = new AppChainServerConfig
                {
                    Host = context.ParseResult.GetValueForOption(hostOption)!,
                    Port = context.ParseResult.GetValueForOption(portOption),
                    ChainId = context.ParseResult.GetValueForOption(chainIdOption),
                    ChainName = context.ParseResult.GetValueForOption(chainNameOption)!,
                    GenesisOwnerPrivateKey = context.ParseResult.GetValueForOption(genesisOwnerKeyOption),
                    GenesisOwnerAddress = context.ParseResult.GetValueForOption(genesisOwnerAddressOption),
                    SequencerPrivateKey = context.ParseResult.GetValueForOption(sequencerKeyOption),
                    SequencerAddress = context.ParseResult.GetValueForOption(sequencerAddressOption),
                    BlockTimeMs = context.ParseResult.GetValueForOption(blockTimeOption),
                    AllowEmptyBlocks = context.ParseResult.GetValueForOption(allowEmptyBlocksOption),
                    DeployMudWorld = context.ParseResult.GetValueForOption(deployMudOption),
                    DatabasePath = context.ParseResult.GetValueForOption(dbPathOption)!,
                    UseInMemoryStorage = context.ParseResult.GetValueForOption(inMemoryOption),
                    L1RpcUrl = context.ParseResult.GetValueForOption(l1RpcOption),
                    AnchorContractAddress = context.ParseResult.GetValueForOption(anchorContractOption),
                    AnchorCadence = context.ParseResult.GetValueForOption(anchorCadenceOption),
                    AutoCreateBatches = context.ParseResult.GetValueForOption(batchProductionOption),
                    BatchSize = context.ParseResult.GetValueForOption(batchCadenceOption),
                    BatchOutputDirectory = context.ParseResult.GetValueForOption(batchOutputOption),
                    ConsensusMode = context.ParseResult.GetValueForOption(consensusModeOption)!,
                    P2PMode = context.ParseResult.GetValueForOption(p2pModeOption)!,
                    P2PPort = context.ParseResult.GetValueForOption(p2pPortOption),
                    P2PListenAddress = context.ParseResult.GetValueForOption(p2pListenOption)!,
                    BootstrapNodes = context.ParseResult.GetValueForOption(bootstrapNodesOption) ?? Array.Empty<string>(),
                    SignerPrivateKey = context.ParseResult.GetValueForOption(signerKeyOption),
                    InitialSigners = context.ParseResult.GetValueForOption(initialSignersOption) ?? Array.Empty<string>(),
                    CliquePeriod = context.ParseResult.GetValueForOption(cliquePeriodOption),
                    CliqueEpoch = context.ParseResult.GetValueForOption(cliqueEpochOption),
                    SyncPeers = context.ParseResult.GetValueForOption(syncPeersOption) ?? Array.Empty<string>(),
                    SyncPollIntervalMs = context.ParseResult.GetValueForOption(syncPollIntervalOption),
                    AutoSyncOnStart = context.ParseResult.GetValueForOption(autoSyncOption),
                    EnableStateSync = context.ParseResult.GetValueForOption(enableStateSyncOption),
                    EnableMessaging = context.ParseResult.GetValueForOption(enableMessagingOption),
                    HubSourceChains = context.ParseResult.GetValueForOption(hubSourceChainsOption) ?? Array.Empty<string>(),
                    MessagePollIntervalMs = context.ParseResult.GetValueForOption(messagePollIntervalOption),
                    MaxMessagesPerPoll = context.ParseResult.GetValueForOption(maxMessagesPerPollOption),
                    EnableMessageAcknowledgment = context.ParseResult.GetValueForOption(enableAcknowledgmentOption),
                    AcknowledgmentIntervalMs = context.ParseResult.GetValueForOption(acknowledgmentIntervalOption),
                    OtlpEndpoint = context.ParseResult.GetValueForOption(otlpEndpointOption)
                };

                var worldSaltHex = context.ParseResult.GetValueForOption(worldSaltOption);
                if (!string.IsNullOrEmpty(worldSaltHex))
                {
                    config.MudWorldSalt = worldSaltHex.HexToByteArray();
                }

                await RunServerAsync(config);
            });

            return await rootCommand.InvokeAsync(args);
        }

        private static async Task RunServerAsync(AppChainServerConfig config)
        {
            config.DeriveAddresses();
            config.Validate();

            // Enable native secp256k1 for faster signing/verification
            EthECKey.SignRecoverable = true;

            using var loggerFactory = LoggerFactory.Create(builder =>
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
                builder.AddFilter("Nethereum.AppChain.P2P", LogLevel.Information);
            });

            var logger = loggerFactory.CreateLogger<Program>();

            PrintBanner(config, logger);

            logger.LogInformation("Initializing storage...");

            IBlockStore blockStore;
            ITransactionStore transactionStore;
            IReceiptStore receiptStore;
            ILogStore logStore;
            IStateStore stateStore;
            ITrieNodeStore? trieNodeStore = null;
            RocksDbManager? rocksDbManager = null;

            if (config.UseInMemoryStorage)
            {
                blockStore = new InMemoryBlockStore();
                transactionStore = new InMemoryTransactionStore(blockStore);
                receiptStore = new InMemoryReceiptStore();
                logStore = new InMemoryLogStore();
                stateStore = new HistoricalStateStore(new InMemoryStateStore(), new InMemoryStateDiffStore(), HistoricalStateOptions.Default);
                logger.LogInformation("Using in-memory storage");
            }
            else
            {
                var options = new RocksDbStorageOptions { DatabasePath = config.DatabasePath };
                rocksDbManager = new RocksDbManager(options);
                blockStore = new RocksDbBlockStore(rocksDbManager);
                transactionStore = new RocksDbTransactionStore(rocksDbManager, blockStore);
                receiptStore = new RocksDbReceiptStore(rocksDbManager, blockStore);
                logStore = new RocksDbLogStore(rocksDbManager);
                stateStore = new HistoricalStateStore(new RocksDbStateStore(rocksDbManager), new RocksDbStateDiffStore(rocksDbManager), HistoricalStateOptions.Default);
                trieNodeStore = new RocksDbTrieNodeStore(rocksDbManager);
                logger.LogInformation("Using RocksDB storage at: {Path}", config.DatabasePath);
            }

            var appChainConfig = AppChainConfig.CreateWithName(config.ChainName, config.ChainId);
            appChainConfig.SequencerAddress = config.SequencerAddress;

            var appChain = new Nethereum.AppChain.AppChain(
                appChainConfig,
                blockStore,
                transactionStore,
                receiptStore,
                logStore,
                stateStore,
                trieNodeStore);

            var isFollower = config.SyncPeers.Length > 0;

            if (!isFollower)
            {
                var genesisOptions = new GenesisOptions
                {
                    DeployCreate2Factory = true,
                    PrefundedAddresses = new[] { config.GenesisOwnerAddress!, config.SequencerAddress! },
                    PrefundBalance = BigInteger.Parse("1000000000000000000000")
                };

                logger.LogInformation("Creating AppChain (sequencer mode)...");
                await appChain.InitializeAsync(genesisOptions);

                logger.LogInformation("Genesis block created");
                logger.LogInformation("  Create2Factory: {Address}", Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS);
                logger.LogInformation("  Genesis Owner:  {Address}", config.GenesisOwnerAddress);
                logger.LogInformation("  Sequencer:      {Address}", config.SequencerAddress);
            }
            else
            {
                var genesisOptions = new GenesisOptions
                {
                    DeployCreate2Factory = true,
                    PrefundedAddresses = new[] { config.GenesisOwnerAddress!, config.SequencerAddress! },
                    PrefundBalance = BigInteger.Parse("1000000000000000000000")
                };

                logger.LogInformation("Creating AppChain (follower mode - applying genesis state before sync)...");
                await appChain.ApplyGenesisStateAsync(genesisOptions);
                logger.LogInformation("Genesis state applied (prefunded accounts, Create2Factory)");
            }

            // Only create sequencer config when not in follower mode
            SequencerConfig? sequencerConfig = null;
            IBatchStore? batchStore = null;

            if (!isFollower)
            {
                sequencerConfig = new SequencerConfig
                {
                    SequencerAddress = config.SequencerAddress!,
                    SequencerPrivateKey = config.SequencerPrivateKey,
                    BlockTimeMs = config.BlockTimeMs,
                    AllowEmptyBlocks = config.AllowEmptyBlocks,
                    MaxTransactionsPerBlock = 1000,
                    Policy = PolicyConfig.OpenAccess,
                    BatchProduction = new BatchProductionConfig
                    {
                        Enabled = config.AutoCreateBatches,
                        BatchCadence = config.BatchSize,
                        BatchOutputDirectory = config.BatchOutputDirectory ?? "./batches",
                        CompressBatches = true,
                        TriggerAnchorOnBatch = !string.IsNullOrEmpty(config.L1RpcUrl)
                    }
                };

                if (config.AutoCreateBatches)
                {
                    batchStore = new InMemoryBatchStore();
                }
            }

            ISequencer? sequencer = null;
            IP2PTransport? p2pTransport = null;
            CliqueEngine? cliqueEngine = null;
            CliqueBlockProductionStrategy? cliqueStrategy = null;
            MultiPeerSyncService? multiPeerSync = null;
            CoordinatedSyncService? coordinatedSync = null;
            PeerManager? peerManager = null;

            var sharedStateRootCalculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);

            var chainIdStr = config.ChainId.ToString();
            var metricsName = config.ChainName ?? "Nethereum";
            var blockProductionMetrics = new BlockProductionMetrics(chainIdStr, metricsName);
            var txPoolMetrics = new TxPoolMetrics(chainIdStr, metricsName);
            var rpcMetrics = new RpcMetrics(chainIdStr, metricsName);
            var storageMetrics = new StorageMetrics(chainIdStr, metricsName);
            var syncMetrics = new SyncMetrics(chainIdStr, metricsName);
            var sequencerMetrics = new SequencerMetrics(chainIdStr, metricsName);
            var haMetrics = new HAMetrics(chainIdStr, metricsName);
            var anchoringMetrics = new AnchoringMetrics(chainIdStr, metricsName);

            // Create message processing infrastructure (before sequencer so it can be injected)
            IMessageResultStore messageResultStore = rocksDbManager != null
                ? new RocksDbMessageResultStore(rocksDbManager)
                : new InMemoryMessageResultStore();
            var messageAccumulator = new MessageMerkleAccumulator();

            var rebuildCount = await messageAccumulator.RebuildFromStoreAsync(messageResultStore);
            if (rebuildCount > 0)
            {
                logger.LogInformation("Rebuilt message accumulator from store: {Count} leaves across all chains", rebuildCount);
            }

            IMessageQueue? messageQueue = null;
            IMessageProcessor? messageProcessor = null;
            if (config.EnableMessaging && !isFollower)
            {
                messageQueue = new MessageQueue();
                messageProcessor = new MessageProcessor(
                    messageAccumulator,
                    messageResultStore,
                    logger: loggerFactory.CreateLogger<MessageProcessor>());
                logger.LogInformation("Cross-chain messaging enabled (poll interval: {Ms}ms)", config.MessagePollIntervalMs);
            }

            if (config.ConsensusMode == "clique")
            {
                var syncMode = config.P2PMode != "none" ? $"P2P ({config.P2PMode})" : $"HTTP sync ({config.SyncPeers.Length} peers)";
                logger.LogInformation("Initializing Clique consensus with sync mode: {SyncMode}", syncMode);

                // Create P2P transport if configured
                if (config.P2PMode == "dotnetty")
                {
                    var dotNettyConfig = new DotNettyConfig
                    {
                        ListenAddress = config.P2PListenAddress,
                        ListenPort = config.P2PPort,
                        BootstrapNodes = config.BootstrapNodes.ToList(),
                        NodePrivateKey = config.SignerPrivateKey,
                        ChainId = (int)config.ChainId
                    };
                    p2pTransport = new DotNettyTransport(dotNettyConfig, loggerFactory.CreateLogger<DotNettyTransport>());
                    await p2pTransport.StartAsync();
                    logger.LogInformation("DotNetty P2P transport started on {Address}:{Port}", config.P2PListenAddress, config.P2PPort);
                }

                // Create Clique configuration
                var cliqueConfig = new CliqueConfig
                {
                    BlockPeriodSeconds = config.CliquePeriod,
                    EpochLength = config.CliqueEpoch,
                    InitialSigners = config.InitialSigners.ToList(),
                    LocalSignerAddress = config.SignerAddress!,
                    LocalSignerPrivateKey = config.SignerPrivateKey,
                    AllowEmptyBlocks = config.AllowEmptyBlocks,
                    EnableVoting = true,
                    WiggleTimeMs = 500
                };

                // Create Clique engine and strategy
                cliqueEngine = new CliqueEngine(cliqueConfig, loggerFactory.CreateLogger<CliqueEngine>());
                cliqueEngine.ApplyGenesisSigners(config.InitialSigners.ToList());

                cliqueStrategy = new CliqueBlockProductionStrategy(
                    appChainConfig,
                    cliqueEngine,
                    loggerFactory.CreateLogger<CliqueBlockProductionStrategy>());

                // Create sequencer with Clique strategy
                var nodeId = $"Node-{config.SignerAddress?.Substring(0, 10)}";
                sequencer = new Nethereum.AppChain.Sequencer.Sequencer(
                    appChain,
                    sequencerConfig,
                    blockProductionStrategy: cliqueStrategy,
                    messageQueue: messageQueue,
                    messageProcessor: messageProcessor,
                    logger: loggerFactory.CreateLogger<Nethereum.AppChain.Sequencer.Sequencer>(),
                    nodeId: nodeId,
                    stateRootCalculator: sharedStateRootCalculator);
                sequencer = new InstrumentedSequencer(sequencer, blockProductionMetrics, txPoolMetrics, sequencerMetrics);

                // Create P2P block handling components if P2P is enabled
                if (p2pTransport != null)
                {
                    var p2pBlockHandler = new P2PBlockHandler(
                        blockStore,
                        cliqueEngine,
                        loggerFactory.CreateLogger<P2PBlockHandler>());
                    var p2pBroadcaster = new P2PBlockBroadcaster(
                        p2pTransport,
                        loggerFactory.CreateLogger<P2PBlockBroadcaster>());
                    var p2pDispatcher = new P2PMessageDispatcher(
                        p2pTransport,
                        p2pBlockHandler,
                        loggerFactory.CreateLogger<P2PMessageDispatcher>());

                    p2pBlockHandler.BlockImported += (sender, e) =>
                    {
                        logger.LogInformation("[P2P] Imported block {BlockNumber} from peer {PeerId}",
                            e.Header.BlockNumber, e.FromPeerId?.Substring(0, 10) ?? "unknown");
                    };

                    p2pBlockHandler.BlockRejected += (sender, e) =>
                    {
                        logger.LogWarning("[P2P] Rejected block from peer {PeerId}: {Reason} - {Error}",
                            e.FromPeerId?.Substring(0, 10) ?? "unknown", e.Reason, e.Error);
                    };

                    cliqueStrategy.BlockFinalized += async (sender, e) =>
                    {
                        var txHashes = e.Result.TransactionResults
                            .Where(tx => tx.TxHash != null)
                            .Select(tx => tx.TxHash!)
                            .ToArray();

                        await p2pBroadcaster.BroadcastBlockAsync(e.Header, txHashes);
                    };
                }

                logger.LogInformation("Clique consensus initialized with {Count} initial signers", config.InitialSigners.Length);
                foreach (var signer in config.InitialSigners)
                {
                    logger.LogInformation("  Signer: {Address}", signer);
                }
            }
            else if (!isFollower)
            {
                // Default single-sequencer mode (only when not a follower)
                var nodeId = $"Node-{config.SequencerAddress?.Substring(0, 10)}";
                var singleSequencer = new Nethereum.AppChain.Sequencer.Sequencer(
                    appChain,
                    sequencerConfig!,
                    batchStore: batchStore,
                    messageQueue: messageQueue,
                    messageProcessor: messageProcessor,
                    logger: loggerFactory.CreateLogger<Nethereum.AppChain.Sequencer.Sequencer>(),
                    nodeId: nodeId,
                    stateRootCalculator: sharedStateRootCalculator);

                if (config.AutoCreateBatches)
                {
                    singleSequencer.BatchProduced += (sender, result) =>
                    {
                        if (result.Success)
                        {
                            logger.LogInformation("Batch produced: blocks {From}-{To}, file: {Path}",
                                result.BatchInfo?.FromBlock, result.BatchInfo?.ToBlock, result.FilePath);
                        }
                    };
                }

                sequencer = new InstrumentedSequencer(singleSequencer, blockProductionMetrics, txPoolMetrics, sequencerMetrics);
            }
            // Follower mode: no sequencer - blocks come from sync peers

            // Setup HTTP sync if sync peers are configured
            var finalityTracker = new InMemoryFinalityTracker();
            IBlockReExecutor? blockReExecutor = null;
            if (config.SyncPeers.Length > 0)
            {
                logger.LogInformation("Initializing HTTP sync with {Count} peers (state sync: {StateSync})",
                    config.SyncPeers.Length, config.EnableStateSync ? "enabled" : "disabled");
                foreach (var peer in config.SyncPeers)
                {
                    logger.LogInformation("  Sync peer: {Url}", peer);
                }

                peerManager = new PeerManager(clientFactory: url => new HttpSequencerRpcClient(url));
                foreach (var peerUrl in config.SyncPeers)
                {
                    peerManager.AddPeer(peerUrl);
                }

                // Create BlockReExecutor for state sync if enabled
                if (config.EnableStateSync)
                {
                    var chainConfig = new CoreChain.ChainConfig
                    {
                        ChainId = config.ChainId,
                        BaseFee = System.Numerics.BigInteger.Zero,
                        Coinbase = config.SequencerAddress ?? ""
                    };

                    var txVerifier = new TransactionVerificationAndRecoveryImp();
                    var transactionProcessor = new CoreChain.TransactionProcessor(
                        stateStore,
                        blockStore,
                        chainConfig,
                        txVerifier,
                        chainConfig.GetHardforkConfig());

                    blockReExecutor = new BlockReExecutor(
                        transactionProcessor,
                        stateStore,
                        chainConfig,
                        loggerFactory.CreateLogger<BlockReExecutor>(),
                        sharedStateRootCalculator);

                    logger.LogInformation("State sync enabled - transactions will be re-executed to build local state");
                }

                var syncConfig = new MultiPeerSyncConfig
                {
                    PollIntervalMs = config.SyncPollIntervalMs,
                    AutoFollow = true,  // Continuously poll for new blocks
                    RejectOnStateRootMismatch = config.EnableStateSync  // Validate state roots when state sync is on
                };

                multiPeerSync = new MultiPeerSyncService(
                    syncConfig,
                    blockStore,
                    transactionStore,
                    receiptStore,
                    logStore,
                    finalityTracker,
                    peerManager,
                    blockReExecutor,
                    loggerFactory.CreateLogger<MultiPeerSyncService>());

                multiPeerSync.BlockImported += (s, e) =>
                {
                    logger.LogInformation("[Sync] Imported block {BlockNumber} ({TxCount} txs)",
                        e.BlockNumber, e.TransactionCount);
                };

                multiPeerSync.PeerSwitched += (s, e) =>
                {
                    if (e.PreviousPeerUrl != null)
                    {
                        logger.LogInformation("[Sync] Switched from {OldPeer} to {NewPeer}: {Reason}",
                            e.PreviousPeerUrl, e.NewPeerUrl, e.Reason);
                    }
                    else
                    {
                        logger.LogInformation("[Sync] Connected to {Peer}", e.NewPeerUrl);
                    }
                };

                multiPeerSync.Error += (s, e) =>
                {
                    if (e.Recoverable)
                    {
                        logger.LogWarning("[Sync] Recoverable error: {Message}", e.Message);
                    }
                    else
                    {
                        logger.LogError(e.Exception, "[Sync] Error: {Message}", e.Message);
                    }
                };

                if (config.EnableStateSync)
                {
                    multiPeerSync.StateRootMismatch += (s, e) =>
                    {
                        logger.LogWarning("[Sync] State root mismatch at block {BlockNumber}: expected={Expected}, computed={Computed}",
                            e.BlockNumber,
                            e.ExpectedStateRoot != null ? BitConverter.ToString(e.ExpectedStateRoot).Replace("-", "").ToLowerInvariant() : "null",
                            e.ComputedStateRoot != null ? BitConverter.ToString(e.ComputedStateRoot).Replace("-", "").ToLowerInvariant() : "null");
                    };
                }

                // Create CoordinatedSyncService wrapping MultiPeerSyncService
                var anchorConfig = new AnchorConfig { Enabled = false };
                var anchorService = new EvmAnchorService(anchorConfig);
                var syncBatchStore = new InMemoryBatchStore();
                var batchImporter = new BatchImporter(blockStore, transactionStore, receiptStore, logStore, syncBatchStore);
                var batchSyncConfig = new BatchSyncConfig
                {
                    ChainId = config.ChainId,
                    SequencerUrl = config.SyncPeers.FirstOrDefault()
                };
                var batchSyncService = new BatchSyncService(batchSyncConfig, syncBatchStore, batchImporter, anchorService);

                coordinatedSync = new CoordinatedSyncService(
                    CoordinatedSyncConfig.Default,
                    batchSyncService,
                    multiPeerSync,
                    finalityTracker,
                    anchorService,
                    syncBatchStore);

                coordinatedSync.SyncProgressChanged += (s, e) =>
                {
                    logger.LogInformation("[CoordinatedSync] {Mode}: {Message} (finalized={Finalized}, soft={Soft})",
                        e.Mode, e.Message, e.FinalizedTip, e.SoftTip);
                };

                // Start coordinated sync (batch phase is no-op without L1 anchoring, live phase syncs from peers)
                if (config.AutoSyncOnStart)
                {
                    logger.LogInformation("Starting coordinated sync (batch + live)...");
                    await coordinatedSync.StartAsync();
                    logger.LogInformation("Coordinated sync started - syncing from peers in background");
                }
            }

            var node = new AppChainNode(appChain, sequencer);

            // Start block production FIRST so MUD deployment transactions can be mined
            if (!isFollower && sequencer != null)
            {
                await sequencer.StartAsync();
                logger.LogInformation("Block production started (interval: {Ms}ms)", config.BlockTimeMs);
            }
            else if (isFollower)
            {
                logger.LogInformation("Follower mode - block production disabled, syncing from peers");
            }

            // Deploy MUD World AFTER block production is running
            Nethereum.AppChain.MudGenesisResult? mudResult = null;
            if (config.DeployMudWorld)
            {
                logger.LogInformation("Deploying MUD World contracts...");
                var mudDeployer = new MudWorldDeployer(logger);
                mudResult = await mudDeployer.DeployMudWorldAsync(node, config.GenesisOwnerPrivateKey!, config.MudWorldSalt);

                PrintMudDeployment(mudResult, logger);
            }

            var builder = WebApplication.CreateBuilder();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.Logging.AddFilter("Nethereum", LogLevel.Information);
            builder.Services.AddAppChainServer(config);

            builder.Services.AddSingleton(blockProductionMetrics);
            builder.Services.AddSingleton(txPoolMetrics);
            builder.Services.AddSingleton(rpcMetrics);
            builder.Services.AddSingleton(storageMetrics);
            builder.Services.AddSingleton(syncMetrics);
            builder.Services.AddSingleton(sequencerMetrics);
            builder.Services.AddSingleton(haMetrics);
            builder.Services.AddSingleton(anchoringMetrics);
            builder.Services.AddSingleton(new MetricsConfig());

            builder.Services.AddAppChainOpenTelemetry(config);
            builder.Services.AddAppChainHealthChecks();

            builder.Services.AddSingleton(node);
            builder.Services.AddSingleton<IAppChain>(appChain);
            if (sequencer != null)
            {
                builder.Services.AddSingleton<ISequencer>(sequencer);
            }
            builder.Services.AddSingleton<IBlockStore>(blockStore);
            builder.Services.AddSingleton<ITransactionStore>(transactionStore);
            builder.Services.AddSingleton<IReceiptStore>(receiptStore);
            builder.Services.AddSingleton<ILogStore>(logStore);
            builder.Services.AddSingleton<IStateStore>(stateStore);
            builder.Services.AddSingleton<IFinalityTracker>(finalityTracker);
            if (batchStore != null)
            {
                builder.Services.AddSingleton<IBatchStore>(batchStore);
            }
            if (mudResult != null)
            {
                builder.Services.AddSingleton(mudResult);
            }
            if (rocksDbManager != null)
            {
                builder.Services.AddSingleton(rocksDbManager);
            }

            builder.Services.AddSingleton<IMessageResultStore>(messageResultStore);
            builder.Services.AddSingleton<IMessageMerkleAccumulator>(messageAccumulator);
            if (peerManager != null)
            {
                builder.Services.AddSingleton<IPeerManager>(peerManager);
            }
            if (multiPeerSync != null)
            {
                builder.Services.AddSingleton<ILiveBlockSync>(multiPeerSync);
            }
            if (coordinatedSync != null)
            {
                builder.Services.AddSingleton(coordinatedSync);
            }

            // Register hosted services for lifecycle management (stop order is reverse of registration)
            if (p2pTransport != null)
            {
                builder.Services.AddSingleton<IHostedService>(sp =>
                    new P2PHosting.P2PTransportHostedService(p2pTransport, alreadyStarted: true,
                        sp.GetService<ILoggerFactory>()?.CreateLogger<P2PHosting.P2PTransportHostedService>()));
            }
            if (sequencer != null)
            {
                builder.Services.AddSingleton<IHostedService>(sp =>
                    new SequencerHosting.SequencerHostedService(sequencer, alreadyStarted: true,
                        sp.GetService<ILoggerFactory>()?.CreateLogger<SequencerHosting.SequencerHostedService>()));
            }
            if (coordinatedSync != null)
            {
                builder.Services.AddSingleton<IHostedService>(sp =>
                    new SyncHosting.SyncHostedService(coordinatedSync, alreadyStarted: true,
                        sp.GetService<ILoggerFactory>()?.CreateLogger<SyncHosting.SyncHostedService>()));
            }
            builder.Services.AddSingleton<IHostedService>(sp =>
                new Server.Metrics.MetricsCollector(
                    blockProductionMetrics,
                    syncMetrics,
                    appChain,
                    sp.GetRequiredService<MetricsConfig>(),
                    txPoolMetrics,
                    sequencer?.TxPool,
                    sp.GetService<ILoggerFactory>()?.CreateLogger<Server.Metrics.MetricsCollector>()));
            if (rocksDbManager != null)
            {
                builder.Services.AddSingleton<IHostedService>(sp =>
                    new RocksDbHosting.RocksDbLifetimeService(rocksDbManager,
                        sp.GetService<ILoggerFactory>()?.CreateLogger<RocksDbHosting.RocksDbLifetimeService>()));
            }

            // Wire cross-chain messaging workers
            if (config.EnableMessaging && !isFollower)
            {
                var messagingConfig = new MessagingConfig
                {
                    Enabled = true,
                    PollIntervalMs = config.MessagePollIntervalMs,
                    MaxMessagesPerPoll = config.MaxMessagesPerPoll
                };

                foreach (var entry in config.HubSourceChains)
                {
                    // Format: "chainId|rpcUrl|hubAddress" or "chainId:rpcUrl:hubAddress"
                    // Use pipe separator first; fall back to splitting on last two colons if no pipes
                    string[] parts;
                    if (entry.Contains('|'))
                    {
                        parts = entry.Split('|');
                    }
                    else
                    {
                        // Split by last occurrence: chainId then rpcUrl (may contain ://) then hubAddress (0x...)
                        var lastColon = entry.LastIndexOf(":0x", StringComparison.OrdinalIgnoreCase);
                        if (lastColon < 0) lastColon = entry.LastIndexOf(':');
                        var firstColon = entry.IndexOf(':');
                        if (lastColon > firstColon && firstColon > 0)
                        {
                            parts = new[]
                            {
                                entry.Substring(0, firstColon),
                                entry.Substring(firstColon + 1, lastColon - firstColon - 1),
                                entry.Substring(lastColon + 1)
                            };
                        }
                        else
                        {
                            parts = entry.Split(':');
                        }
                    }

                    if (parts.Length >= 3 && ulong.TryParse(parts[0], out var srcChainId))
                    {
                        messagingConfig.SourceChains.Add(new SourceChainConfig
                        {
                            ChainId = srcChainId,
                            RpcUrl = parts[1],
                            HubContractAddress = parts[2]
                        });
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

                    builder.Services.AddSingleton<IHostedService>(logProcessingWorker);

                    var messagingService = new MessagingService(
                        (ulong)config.ChainId,
                        messagingConfig,
                        messageIndexStore,
                        messageQueue,
                        loggerFactory.CreateLogger<MessagingService>(),
                        messageAccumulator);

                    var messagingWorker = new MessagingWorker(
                        messagingService,
                        messagingConfig,
                        loggerFactory.CreateLogger<MessagingWorker>());

                    builder.Services.AddSingleton<IHostedService>(messagingWorker);
                    logger.LogInformation("Messaging worker registered for {Count} source chains with log processing", messagingConfig.SourceChains.Count);

                    if (config.EnableMessageAcknowledgment && !string.IsNullOrEmpty(config.SequencerPrivateKey))
                    {
                        var ackServices = new Dictionary<ulong, IMessageAcknowledgmentService>();
                        foreach (var source in messagingConfig.SourceChains)
                        {
                            ackServices[source.ChainId] = new HubMessageAcknowledgmentService(
                                (ulong)config.ChainId,
                                source.RpcUrl,
                                source.HubContractAddress,
                                config.SequencerPrivateKey,
                                source.ChainId,
                                loggerFactory.CreateLogger<HubMessageAcknowledgmentService>());
                        }

                        var ackConfig = new MessageAcknowledgmentConfig
                        {
                            Enabled = true,
                            IntervalMs = config.AcknowledgmentIntervalMs,
                            MaxRetries = 3,
                            RetryDelayMs = 2000
                        };

                        var ackWorker = new MessageAcknowledgmentWorker(
                            messageAccumulator,
                            ackServices,
                            ackConfig,
                            loggerFactory.CreateLogger<MessageAcknowledgmentWorker>());

                        builder.Services.AddSingleton<IHostedService>(ackWorker);
                        logger.LogInformation("Acknowledgment worker registered for {Count} source chains (interval: {Ms}ms)",
                            ackServices.Count, config.AcknowledgmentIntervalMs);
                    }
                }
            }

            var app = builder.Build();

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
            if (peerManager != null)
            {
                rpcRegistry.AddAdminHandlers();
            }

            var rpcServices = app.Services;
            var rpcContext = new RpcContext(node, (long)config.ChainId, rpcServices);
            if (sequencer != null)
            {
                rpcContext.TxPool = sequencer.TxPool;
            }
            var rpcDispatcher = new InstrumentedRpcDispatcher(rpcRegistry, rpcContext, rpcMetrics, logger, serializerOptions);

            var subscriptionManager = new SubscriptionManager();
            var wsHandler = new WebSocketRpcHandler(subscriptionManager, rpcRegistry, rpcContext, serializerOptions);

            if (sequencer != null)
            {
                sequencer.BlockProduced += async (sender, result) =>
                {
                    try
                    {
                        var blockLogs = await logStore.GetLogsByBlockNumberAsync(result.Header.BlockNumber);
                        await wsHandler.BroadcastBlockAsync(result.Header, result.BlockHash, blockLogs);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to broadcast WebSocket notifications for block {BlockNumber}", result.Header.BlockNumber);
                    }
                };
            }

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

            app.MapWebSocketEndpoint(wsHandler);

            app.MapHealthChecks("/health");

            app.MapBatchSyncEndpoints();
            app.MapLiveBlockEndpoints();

            app.MapGet("/status", async () =>
            {
                var status = new AppChainStatus
                {
                    ChainId = (long)config.ChainId,
                    ChainName = config.ChainName,
                    BlockNumber = (long)await appChain.GetBlockNumberAsync(),
                    RpcUrl = config.RpcUrl,
                    ConsensusMode = config.ConsensusMode,
                    P2PMode = config.P2PMode,
                    Accounts = new AccountsStatus
                    {
                        GenesisOwner = config.GenesisOwnerAddress!,
                        Sequencer = config.SequencerAddress!
                    },
                    Contracts = mudResult != null ? new ContractsStatus
                    {
                        Create2Factory = mudResult.Create2FactoryAddress,
                        WorldFactory = mudResult.WorldFactoryAddress,
                        World = mudResult.WorldAddress,
                        InitModule = mudResult.InitModuleAddress,
                        AccessManagementSystem = mudResult.AccessManagementSystemAddress,
                        BalanceTransferSystem = mudResult.BalanceTransferSystemAddress,
                        BatchCallSystem = mudResult.BatchCallSystemAddress,
                        RegistrationSystem = mudResult.RegistrationSystemAddress
                    } : null,
                    Anchoring = !string.IsNullOrEmpty(config.L1RpcUrl) ? new AnchoringStatus
                    {
                        Enabled = true,
                        L1RpcUrl = config.L1RpcUrl,
                        AnchorContract = config.AnchorContractAddress,
                        LastAnchoredBlock = 0
                    } : null,
                    P2P = config.P2PMode != "none" ? new P2PStatus
                    {
                        Enabled = true,
                        Mode = config.P2PMode,
                        Port = config.P2PPort,
                        ConnectedPeers = p2pTransport?.ConnectedPeers?.Count ?? 0,
                        BootstrapNodes = config.BootstrapNodes
                    } : null,
                    HttpSync = config.SyncPeers.Length > 0 ? new HttpSyncStatus
                    {
                        Enabled = true,
                        PeerCount = peerManager?.Peers.Count ?? 0,
                        HealthyPeerCount = peerManager?.Peers.Count(p => p.IsHealthy) ?? 0,
                        LocalTip = (long)(multiPeerSync?.LocalTip ?? -1),
                        RemoteTip = (long)(multiPeerSync?.RemoteTip ?? -1),
                        CurrentPeer = multiPeerSync?.CurrentPeerUrl,
                        SyncPeers = config.SyncPeers
                    } : null
                };

                return Results.Ok(status);
            });

            logger.LogInformation("Starting HTTP server at {Url}", config.RpcUrl);
            PrintReadyBanner(config, mudResult, logger);

            await app.RunAsync($"http://{config.Host}:{config.Port}");
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
            Console.WriteLine($"║ Consensus:          {config.ConsensusMode,-53} ║");
            Console.WriteLine($"║ P2P:                {(config.P2PMode == "none" ? "disabled" : $"{config.P2PMode} (port {config.P2PPort})"),-53} ║");
            Console.WriteLine($"║ HTTP Sync:          {(config.SyncPeers.Length == 0 ? "disabled" : $"{config.SyncPeers.Length} peers"),-53} ║");
            Console.WriteLine($"║ Storage:            {(config.UseInMemoryStorage ? "In-Memory" : $"RocksDB ({config.DatabasePath})"),-53} ║");
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
        public AccountsStatus? Accounts { get; set; }
        public ContractsStatus? Contracts { get; set; }
        public AnchoringStatus? Anchoring { get; set; }
        public P2PStatus? P2P { get; set; }
        public HttpSyncStatus? HttpSync { get; set; }
    }

    public class P2PStatus
    {
        public bool Enabled { get; set; }
        public string Mode { get; set; } = "";
        public int Port { get; set; }
        public int ConnectedPeers { get; set; }
        public string[] BootstrapNodes { get; set; } = Array.Empty<string>();
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

    public class HttpSyncStatus
    {
        public bool Enabled { get; set; }
        public int PeerCount { get; set; }
        public int HealthyPeerCount { get; set; }
        public long LocalTip { get; set; }
        public long RemoteTip { get; set; }
        public string? CurrentPeer { get; set; }
        public string[] SyncPeers { get; set; } = Array.Empty<string>();
    }
}
