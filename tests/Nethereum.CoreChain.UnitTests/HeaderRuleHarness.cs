using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.Codecs;

namespace Nethereum.CoreChain.UnitTests
{
    internal sealed class HeaderRuleHarness
    {
        public const long ParentNumber = 1000;
        public const long ParentTimestamp = 1_700_000_000;
        public const long ParentBaseFee = 1_000;
        public const long ParentDifficulty = 2_000_000_000;

        private readonly ChainConfig _config;
        private readonly IChainActivations _activations;

        private HeaderRuleHarness(
            HardforkName fork, BlockHeader parent, byte[] parentHash, BlockImporter importer,
            ChainConfig config, IChainActivations activations)
        {
            Fork = fork;
            Parent = parent;
            ParentHash = parentHash;
            Importer = importer;
            _config = config;
            _activations = activations;
        }

        public HardforkName Fork { get; }
        public BlockHeader Parent { get; }
        public byte[] ParentHash { get; }
        public BlockImporter Importer { get; }

        public static async Task<HeaderRuleHarness> CreateAsync(
            HardforkName fork,
            long parentGasLimit = 30_000_000,
            HardforkName? parentFork = null,
            IChainActivations activations = null,
            BlockValidationOptions options = null)
        {
            var resolvedParentFork = parentFork ?? fork;
            activations ??= new FixedChainActivations(fork);

            var stateStore = new InMemoryStateStore();
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(stateStore, fork);

            var blockStore = new InMemoryBlockStore();
            var config = new ChainConfig { ChainId = 1337, BlockGasLimit = parentGasLimit, BaseFee = 0 };
            var trieNodeStore = new InMemoryContentNodeStore();

            var engine = new BlockExecutor(
                stateStore,
                blockStore,
                activations,
                chainConfigFactory: _ => config,
                hardforkConfigFactory: config.ConfigForFork,
                stateRootCalculator: new IncrementalStateRootCalculator(stateStore, trieNodeStore),
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore,
                logger: null,
                blockRootsProvider: null,
                authorResolver: null,
                validation: options ?? new BlockValidationOptions());

            var parent = ShapedParent(resolvedParentFork, parentGasLimit);
            var parentHash = BlockHashCalculator.ForFork(parent, resolvedParentFork);
            await blockStore.SaveAsync(parent, parentHash);

            return new HeaderRuleHarness(
                fork, parent, parentHash, new BlockImporter(engine, blockStore, stateStore), config, activations);
        }

        public bool IsProofOfStake => Fork >= HardforkName.Paris;

        public BlockHeader ValidChild()
        {
            var codec = BlockHeaderCodecs.ForFork(Fork);
            var number = ParentNumber + 1;
            var timestamp = ParentTimestamp + 12;

            var child = new BlockHeader
            {
                ParentHash = ParentHash,
                UnclesHash = DefaultValues.EMPTY_UNCLES_HASH,
                Coinbase = ShapedHeaders.Coinbase,
                StateRoot = new byte[32],
                TransactionsHash = DefaultValues.EMPTY_TRIE_ROOT,
                ReceiptHash = DefaultValues.EMPTY_TRIE_ROOT,
                LogsBloom = new byte[256],
                BlockNumber = number,
                GasLimit = Parent.GasLimit,
                GasUsed = 0,
                Timestamp = timestamp,
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8]
            };

            child.Difficulty = IsProofOfStake
                ? 0
                : (Nethereum.Util.EvmUInt256)EthashDifficulty.Calculate(Fork, (ulong)timestamp, Parent);

            if (codec.CarriesBaseFee)
                child.BaseFee = BaseFeeCalculator.CalculateExpectedBaseFeePerGas(
                    Parent.BaseFee, Parent.GasLimit, Parent.GasUsed);

            if (codec.CarriesWithdrawalsRoot)
                child.WithdrawalsRoot = DefaultValues.EMPTY_TRIE_ROOT;

            if (codec.CarriesBlobFieldsAndBeaconRoot)
            {
                var hardforkConfig = _config.ConfigForFork(Fork).AtBlock(_activations, number, (ulong)timestamp);
                child.BlobGasUsed = 0;
                child.ExcessBlobGas = unchecked((long)BlockExecutor.ExpectedExcessBlobGas(Parent, hardforkConfig));
                child.ParentBeaconBlockRoot = new byte[32];
            }

            if (codec.CarriesRequestsHash)
                child.RequestsHash = new byte[32];

            if (codec.CarriesBlockAccessList)
            {
                child.BlockAccessListHash = new byte[32];
                child.SlotNumber = (ulong)number;
            }

            return child;
        }

        public Task<BlockImporterResult> ImportAsync(
            BlockHeader header, IList<BlockHeader> uncles = null, IList<Withdrawal> withdrawals = null)
        {
            var emptyWithdrawals = withdrawals ?? (header.WithdrawalsRoot != null ? new List<Withdrawal>() : null);
            return ImportWithBodyAsync(header, uncles, emptyWithdrawals);
        }

        public Task<BlockImporterResult> ImportWithBodyAsync(
            BlockHeader header, IList<BlockHeader> uncles, IList<Withdrawal> withdrawals) =>
            Importer.ImportAsync(
                header, new List<ISignedTransaction>(), uncles, withdrawals, CancellationToken.None);

        private static BlockHeader ShapedParent(HardforkName fork, long gasLimit) =>
            ShapedHeaders.Create(
                fork, ParentNumber, ParentTimestamp, gasLimit, gasLimit / 2, ParentBaseFee,
                fork >= HardforkName.Paris ? 0 : ParentDifficulty);
    }
}
