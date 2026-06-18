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
using Nethereum.EVM.Precompiles;
using Nethereum.MainnetChain.Server.Gate;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.MainnetChain.Server.Hosting
{
    /// <summary>
    /// Builds the canonical mainnet follower executor stack (BlockExecutor + BlockImporter)
    /// against an opened <see cref="IChainStoreBundle"/>, then wraps it with the configured
    /// <see cref="IConsensusBlockGate"/> decorator. The composition mirrors the one used by
    /// <c>tools/Nethereum.DevP2P.SyncNode/Program.cs</c> so the same execution path covers
    /// both the from-genesis replay validator and a long-running follower host.
    /// </summary>
    public sealed class MainnetChainNodeFactory
    {
        private readonly IConsensusBlockGate _gate;
        private readonly ILoggerFactory _loggerFactory;

        public MainnetChainNodeFactory(IConsensusBlockGate gate, ILoggerFactory? loggerFactory = null)
        {
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        }

        public MainnetChainNode Build(
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

            var chainConfig = new ChainConfig
            {
                ChainId = MainnetGenesisConstants.ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = AddressUtil.ZERO_ADDRESS,
                Hardfork = "cancun",
            };
            var hardforkConfig = DefaultMainnetHardforkRegistry.Instance.Get(HardforkName.Cancun);
            var txVerifier = new TransactionVerificationAndRecoveryImp();
            var txProcessor = new TransactionProcessor(
                bundle.State, bundle.Blocks, chainConfig, txVerifier, hardforkConfig);

            Func<IChainStoreBundle, IBlockExecutor> executorFactory = b =>
            {
                var calc = new IncrementalStateRootCalculator(b.State, b.TrieNodes);
                var engine = new BlockExecutor(
                    b.State, b.Blocks, MainnetChainActivations.Instance,
                    chainConfigFactory: f => new ChainConfig
                    {
                        ChainId = MainnetGenesisConstants.ChainId,
                        BaseFee = BigInteger.Zero,
                        Coinbase = AddressUtil.ZERO_ADDRESS,
                        Hardfork = f.ToString().ToLowerInvariant()
                    },
                    hardforkConfigFactory: f => DefaultMainnetHardforkRegistry.Instance.Get(f),
                    stateRootCalculator: calc,
                    rewardPolicy: EthereumProofOfWorkRewardPolicy.Instance,
                    trieNodeStore: b.TrieNodes);

                IBlockExecutor inner = new BlockImporter(
                    engine, b.Blocks, b.State,
                    b.Transactions, b.Receipts, b.Logs, b.Uncles);

                inner = new ConsensusGatedBlockExecutor(
                    inner,
                    _gate,
                    _loggerFactory.CreateLogger<ConsensusGatedBlockExecutor>());

                return inner;
            };

            return new MainnetChainNode(
                bundle: bundle,
                source: source,
                executorFactory: executorFactory,
                policy: policy,
                options: options,
                chainConfig: chainConfig,
                hardforkConfig: hardforkConfig,
                txProcessor: txProcessor,
                txVerifier: txVerifier,
                canonical: canonical,
                logger: _loggerFactory.CreateLogger<MainnetChainNode>());
        }
    }
}
