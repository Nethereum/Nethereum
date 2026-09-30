using System.Numerics;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain;
using Nethereum.AppChain.Genesis;
using Nethereum.AppChain.Sequencer;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.Signer;
using Nethereum.AppChain.Server.Anchoring;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.AppChain.Anchoring.Metrics;
using Nethereum.AppChain.Sequencer.Metrics;
using Nethereum.AppChain.Server.Metrics;
using Nethereum.CoreChain.Metrics;
using Nethereum.AppChain.Anchoring.Messaging;
using Nethereum.Consensus.Clique;
using Nethereum.Model;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.AppChain.Sequencer.ProducerAuthority;

namespace Nethereum.AppChain.Server
{
    public static class AppChainComposition
    {
        public static async Task<AppChainComposedNode> ComposeAsync(
            AppChainServerConfig config, ILoggerFactory loggerFactory, CancellationToken ct)
        {
            var logger = loggerFactory.CreateLogger("Nethereum.AppChain.Server");

            if (config.IsFullyUnconfigured)
                ApplyEphemeralDevKeys(config, logger);

            config.DeriveAddresses();
            config.Validate();

            EthECKey.SignRecoverable = true;

            var appChainConfig = AppChainConfig.CreateWithName(config.ChainName, config.ChainId);
            appChainConfig.ForkSchedule = config.ForkSchedule;
            appChainConfig.SequencerAddress = config.Consensus.Sequencer.Address;
            if (config.BaseFee.HasValue) appChainConfig.BaseFee = config.BaseFee.Value;
            config.Node.Rpc.ApplyTo(appChainConfig);

            Nethereum.EVM.ChainActivationsRegistry.Instance.Register(
                (long)config.ChainId, config.ForkSchedule.ResolveActivations());

            var imports = config.ImportsBlocks;
            var produces = config.ProducesBlocks;

            config.Node.AsRole(produces ? ChainNodeRole.Signer : ChainNodeRole.Follower);

            var chainNode = await Nethereum.ChainNode.Hosting.ChainNode.StartAsync(
                new AppChainDefinition(
                    config,
                    (bundle, _) => WriteGenesisAsync(appChainConfig, bundle, config, fullGenesis: true, logger)),
                config.Node,
                loggerFactory,
                ct: ct);

            var bundle = chainNode.Bundle;
            var appChain = new Nethereum.AppChain.AppChain(
                appChainConfig, bundle.Blocks, bundle.Transactions, bundle.Receipts,
                bundle.Logs, bundle.State, bundle.TrieNodes);

            var sequencerConfig = produces ? BuildSequencerConfig(config) : null;
            var sharedStateRootCalculator = new IncrementalStateRootCalculator(bundle.State, bundle.TrieNodes);
            var metrics = AppChainMetricsBundle.Create(config);

            var messaging = await ComposeMessagingAsync(config, chainNode, metrics, loggerFactory, logger);

            var (sequencer, cliqueEngine, cliqueStrategy, producerAuthority, producerAuthorityNodeId, forkChoice) =
                ComposeConsensus(
                    config, appChain, chainNode, sequencerConfig, sharedStateRootCalculator, metrics,
                    messaging.Queue, messaging.Processor, bundle, loggerFactory, logger);

            if (imports)
            {
                logger.LogInformation("Importing blocks from peers over DevP2P");
                var mempoolReconciler = chainNode.Mempool != null
                    ? new Nethereum.DevP2P.Sync.Mempool.MempoolReorgReconciler(
                        chainNode.Mempool.TxPool, chainNode.Mempool.Relay,
                        loggerFactory.CreateLogger<Nethereum.DevP2P.Sync.Mempool.MempoolReorgReconciler>())
                    : null;
                _ = Task.Run(() => AppChainDevP2PFollower.RunAsync(
                    config, chainNode, loggerFactory, CancellationToken.None, cliqueEngine, forkChoice, mempoolReconciler));
            }

            var node = new AppChainNode(
                appChain, sequencer, filterStore: null, blockAccessListStore: bundle?.BlockAccessLists);

            if (sequencer != null)
            {
                await sequencer.StartAsync();
                logger.LogInformation("Block production started (interval: {Ms}ms)", config.Consensus.BlockTimeMs);
            }
            else
            {
                logger.LogInformation("No signing key - this node imports blocks and produces none");
            }

            var mudResult = config.Mud.DeployWorld
                ? await DeployMudWorldAsync(config, node, logger)
                : null;

            WireProducedBlockPublisher(chainNode, sequencer, loggerFactory);

            return new AppChainComposedNode
            {
                ChainNode = chainNode,
                AppChain = appChain,
                Node = node,
                SequencerConfig = sequencerConfig,
                Sequencer = sequencer,
                CliqueEngine = cliqueEngine,
                CliqueStrategy = cliqueStrategy,
                StateRootCalculator = sharedStateRootCalculator,
                FinalityTracker = new InMemoryFinalityTracker(),
                MessageResultStore = messaging.ResultStore,
                MessageAccumulator = messaging.Accumulator,
                MessageQueue = messaging.Queue,
                MessageProcessor = messaging.Processor,
                MudResult = mudResult,
                Imports = imports,
                Produces = produces,
                Metrics = metrics,
                ProducerAuthority = producerAuthority,
                ProducerAuthorityNodeId = producerAuthorityNodeId,
                ForkChoice = forkChoice,
            };
        }

