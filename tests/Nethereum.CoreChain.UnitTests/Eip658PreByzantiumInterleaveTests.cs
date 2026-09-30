using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.Execution.TxFinalisation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class Eip658PreByzantiumInterleaveTests
    {
        [Fact]
        [Trait("Category", "Eip658PreByzantiumReceiptPostState")]
        public async Task Given_PreByzantiumThreeTxBlock_When_PerTxPostStateStampingApplied_Then_FinalBlockStateRootMatchesUnstampedControl()
        {
            var stamped = await BuildAsync(HardforkConfig.Frontier);
            var control = await BuildAsync(WithoutIntermediateStamping(HardforkConfig.Frontier));

            var stampedResult = await stamped.ProduceBlockAsync(ThreeTransfers(), Options());
            var controlResult = await control.ProduceBlockAsync(ThreeTransfers(), Options());

            Assert.Equal(3, stampedResult.SuccessfulTransactions);
            Assert.Equal(3, controlResult.SuccessfulTransactions);
            Assert.NotNull(stampedResult.Header.StateRoot);
            Assert.NotNull(controlResult.Header.StateRoot);
            Assert.Equal(controlResult.Header.StateRoot.ToHex(), stampedResult.Header.StateRoot.ToHex());

            foreach (var txResult in stampedResult.TransactionResults)
            {
                Assert.False(txResult.Receipt.IsStatusReceipt);
                Assert.Equal(32, txResult.Receipt.PostStateOrStatus.Length);
            }

            foreach (var txResult in controlResult.TransactionResults)
            {
                Assert.True(txResult.Receipt.IsStatusReceipt);
            }
        }

        private static List<ISignedTransaction> ThreeTransfers() => new()
        {
            BlockPipelineHarness.Transfer(0),
            BlockPipelineHarness.Transfer(1),
            BlockPipelineHarness.Transfer(2),
        };

        private static BlockProductionOptions Options() => new()
        {
            Timestamp = 1_700_000_000,
            Coinbase = BlockPipelineHarness.SenderAddress,
            BaseFee = 0,
            BlockGasLimit = 30_000_000,
            Difficulty = 1,
            ChainId = BlockPipelineHarness.ChainId
        };

        private static HardforkConfig WithoutIntermediateStamping(HardforkConfig source)
        {
            var clone = source.Clone();
            clone.ReceiptConstruction = StatusReceiptConstructionRule.Instance;
            return clone;
        }

        private static async Task<BlockProducer> BuildAsync(HardforkConfig hardforkConfig)
        {
            var stateStore = new InMemoryStateStore();
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(stateStore, HardforkName.Frontier);
            await stateStore.SaveAccountAsync(BlockPipelineHarness.SenderAddress, new Account
            {
                Balance = 1_000_000_000_000_000,
                Nonce = 0
            });

            var blockStore = new InMemoryBlockStore();
            var config = new ChainConfig { ChainId = BlockPipelineHarness.ChainId, BlockGasLimit = 30_000_000, BaseFee = 0 };
            var trieNodeStore = new InMemoryContentNodeStore();
            var stateRootCalculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);

            var engine = new BlockExecutor(
                stateStore,
                blockStore,
                new FixedChainActivations(HardforkName.Frontier),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => hardforkConfig,
                stateRootCalculator: stateRootCalculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);

            return new BlockProducer(
                engine, blockStore,
                new InMemoryTransactionStore(blockStore), new InMemoryReceiptStore(), new InMemoryLogStore(),
                stateStore, trieNodeStore, stateRootCalculator);
        }
    }
}
