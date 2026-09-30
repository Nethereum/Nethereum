using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;

namespace Nethereum.CoreChain.UnitTests
{
    internal static class AmsterdamBlockPipelineHarness
    {
        public const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        public const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        public const string RecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";

        public const long DefaultBlockGasLimit = 30_000_000;
        public static readonly BigInteger ChainId = 1337;

        private static readonly LegacyTransactionSigner Signer = new();

        public const long AboveInitialBaseFeeGasPrice = 2_000_000_000;

        public static ISignedTransaction Transfer(BigInteger nonce) =>
            TransactionFactory.CreateTransaction(
                Signer.SignTransaction(
                    PrivateKey.HexToByteArray(), ChainId, RecipientAddress, 100, nonce,
                    AboveInitialBaseFeeGasPrice, 21_000, ""));

        public static BlockHeader CloneHeader(BlockHeader header)
        {
            var provider = RlpBlockEncodingProvider.Instance;
            return provider.DecodeBlockHeader(provider.EncodeBlockHeader(header));
        }

        public static async Task<(BlockHeader Header, List<ISignedTransaction> Transactions)> ProduceValidBlockAsync(
            List<ISignedTransaction> transactions = null)
        {
            var txs = transactions ?? new List<ISignedTransaction> { Transfer(nonce: 0) };
            var produced = await ProduceBlockAsync(txs, DefaultBlockGasLimit).ConfigureAwait(false);
            return (produced.Header, txs);
        }

        public static async Task<BlockProductionResult> ProduceBlockAsync(
            List<ISignedTransaction> transactions, long blockGasLimit,
            IBlockAccessListStore blockAccessListStore = null,
            Nethereum.EVM.HardforkName fork = Nethereum.EVM.HardforkName.Amsterdam,
            Func<InMemoryStateStore, Task> seed = null,
            ulong slotNumber = 1)
        {
            var stack = await CreateStackAsync(fork, seed).ConfigureAwait(false);
            var producer = new BlockProducer(
                stack.Engine, stack.BlockStore,
                new InMemoryTransactionStore(stack.BlockStore), new InMemoryReceiptStore(), new InMemoryLogStore(),
                stack.StateStore, stack.TrieNodeStore, stack.StateRootCalculator,
                orderingPolicy: null, blockHashProvider: null, blockEncodingProvider: null,
                blockRootsProvider: null, withdrawalStore: null, nodeCommitBlockContext: null,
                blockAccessListStore: blockAccessListStore);

            return await producer.ProduceBlockAsync(transactions, new BlockProductionOptions
            {
                Timestamp = 1_700_000_000,
                Coinbase = SenderAddress,
                BaseFee = 0,
                BlockGasLimit = blockGasLimit,
                Difficulty = 1,
                ChainId = ChainId,
                SlotNumber = slotNumber
            }).ConfigureAwait(false);
        }

        public static async Task<BlockImporter> CreateImporterAsync(
            IBlockAccessListStore blockAccessListStore = null)
        {
            var stack = await CreateStackAsync().ConfigureAwait(false);
            return new BlockImporter(
                stack.Engine, stack.BlockStore, stack.StateStore,
                transactionStore: null, receiptStore: null, logStore: null, uncleStore: null,
                logger: null, nodeCommitBlockContext: null, atomicFlush: null, flushCadence: null,
                blockAccessListStore: blockAccessListStore);
        }

        public static async Task<BlockImporterResult> ImportAsync(
            BlockHeader header, IList<ISignedTransaction> transactions,
            IBlockAccessListStore blockAccessListStore = null)
        {
            var importer = await CreateImporterAsync(blockAccessListStore).ConfigureAwait(false);
            return await importer.ImportAsync(
                header, transactions, uncles: null, withdrawals: null, CancellationToken.None).ConfigureAwait(false);
        }

        private sealed class Stack
        {
            public InMemoryStateStore StateStore;
            public InMemoryBlockStore BlockStore;
            public ITrieNodeStore TrieNodeStore;
            public IIncrementalStateRootCalculator StateRootCalculator;
            public BlockExecutor Engine;
        }

        private static async Task<Stack> CreateStackAsync(
            Nethereum.EVM.HardforkName fork = Nethereum.EVM.HardforkName.Amsterdam,
            Func<InMemoryStateStore, Task> seed = null,
            ulong slotNumber = 1)
        {
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, fork);
            await stateStore.SaveAccountAsync(SenderAddress, new Account
            {
                Balance = 1_000_000_000_000_000,
                Nonce = 0
            }).ConfigureAwait(false);
            if (seed != null) await seed(stateStore).ConfigureAwait(false);

            var blockStore = new InMemoryBlockStore();
            var config = new ChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = DefaultBlockGasLimit,
                BaseFee = 0,
                Hardfork = fork.ToString()
            };
            var trieNodeStore = new InMemoryContentNodeStore();
            var stateRootCalculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);

            return new Stack
            {
                StateStore = stateStore,
                BlockStore = blockStore,
                TrieNodeStore = trieNodeStore,
                StateRootCalculator = stateRootCalculator,
                Engine = new BlockExecutor(
                    stateStore,
                    blockStore,
                    new FixedChainActivations(fork),
                    chainConfigFactory: _ => config,
                    hardforkConfigFactory: _ => config.GetHardforkConfig(),
                    stateRootCalculator: stateRootCalculator,
                    rewardPolicy: NoRewardPolicy.Instance,
                    trieNodeStore: trieNodeStore)
            };
        }
    }
}
