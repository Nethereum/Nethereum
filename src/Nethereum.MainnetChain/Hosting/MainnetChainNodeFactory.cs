using System;
using Nethereum.Documentation;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Genesis;
using Nethereum.CoreChain.Prefetch;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.MainnetChain.Hosting
{
    public sealed class MainnetChainNodeFactory
    {
        private readonly IConsensusBlockGate _gate;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IFollowerService? _follower;
        private readonly IFlushCadence? _flushCadence;
        private readonly int _prefetchWorkers;
        private readonly int _stateRootWorkers;
        private readonly StandardGenesisDocument? _genesis;

        public MainnetChainNodeFactory(
            IConsensusBlockGate gate,
            ILoggerFactory? loggerFactory = null,
            IFollowerService? follower = null,
            IFlushCadence? flushCadence = null)
            : this(gate, loggerFactory, follower, flushCadence, prefetchWorkers: 0)
        {
        }

        public MainnetChainNodeFactory(
            IConsensusBlockGate gate,
            ILoggerFactory? loggerFactory,
            IFollowerService? follower,
            IFlushCadence? flushCadence,
            int prefetchWorkers)
            : this(gate, loggerFactory, follower, flushCadence, prefetchWorkers, Nethereum.CoreChain.ExecutionStackOptions.DefaultStateRootWorkers)
        {
        }

        public MainnetChainNodeFactory(
            IConsensusBlockGate gate,
            ILoggerFactory? loggerFactory,
            IFollowerService? follower,
            IFlushCadence? flushCadence,
            int prefetchWorkers,
            int stateRootWorkers,
            StandardGenesisDocument? genesis = null)
        {
            if (stateRootWorkers < 1) throw new ArgumentOutOfRangeException(nameof(stateRootWorkers), "StateRootWorkers must be >= 1 (1 = sequential).");
            _stateRootWorkers = stateRootWorkers;
            _genesis = genesis;
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _follower = follower;
            _flushCadence = flushCadence;
            _prefetchWorkers = prefetchWorkers;
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "mainnet-follower", "Build the follower chain config for mainnet or a custom genesis")]
        public static ChainConfig BuildChainConfig(StandardGenesisDocument? genesis = null)
        {
            if (genesis == null)
                return new ChainConfig
                {
                    ChainId = MainnetGenesisConstants.ChainId,
                    BaseFee = BigInteger.Zero,
                    Coinbase = AddressUtil.ZERO_ADDRESS,
                    Activations = MainnetChainActivations.Instance,
                    Registry = MainnetChainHardforkRegistry.Instance,
                    Hardfork = "cancun",
                    DepositContractAddress = DepositRequests.DepositContractAddress,
                };

            var custom = new ChainConfig { Registry = MainnetChainHardforkRegistry.Instance };
            StandardGenesisLoader.ApplyToChainConfig(custom, genesis);
            custom.BaseFee = genesis.BaseFeePerGas ?? BigInteger.Zero;
            return custom;
        }

        public FollowerChainNode Build(
            IChainStoreBundle bundle,
            IBlockSource source,
            IValidationPolicy policy,
            FollowerOptions options,
            ICanonicalStateRootSource? canonical = null)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (options == null) throw new ArgumentNullException(nameof(options));

            _loggerFactory.CreateLogger<MainnetChainNodeFactory>().LogInformation(
                "Follower flush cadence: {Cadence}",
                _flushCadence is FixedIntervalFlushCadence fixedCadence
                    ? $"every {fixedCadence.K} blocks (batched)"
                    : "every block (K=1)");
            _loggerFactory.CreateLogger<MainnetChainNodeFactory>().LogInformation(
                "Follower state prefetch workers: {Workers}", _prefetchWorkers);
            _loggerFactory.CreateLogger<MainnetChainNodeFactory>().LogInformation(
                "Follower state-root workers: {Workers}", _stateRootWorkers);

            var chainConfig = BuildChainConfig(_genesis);
            var activations = chainConfig.ResolveActivations();
            var hardforkConfig = _genesis == null
                ? MainnetChainHardforkRegistry.Instance.Get(HardforkName.Cancun)
                : chainConfig.GetHardforkConfigAt(0, (ulong)chainConfig.GenesisTimestamp);
            var txVerifier = new TransactionVerificationAndRecoveryImp();
            var txProcessor = new TransactionProcessor(
                bundle.State, bundle.Blocks, chainConfig, txVerifier, hardforkConfig);

            Func<HardforkName, ChainConfig> chainConfigFactory = f => new ChainConfig
            {
                ChainId = chainConfig.ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = AddressUtil.ZERO_ADDRESS,
                Hardfork = f.ToString().ToLowerInvariant()
            };
            Func<HardforkName, HardforkConfig> hardforkConfigFactory = f => MainnetChainHardforkRegistry.Instance.Get(f);
            var prefetcher = _prefetchWorkers > 0
                ? new StatePrefetcher(
                    bundle.State, bundle.Blocks, activations,
                    chainConfigFactory, hardforkConfigFactory, authorResolver: null, _prefetchWorkers)
                : null;

            var resources = new ExecutionResourceSet();
            resources.Add(prefetcher);

            Func<IChainStoreBundle, IBlockExecutor> executorFactory = b =>
            {
                var stack = FollowerExecutorStackFactory.BuildFollowerExecutorStackWithCalculator(
                    b,
                    activations,
                    chainConfigFactory: chainConfigFactory,
                    hardforkConfigFactory: hardforkConfigFactory,
                    rewardPolicy: EthereumProofOfWorkRewardPolicy.Instance,
                    loggerFactory: _loggerFactory,
                    stateOverride: null,
                    flushCadence: _flushCadence,
                    prefetcher: ReferenceEquals(b, bundle) ? prefetcher : null,
                    stateRootWorkers: _stateRootWorkers);
                resources.Add(stack.Calculator);

                return new ConsensusGatedBlockExecutor(
                    stack.Executor,
                    _gate,
                    _loggerFactory.CreateLogger<ConsensusGatedBlockExecutor>());
            };

            return new FollowerChainNode(
                bundle: bundle,
                source: source,
                executorFactory: executorFactory,
                policy: policy,
                options: options,
                chainConfig: chainConfig,
                hardforkConfig: hardforkConfig,
                txProcessor: txProcessor,
                txVerifier: txVerifier,
                follower: _follower,
                canonical: canonical,
                activations: activations,
                hardforkConfigFactory: f => MainnetChainHardforkRegistry.Instance.Get(f),
                logger: _loggerFactory.CreateLogger<FollowerChainNode>())
            {
                ExecutionResources = resources
            };
        }

    }
}