        private static void ApplyEphemeralDevKeys(AppChainServerConfig config, ILogger logger)
        {
            config.Genesis.Owner.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
            config.Consensus.Sequencer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();

            logger.LogInformation("AppChain dev mode: generated ephemeral keys (NOT for production)");
        }

        private static SequencerConfig BuildSequencerConfig(AppChainServerConfig config) => new SequencerConfig
        {
            SequencerAddress = config.Consensus.Sequencer.Address!,
            SequencerPrivateKey = config.Consensus.Sequencer.PrivateKey,
            BlockTimeMs = config.Consensus.BlockTimeMs,
            AllowEmptyBlocks = config.Consensus.AllowEmptyBlocks,
            BlockProductionMode = config.Consensus.BlockProductionMode,
            MaxTransactionsPerBlock = 1000,
            Policy = config.Consensus.Policy,
        };

        private static async Task<AppChainMessagingComposition> ComposeMessagingAsync(
            AppChainServerConfig config,
            Nethereum.ChainNode.Hosting.ChainNode chainNode,
            AppChainMetricsBundle metrics,
            ILoggerFactory loggerFactory,
            ILogger logger)
        {
            var rocksDbManager = chainNode.Storage.Manager;
            IMessageResultStore messageResultStore = rocksDbManager != null
                ? new RocksDbMessageResultStore(rocksDbManager)
                : new InMemoryMessageResultStore();
            var messageAccumulator = new MessageMerkleAccumulator();

            var rebuildCount = await messageAccumulator.RebuildFromStoreAsync(messageResultStore);
            if (rebuildCount > 0)
            {
                logger.LogInformation(
                    "Rebuilt message accumulator from store: {Count} leaves across all chains", rebuildCount);
            }

            IMessageQueue? messageQueue = null;
            IMessageProcessor? messageProcessor = null;
            if (config.Messaging.Enabled && config.ProducesBlocks)
            {
                messageQueue = new MessageQueue();
                messageProcessor = new MessageProcessor(
                    messageAccumulator, messageResultStore, logger: loggerFactory.CreateLogger<MessageProcessor>());
                logger.LogInformation(
                    "Cross-chain messaging enabled (poll interval: {Ms}ms)", config.Messaging.PollIntervalMs);
            }

            return new AppChainMessagingComposition(messageResultStore, messageAccumulator, messageQueue, messageProcessor);
        }

