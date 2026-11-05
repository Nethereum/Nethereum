using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Consensus.Clique;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;

namespace Nethereum.AppChain.Server.Hosting
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ChainInfrastructure, "appchain-server-core", "AppChainExecutionCore")]
    public sealed class AppChainExecutionCore
    {
        private readonly AppChainConfig _chainConfig;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ExecutionStack _stack;

        public AppChainExecutionCore(
            AppChainConfig chainConfig,
            IChainStoreBundle bundle,
            CliqueEngine? clique,
            ILoggerFactory loggerFactory,
            IFlushCadence? flushCadence = null)
        {
            _chainConfig = chainConfig ?? throw new ArgumentNullException(nameof(chainConfig));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

            _stack = ExecutionStackFactory.Build(
                bundle, chainConfig.ResolveActivations(),
                chainConfigFactory: _ => chainConfig,
                hardforkConfigFactory: chainConfig.ConfigForFork,
                options: new ExecutionStackOptions
                {
                    RewardPolicy = NoRewardPolicy.Instance,
                    AuthorResolver = clique != null ? clique.AuthorOf : null,
                    SealedByClique = clique != null,
                    FeeFreeBaseFee = chainConfig.FeeFreeBaseFee,
                    SideChain = new SideChainOptions { PersistMetadata = false, AcknowledgeKnownCanonicalBlocks = false },
                    FlushCadence = flushCadence,
                    LoggerFactory = loggerFactory
                });

            Clique = clique;
            CliqueGate = clique != null
                ? new CliqueConsensusBlockGate(clique, SideChain.Blocks, loggerFactory.CreateLogger<CliqueConsensusBlockGate>())
                : null;
        }

        public static AppChainConfig ChainConfigFor(Configuration.AppChainServerConfig config)
        {
            var chainConfig = AppChainConfig.CreateWithName(config.ChainName, config.ChainId);
            chainConfig.ForkSchedule = config.ForkSchedule;
            if (config.Registry != null) chainConfig.Registry = config.Registry;
            chainConfig.SequencerAddress = config.Consensus.Sequencer.Address;
            ChainNode.Hosting.Configuration.ChainNodeProducerConfigExtensions.ApplyTo(config.Node.Producer, chainConfig);
            ChainNode.Hosting.Configuration.ChainNodeRpcConfigExtensions.ApplyTo(config.Node.Rpc, chainConfig);
            return chainConfig;
        }

        public static IFlushCadence? FlushCadenceFor(Configuration.AppChainServerConfig config) =>
            FlushCadences.ForBlocks(config.Node.Storage.FlushCadenceBlocks, "Node.Storage.FlushCadenceBlocks");

        public Nethereum.EVM.IChainActivations Activations => _chainConfig.ResolveActivations();

        public IncrementalStateRootCalculator StateRoot => (IncrementalStateRootCalculator)_stack.Calculator;

        public BlockCommit Commit => _stack.Commit;

        public BlockExecutor Executor => _stack.Executor;

        public SideChainSupport SideChain => _stack.SideChain!;

        public CliqueEngine? Clique { get; }

        public CliqueConsensusBlockGate? CliqueGate { get; }

        public IBlockExecutor CreateImporter()
        {
            var importer = _stack.NewImporter();

            return CliqueGate == null
                ? importer
                : new ConsensusGatedBlockExecutor(
                    importer, CliqueGate, _loggerFactory.CreateLogger<ConsensusGatedBlockExecutor>());
        }

        public async Task ChainRewrittenAsync(CancellationToken ct)
        {
            StateRoot.DiscardPendingState();

            if (CliqueGate != null)
                await CliqueGate.RestoreHeadSnapshotAsync(ct).ConfigureAwait(false);
        }
    }
}
