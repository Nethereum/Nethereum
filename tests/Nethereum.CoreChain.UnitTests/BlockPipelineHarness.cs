using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;

namespace Nethereum.CoreChain.UnitTests
{
    internal sealed class BlockPipelineHarness
    {
        public const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        public const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        public const string RecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";

        public static readonly BigInteger ChainId = 1337;

        public const long IntrinsicTransferGas = 21_000;

        private static readonly LegacyTransactionSigner Signer = new();

        private BlockPipelineHarness(
            InMemoryStateStore stateStore,
            InMemoryBlockStore blockStore,
            ITrieNodeStore trieNodeStore,
            IIncrementalStateRootCalculator stateRootCalculator,
            BlockExecutor engine,
            IBlockAccessListStore blockAccessListStore)
        {
            BlockAccessListStore = blockAccessListStore;
            StateStore = stateStore;
            BlockStore = blockStore;
            TrieNodeStore = trieNodeStore;
            StateRootCalculator = stateRootCalculator;
            Engine = engine;
        }

        public InMemoryStateStore StateStore { get; }
        public InMemoryBlockStore BlockStore { get; }
        public ITrieNodeStore TrieNodeStore { get; }
        public IIncrementalStateRootCalculator StateRootCalculator { get; }
        public BlockExecutor Engine { get; }
        public IBlockAccessListStore BlockAccessListStore { get; }

        public static async Task<BlockPipelineHarness> CreateAsync(HardforkName fork = HardforkName.Prague,
            IBlockAccessListStore blockAccessListStore = null)
        {
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, fork);
            await stateStore.SaveAccountAsync(SenderAddress, new Account
            {
                Balance = 1_000_000_000_000_000,
                Nonce = 0
            });

            var blockStore = new InMemoryBlockStore();
            var config = new ChainConfig { ChainId = ChainId, BlockGasLimit = 30_000_000, BaseFee = 0 };
            var trieNodeStore = new InMemoryContentNodeStore();
            var stateRootCalculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);

            var engine = new BlockExecutor(
                stateStore,
                blockStore,
                new FixedChainActivations(fork),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                stateRootCalculator: stateRootCalculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);

            return new BlockPipelineHarness(stateStore, blockStore, trieNodeStore, stateRootCalculator, engine, blockAccessListStore);
        }

        public BlockProducer Producer() =>
            new BlockProducer(
                Engine, BlockStore,
                new InMemoryTransactionStore(BlockStore), new InMemoryReceiptStore(), new InMemoryLogStore(),
                StateStore, TrieNodeStore, StateRootCalculator,
                orderingPolicy: null, blockHashProvider: null, blockEncodingProvider: null,
                blockRootsProvider: null, withdrawalStore: null, nodeCommitBlockContext: null,
                blockAccessListStore: BlockAccessListStore);

        public BlockImporter Importer() => new BlockImporter(Engine, BlockStore, StateStore);

        public Task<BlockProductionResult> ProduceAsync(List<ISignedTransaction> transactions) =>
            Producer().ProduceBlockAsync(transactions, new BlockProductionOptions
            {
                Timestamp = 1_700_000_000,
                Coinbase = SenderAddress,
                BaseFee = 0,
                BlockGasLimit = 30_000_000,
                Difficulty = 1,
                ChainId = ChainId
            });

        public Task<BlockImporterResult> ImportAsync(BlockHeader header, IList<ISignedTransaction> transactions) =>
            Importer().ImportAsync(header, transactions, uncles: null, withdrawals: null, CancellationToken.None);

        public static BlockHeader CloneHeader(BlockHeader header)
        {
            var provider = RlpBlockEncodingProvider.Instance;
            return provider.DecodeBlockHeader(provider.EncodeBlockHeader(header));
        }

        public const long AboveInitialBaseFeeGasPrice = 2_000_000_000;

        public static ISignedTransaction Transfer(BigInteger nonce, long gasLimit = IntrinsicTransferGas) =>
            TransactionFactory.CreateTransaction(
                Signer.SignTransaction(
                    PrivateKey.HexToByteArray(), ChainId, RecipientAddress, 100, nonce,
                    AboveInitialBaseFeeGasPrice, gasLimit, ""));
    }
}
