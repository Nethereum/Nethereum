using Nethereum.Documentation;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Prefetch;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Signer;

namespace Nethereum.CoreChain
{
    public sealed class ExecutionCore
    {
        public ExecutionCore(BlockExecutor executor, IIncrementalStateRootCalculator calculator)
            : this(executor, calculator, null)
        {
        }

        internal ExecutionCore(BlockExecutor executor, IIncrementalStateRootCalculator calculator, StatePrefetcher? prefetcher)
        {
            Executor = executor;
            Calculator = calculator;
            Prefetcher = prefetcher;
        }

        internal StatePrefetcher? Prefetcher { get; }

        public BlockExecutor Executor { get; }

        public IIncrementalStateRootCalculator Calculator { get; }
    }

    public sealed class ExecutionStack : IDisposable
    {
        private readonly IChainStoreBundle _bundle;
        private readonly ExecutionStackOptions _options;
        private readonly IStateStore _state;
        private readonly StatePrefetcher? _prefetcher;

        internal ExecutionStack(
            IChainStoreBundle bundle,
            ExecutionStackOptions options,
            IStateStore state,
            ExecutionCore core,
            BlockCommit commit,
            SideChainSupport? sideChain)
        {
            _bundle = bundle;
            _options = options;
            _state = state;
            Executor = core.Executor;
            Calculator = core.Calculator;
            _prefetcher = core.Prefetcher;
            Commit = commit;
            SideChain = sideChain;
            Importer = NewImporter();
        }

        public BlockExecutor Executor { get; }

        public StatePrefetcher? Prefetcher => _prefetcher;

        public BlockImporter Importer { get; }

        public IIncrementalStateRootCalculator Calculator { get; }

        public BlockCommit Commit { get; }

        public SideChainSupport? SideChain { get; }

        public void Dispose()
        {
            if (_options.Prefetcher == null) _prefetcher?.Dispose();
            if (_options.StateRootCalculatorFactory == null) (Calculator as IDisposable)?.Dispose();
        }

        public BlockImporter NewImporter() =>
            ExecutionStackFactory.ImporterOver(_bundle, Executor, _options, _state, SideChain, Commit, _prefetcher);
    }