        private static (ISequencer? Sequencer, CliqueEngine? CliqueEngine, CliqueBlockProductionStrategy? CliqueStrategy,
            Nethereum.AppChain.Sequencer.ProducerAuthority.IProducerAuthority? ProducerAuthority, string? ProducerAuthorityNodeId,
            IChainForkChoice? ForkChoice)
            ComposeConsensus(
                AppChainServerConfig config,
                Nethereum.AppChain.AppChain appChain,
                Nethereum.ChainNode.Hosting.ChainNode chainNode,
                SequencerConfig? sequencerConfig,
                IncrementalStateRootCalculator stateRootCalculator,
                AppChainMetricsBundle metrics,
                IMessageQueue? messageQueue,
                IMessageProcessor? messageProcessor,
                IChainStoreBundle bundle,
                ILoggerFactory loggerFactory,
                ILogger logger)
        {
            var produces = config.ProducesBlocks;

            if (config.Consensus.Mode == AppChainConsensusMode.Clique)
            {
                var (cliqueSequencer, cliqueEngine, cliqueStrategy) = ComposeCliqueConsensus(
                    config, appChain, chainNode, sequencerConfig, stateRootCalculator, metrics,
                    messageQueue, messageProcessor, bundle, loggerFactory, logger, produces);
                IChainForkChoice cliqueForkChoice = new DifficultyForkChoice(bundle.Blocks);
                return (cliqueSequencer, cliqueEngine, cliqueStrategy, null, null, cliqueForkChoice);
            }

            if (!produces) return (null, null, null, null, null, null);

            var (producerAuthorityStrategy, producerAuthority, producerAuthorityNodeId) =
                BuildProducerAuthorityStrategy(config, loggerFactory);
            var nodeId = $"Node-{config.Consensus.Sequencer.Address?.Substring(0, 10)}";
            var singleSequencer = new Nethereum.AppChain.Sequencer.Sequencer(
                appChain,
                sequencerConfig!,
                txPool: chainNode.Mempool?.TxPool,
                blockProductionStrategy: producerAuthorityStrategy,
                messageQueue: messageQueue,
                messageProcessor: messageProcessor,
                logger: loggerFactory.CreateLogger<Nethereum.AppChain.Sequencer.Sequencer>(),
                nodeId: nodeId,
                stateRootCalculator: stateRootCalculator,
                blockAccessListStore: bundle?.BlockAccessLists);

            var sequencer = new InstrumentedSequencer(
                singleSequencer, metrics.BlockProduction, metrics.TxPool, metrics.Sequencer);

            IChainForkChoice? singleSequencerForkChoice = producerAuthority is ArbiterBackedProducerAuthority arbiterBacked
                ? new LeaseAuthorityForkChoice(arbiterBacked.Arbiter, bundle.Blocks)
                : null;

            return (sequencer, null, null, producerAuthority, producerAuthorityNodeId, singleSequencerForkChoice);
        }

        private static (ISequencer? Sequencer, CliqueEngine? CliqueEngine, CliqueBlockProductionStrategy? CliqueStrategy)
            ComposeCliqueConsensus(
                AppChainServerConfig config,
                Nethereum.AppChain.AppChain appChain,
                Nethereum.ChainNode.Hosting.ChainNode chainNode,
                SequencerConfig? sequencerConfig,
                IncrementalStateRootCalculator stateRootCalculator,
                AppChainMetricsBundle metrics,
                IMessageQueue? messageQueue,
                IMessageProcessor? messageProcessor,
                IChainStoreBundle bundle,
                ILoggerFactory loggerFactory,
                ILogger logger,
                bool produces)
        {
            var cliqueConfig = new CliqueConfig
            {
                BlockPeriodSeconds = config.Consensus.Clique.PeriodSeconds,
                EpochLength = config.Consensus.Clique.EpochLength,
                InitialSigners = config.Consensus.Clique.InitialSigners.ToList(),
                LocalSignerAddress = config.Consensus.Clique.Signer.Address!,
                LocalSignerPrivateKey = config.Consensus.Clique.Signer.PrivateKey,
                AllowEmptyBlocks = config.Consensus.AllowEmptyBlocks,
                EnableVoting = true,
                WiggleTimeMs = 500
            };

            var cliqueEngine = new CliqueEngine(cliqueConfig, loggerFactory.CreateLogger<CliqueEngine>());
            cliqueEngine.ApplyGenesisSigners(config.Consensus.Clique.InitialSigners.ToList());

            var cliqueProposalStore = new InMemoryCliqueProposalStore();
            var cliqueStrategy = new CliqueBlockProductionStrategy(
                appChain.Config, cliqueEngine, loggerFactory.CreateLogger<CliqueBlockProductionStrategy>(),
                cliqueProposalStore);

            ISequencer? sequencer = null;
            if (produces)
            {
                var nodeId = $"Node-{config.Consensus.Clique.Signer.Address?.Substring(0, 10)}";
                var cliqueSequencer = new Nethereum.AppChain.Sequencer.Sequencer(
                    appChain,
                    sequencerConfig!,
                    txPool: chainNode.Mempool?.TxPool,
                    blockProductionStrategy: cliqueStrategy,
                    messageQueue: messageQueue,
                    messageProcessor: messageProcessor,
                    logger: loggerFactory.CreateLogger<Nethereum.AppChain.Sequencer.Sequencer>(),
                    nodeId: nodeId,
                    stateRootCalculator: stateRootCalculator,
                    blockAccessListStore: bundle?.BlockAccessLists);
                sequencer = new InstrumentedSequencer(
                    cliqueSequencer, metrics.BlockProduction, metrics.TxPool, metrics.Sequencer);
            }

            logger.LogInformation(
                "Clique consensus initialized with {Count} initial signers", config.Consensus.Clique.InitialSigners.Length);
            foreach (var signer in config.Consensus.Clique.InitialSigners)
            {
                logger.LogInformation("  Signer: {Address}", signer);
            }

            return (sequencer, cliqueEngine, cliqueStrategy);
        }

