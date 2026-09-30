using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using AppChainCore = Nethereum.AppChain.AppChain;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

using Nethereum.Chain.TestData;

namespace Nethereum.Chain.TestData.UnitTests
{
    public sealed class FollowerNode
    {
        private readonly BlockImporter _importer;

        public IBlockStore Blocks { get; }
        public IStateStore State { get; }
        public IReceiptStore Receipts { get; }
        public ILogStore Logs { get; }
        public ITransactionStore Transactions { get; }
        public IBlockAccessListStore BlockAccessLists { get; }
        public ChainStores Stores => new ChainStores(Blocks, State, Transactions, Receipts, Logs, BlockAccessLists);

        private FollowerNode(BlockImporter importer, IBlockStore blocks, IStateStore state, ITransactionStore transactions, IReceiptStore receipts, ILogStore logs, IBlockAccessListStore blockAccessLists)
        {
            _importer = importer;
            Blocks = blocks;
            State = state;
            Transactions = transactions;
            Receipts = receipts;
            Logs = logs;
            BlockAccessLists = blockAccessLists;
        }

        public static async Task<FollowerNode> CreateAsync(InProcessSequencerDriver sequencer)
        {
            var blocks = new InMemoryBlockStore();
            var txs = new InMemoryTransactionStore(blocks);
            var receipts = new InMemoryReceiptStore();
            var logs = new InMemoryLogStore();
            var state = new InMemoryStateStore();
            var trie = new InMemoryContentNodeStore();
            var blockAccessLists = new InMemoryBlockAccessListStore(blocks);

            var config = Nethereum.AppChain.AppChainConfig.CreateWithName(InProcessSequencerDriver.ChainName, sequencer.ChainId);
            config.SequencerAddress = sequencer.SequencerAddress;
            config.Hardfork = sequencer.Hardfork;
            var appChain = new AppChainCore(config, blocks, txs, receipts, logs, state, trie);
            await appChain.InitializeAsync(sequencer.Genesis);

            var chainConfig = new ChainConfig
            {
                ChainId = sequencer.ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = sequencer.SequencerAddress,
                Hardfork = sequencer.Hardfork
            };
            var activations = new FixedChainActivations(HardforkNames.Parse(chainConfig.Hardfork ?? "prague"));
            var hardforkConfig = chainConfig.GetHardforkConfig();
            var calc = new IncrementalStateRootCalculator(state, trie);
            var engine = new BlockExecutor(
                state, blocks, activations,
                chainConfigFactory: _ => chainConfig,
                hardforkConfigFactory: _ => hardforkConfig,
                stateRootCalculator: calc,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trie);
            var importer = new BlockImporter(engine, blocks, state, txs, receipts, logs, uncleStore: null,
                logger: null, nodeCommitBlockContext: null, atomicFlush: null, flushCadence: null,
                blockAccessListStore: blockAccessLists);

            return new FollowerNode(importer, blocks, state, txs, receipts, logs, blockAccessLists);
        }

        public async Task ConsumeAsync(InProcessSequencerDriver sequencer)
        {
            foreach (var (header, transactions) in sequencer.ProducedBlockData)
            {
                var result = await _importer.ImportAsync(header, transactions, null, null);
                if (!result.RootMatches)
                    throw new Exception(
                        $"Follower diverged at block {header.BlockNumber}: computed state root != served state root");
            }
        }
    }
}