    public static class ExecutionStackFactory
    {
        [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "ExecutionStackFactory.Build - compose the executor, state root and importer for a node role")]
        public static ExecutionStack Build(
            IChainStoreBundle bundle,
            IChainActivations activations,
            Func<HardforkName, ChainConfig> chainConfigFactory,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            ExecutionStackOptions options)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var state = options.StateOverride ?? bundle.State;
            var core = BuildExecutor(
                state, bundle.Blocks, bundle.StateTrieNodes, bundle.TrieNodes, bundle.Uncles,
                activations, chainConfigFactory, hardforkConfigFactory, options);
            var commit = CreateCommit(bundle, options);
            var sideChain = CreateSideChain(bundle, options.SideChain);
            return new ExecutionStack(bundle, options, state, core, commit, sideChain);
        }

        public static ExecutionCore BuildExecutor(
            IStateStore state,
            IBlockStore blocks,
            ITrieNodeStore stateTrieNodes,
            ITrieNodeStore trieNodes,
            IUncleStore? uncles,
            IChainActivations activations,
            Func<HardforkName, ChainConfig> chainConfigFactory,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            ExecutionStackOptions options)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));
            if (activations == null) throw new ArgumentNullException(nameof(activations));
            if (chainConfigFactory == null) throw new ArgumentNullException(nameof(chainConfigFactory));
            if (hardforkConfigFactory == null) throw new ArgumentNullException(nameof(hardforkConfigFactory));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.RewardPolicy == null) throw new ArgumentException("RewardPolicy is required to build an executor.", nameof(options));
            if (options.StateRootWorkers < 1) throw new ArgumentOutOfRangeException(nameof(options), "StateRootWorkers must be >= 1 (1 = sequential).");

            Secp256k1Contexts.EnsureInitialised();

            var loggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            IIncrementalStateRootCalculator calculator = options.StateRootCalculatorFactory != null
                ? options.StateRootCalculatorFactory(state, stateTrieNodes)
                : new IncrementalStateRootCalculator(state, stateTrieNodes,
                    emitTombstones: !ReferenceEquals(stateTrieNodes, trieNodes),
                    stateRootWorkers: options.StateRootWorkers);
            var executor = new BlockExecutor(
                state, blocks, activations,
                chainConfigFactory: chainConfigFactory,
                hardforkConfigFactory: hardforkConfigFactory,
                stateRootCalculator: calculator,
                rewardPolicy: options.RewardPolicy,
                trieNodeStore: trieNodes,
                logger: loggerFactory.CreateLogger<BlockExecutor>(),
                blockRootsProvider: null,
                authorResolver: options.AuthorResolver,
                validation: new BlockValidationOptions
                {
                    SealedByClique = options.SealedByClique,
                    FeeFreeBaseFee = options.FeeFreeBaseFee,
                    UncleStore = uncles,
                    TransactionVerifier = options.TransactionVerifier
                });

            var prefetcher = CreatePrefetcher(state, blocks, activations, chainConfigFactory, hardforkConfigFactory, options);
            return new ExecutionCore(executor, calculator, prefetcher);
        }

        private static StatePrefetcher? CreatePrefetcher(
            IStateStore state,
            IBlockStore blocks,
            IChainActivations activations,
            Func<HardforkName, ChainConfig> chainConfigFactory,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            ExecutionStackOptions options)
        {
            if (options.Prefetcher != null) return options.Prefetcher;
            if (options.PrefetchWorkers < 0)
                throw new ArgumentOutOfRangeException(nameof(options), "PrefetchWorkers must be >= 0 (0 = off).");
            if (options.PrefetchWorkers == 0) return null;
            return new StatePrefetcher(
                state, blocks, activations, chainConfigFactory, hardforkConfigFactory,
                options.AuthorResolver, options.PrefetchWorkers, shutdownBound: null, verifier: options.TransactionVerifier);
        }

        public static BlockCommit CreateCommit(IChainStoreBundle bundle, ExecutionStackOptions options)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var loggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            return new BlockCommit(
                options.StateOverride ?? bundle.State,
                bundle.NodeCommitBlockSource,
                bundle as IAtomicBlockFlush,
                options.FlushCadence,
                loggerFactory.CreateLogger<BlockImporter>());
        }

        public static BlockImporter CreateImporter(
            IChainStoreBundle bundle,
            BlockExecutor executor,
            ExecutionStackOptions options)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (options == null) throw new ArgumentNullException(nameof(options));
            return ImporterOver(
                bundle, executor, options, options.StateOverride ?? bundle.State,
                CreateSideChain(bundle, options.SideChain), CreateCommit(bundle, options));
        }

        internal static BlockImporter ImporterOver(
            IChainStoreBundle bundle,
            BlockExecutor executor,
            ExecutionStackOptions options,
            IStateStore state,
            SideChainSupport? sideChain,
            BlockCommit commit,
            StatePrefetcher? prefetcher = null)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));

            var loggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            return new BlockImporter(
                executor, bundle.Blocks, state,
                bundle.Transactions, bundle.Receipts, bundle.Logs, bundle.Uncles,
                loggerFactory.CreateLogger<BlockImporter>(),
                null, null, null,
                bundle.BlockAccessLists,
                bundle.Withdrawals,
                sideChain,
                commit,
                prefetcher);
        }

        private static SideChainSupport? CreateSideChain(IChainStoreBundle bundle, SideChainOptions? options)
        {
            if (options == null) return null;
            return new SideChainSupport(
                bundle.Blocks,
                new RewindCoordinator(bundle),
                options.PersistMetadata ? bundle.Metadata : null,
                adoptByDifficulty: options.AdoptByDifficulty,
                acknowledgeKnownCanonicalBlocks: options.AcknowledgeKnownCanonicalBlocks);
        }
    }
}
