using System.Numerics;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain
{
    public class ChainConfig
    {
        public BigInteger ChainId { get; set; } = 1337;
        public string Coinbase { get; set; } = AddressUtil.ZERO_ADDRESS;
        protected BigInteger? _blockGasLimit;

        public virtual BigInteger BlockGasLimit
        {
            get => _blockGasLimit ?? EVM.Gas.GasConstants.BLOCK_EXECUTION_GAS_HEADROOM;
            set => _blockGasLimit = value;
        }

        public long BlockGasLimitLargeEnoughToDeployAt(HardforkName fork)
            => EVM.Gas.GasConstants.BlockGasLimitLargeEnoughToDeployAt(
                Registry.Get(fork).IntrinsicGasRules.StateGasActive);
        public BigInteger BaseFee { get; set; } = 1_000_000_000;
        public BigInteger SuggestedPriorityFee { get; set; } = 1_000_000_000;
        public int EstimateGasPaddingPercent { get; set; } = 10;
        public BigInteger InitialBalance { get; set; } = BigInteger.Parse("10000000000000000000000");
        public const string DefaultHardfork = "prague";

        public ChainForkSchedule ForkSchedule { get; set; } =
            new ChainForkSchedule { Hardfork = DefaultHardfork };

        public string Hardfork
        {
            get => ForkSchedule.Hardfork;
            set => ForkSchedule.Hardfork = value ?? DefaultHardfork;
        }

        public long GenesisTimestamp { get; set; } = 0;

        public BigInteger? GenesisDifficulty { get; set; }
        public byte[] GenesisNonce { get; set; }
        public byte[] GenesisMixHash { get; set; }
        public byte[] GenesisExtraData { get; set; }
        public long? GenesisExcessBlobGas { get; set; }
        public long? GenesisBlobGasUsed { get; set; }
        public long? GenesisSlotNumber { get; set; }

        public int RpcMaxLogBlockRange { get; set; } = 10_000;

        public int RpcMaxLogResults { get; set; } = 10_000;

        public BigInteger RpcGasCap { get; set; } = 50_000_000;

        public string DepositContractAddress { get; set; } = AddressUtil.ZERO_ADDRESS;

        public IChainActivations Activations { get; set; }

        public HardforkRegistry Registry { get; set; } = DefaultMainnetHardforkRegistry.Instance;

        public StateTreeType StateTree { get; set; } = StateTreeType.Patricia;
        public IHashProvider StateTreeHashProvider { get; set; }

        public HardforkName PinnedFork => ResolveActivations().ResolveAt(0, 0);

        public HardforkConfig GetHardforkConfig()
            => Registry.Get(PinnedFork);

        public IChainActivations ResolveActivations()
            => Activations ?? ForkSchedule.ResolveActivations();

        public HardforkConfig ConfigForFork(HardforkName fork)
            => Registry.Get(fork);

        public HardforkName NewestForkThisChainRuns
            => ResolveActivations().ResolveAt(long.MaxValue, ulong.MaxValue);

        public HardforkName ResolveHardforkAt(long blockNumber, ulong timestamp)
            => ResolveActivations().ResolveAt(blockNumber, timestamp);

        public HardforkConfig GetHardforkConfigAt(long blockNumber, ulong timestamp)
            => Registry.Get(ResolveHardforkAt(blockNumber, timestamp));

        public IStateRootCalculator CreateStateRootCalculator()
        {
            return StateTree switch
            {
                StateTreeType.Binary => new BinaryStateRootCalculator(
                    StateTreeHashProvider ?? new Blake3HashProvider()),
                _ => new PatriciaStateRootCalculator(new RlpBlockEncodingProvider())
            };
        }

        public IncrementalStateRootCalculator CreateIncrementalStateRootCalculator(
            IStateStore stateStore, ITrieNodeStore trieNodeStore = null)
        {
            return new IncrementalStateRootCalculator(stateStore, trieNodeStore,
                Sha3KeccackHashProvider.Instance);
        }

        public BinaryIncrementalStateRootCalculator CreateBinaryIncrementalStateRootCalculator(
            IStateStore stateStore, IBinaryTrieStorage trieStorage = null)
        {
            return new BinaryIncrementalStateRootCalculator(stateStore,
                StateTreeHashProvider ?? new Blake3HashProvider(), trieStorage);
        }
    }
}
