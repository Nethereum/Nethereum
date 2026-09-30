using System;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
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

        public MainnetChainNodeFactory(
            IConsensusBlockGate gate,
            ILoggerFactory? loggerFactory = null,
            IFollowerService? follower = null,
            IFlushCadence? flushCadence = null)
        {
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _follower = follower;
            _flushCadence = flushCadence;
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

            var chainConfig = new ChainConfig
            {
                ChainId = MainnetGenesisConstants.ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = AddressUtil.ZERO_ADDRESS,
                Activations = MainnetChainActivations.Instance,
                Registry = MainnetChainHardforkRegistry.Instance,
                Hardfork = "cancun",
                DepositContractAddress = DepositRequests.DepositContractAddress,
            };
            var hardforkConfig = MainnetChainHardforkRegistry.Instance.Get(HardforkName.Cancun);
            var txVerifier = new TransactionVerificationAndRecoveryImp();
            var txProcessor = new TransactionProcessor(
                bundle.State, bundle.Blocks, chainConfig, txVerifier, hardforkConfig);

            Func<IChainStoreBundle, IBlockExecutor> executorFactory = b =>
            {
                var inner = FollowerExecutorStackFactory.BuildFollowerExecutorStack(
                    b,
                    MainnetChainActivations.Instance,
                    chainConfigFactory: f => new ChainConfig
                    {
                        ChainId = MainnetGenesisConstants.ChainId,
                        BaseFee = BigInteger.Zero,
                        Coinbase = AddressUtil.ZERO_ADDRESS,
                        Hardfork = f.ToString().ToLowerInvariant()
                    },
                    hardforkConfigFactory: f => MainnetChainHardforkRegistry.Instance.Get(f),
                    rewardPolicy: EthereumProofOfWorkRewardPolicy.Instance,
                    loggerFactory: _loggerFactory,
                    flushCadence: _flushCadence);

                return new ConsensusGatedBlockExecutor(
                    inner,
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
                activations: MainnetChainActivations.Instance,
                hardforkConfigFactory: f => MainnetChainHardforkRegistry.Instance.Get(f),
                logger: _loggerFactory.CreateLogger<FollowerChainNode>());
        }

    }
}