        private static async Task<MudGenesisResult> DeployMudWorldAsync(
            AppChainServerConfig config, AppChainNode node, ILogger logger)
        {
            logger.LogInformation("Deploying MUD World contracts...");
            var mudDeployer = new MudWorldDeployer(logger);
            return await mudDeployer.DeployMudWorldAsync(node, config.Genesis.Owner.PrivateKey!, config.Mud.WorldSalt);
        }

        private static void WireProducedBlockPublisher(
            Nethereum.ChainNode.Hosting.ChainNode chainNode, ISequencer? sequencer, ILoggerFactory loggerFactory)
        {
            if (chainNode.Listener == null || sequencer == null || chainNode.Mempool == null) return;

            var blockPublisher = new DevP2PProducedBlockPublisher(
                chainNode.Mempool.BroadcastPool,
                chainNode.Bundle.Transactions,
                loggerFactory.CreateLogger<DevP2PProducedBlockPublisher>());
            sequencer.BlockProduced += blockPublisher.OnBlockProduced;
        }

        private static (Nethereum.CoreChain.Consensus.IBlockProductionStrategy? Strategy,
            Nethereum.AppChain.Sequencer.ProducerAuthority.IProducerAuthority? Authority, string? NodeId)
            BuildProducerAuthorityStrategy(AppChainServerConfig config, ILoggerFactory loggerFactory)
        {
            var overrideAuthority = config.Consensus.ProducerAuthorityOverride;
            var path = config.Consensus.ProducerAuthorityFile;
            if (overrideAuthority == null && string.IsNullOrWhiteSpace(path)) return (null, null, null);

            var identity = config.Consensus.NodeIdentity ?? config.Node.Network.NodeKeyHex;
            if (string.IsNullOrWhiteSpace(identity))
                throw new InvalidOperationException(
                    "A producer authority needs this node's identity: set Consensus.NodeIdentity");

            var authority = overrideAuthority
                ?? new Nethereum.AppChain.Sequencer.ProducerAuthority.FileProducerAuthority(path!);

            var strategy = new Nethereum.AppChain.Sequencer.ProducerAuthority.ProducerAuthorityBlockProductionStrategy(
                authority,
                identity!,
                new Nethereum.CoreChain.Consensus.DefaultBlockProductionStrategy(
                    new Nethereum.CoreChain.ChainConfig { ChainId = config.ChainId }),
                loggerFactory.CreateLogger<
                    Nethereum.AppChain.Sequencer.ProducerAuthority.ProducerAuthorityBlockProductionStrategy>());

            return (strategy, authority, identity);
        }

        private static async Task WriteGenesisAsync(
            AppChainConfig appChainConfig, IChainStoreBundle bundle, AppChainServerConfig config,
            bool fullGenesis, ILogger logger)
        {
            var appChain = new Nethereum.AppChain.AppChain(
                appChainConfig, bundle.Blocks, bundle.Transactions, bundle.Receipts,
                bundle.Logs, bundle.State, bundle.StateTrieNodes);

            var genesisOptions = new GenesisOptions
            {
                DeployCreate2Factory = config.Genesis.DeployCreate2Factory,
                PrefundedAddresses = new[] { config.Genesis.Owner.Address!, config.Consensus.Sequencer.Address! },
                PrefundBalance = BigInteger.Parse("1000000000000000000000")
            };

            if (fullGenesis)
            {
                logger.LogInformation("Creating AppChain genesis (block 0)...");
                await appChain.InitializeAsync(genesisOptions);
                logger.LogInformation("Genesis block created");
                logger.LogInformation("  Create2Factory: {Address}", Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS);
                logger.LogInformation("  Genesis Owner:  {Address}", config.Genesis.Owner.Address);
                logger.LogInformation("  Sequencer:      {Address}", config.Consensus.Sequencer.Address);
                return;
            }

            logger.LogInformation("Creating AppChain (follower mode - applying genesis state before sync)...");
            await appChain.ApplyGenesisStateAsync(genesisOptions);
            logger.LogInformation("Genesis state applied (prefunded accounts, Create2Factory)");
        }
    }
}
