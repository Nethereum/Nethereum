using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class RejectedBlockLeavesNoStateTests
    {
        private static Task<BlockImporter> OpenJournalingFollowerAsync() =>
            OpenFollowerAsync(new HistoricalStateStore(
                new InMemoryStateStore(), new InMemoryStateDiffStore(), HistoricalStateOptions.DevChainDefault));

        private static async Task<BlockImporter> OpenFollowerAsync(IStateStore stateStore)
        {
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(stateStore, HardforkName.Prague);
            await stateStore.SaveAccountAsync(BlockPipelineHarness.SenderAddress, new Account
            {
                Balance = 1_000_000_000_000_000,
                Nonce = 0
            });

            var blockStore = new InMemoryBlockStore();
            await ShapedHeaders.SaveGenesisAsync(blockStore, HardforkName.Prague, 30_000_000);
            var config = new ChainConfig { ChainId = BlockPipelineHarness.ChainId, BlockGasLimit = 30_000_000, BaseFee = 0 };
            var trieNodeStore = new InMemoryContentNodeStore();
            var engine = new BlockExecutor(
                stateStore, blockStore, new FixedChainActivations(HardforkName.Prague),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                stateRootCalculator: new IncrementalStateRootCalculator(stateStore, trieNodeStore),
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);
            return new BlockImporter(engine, blockStore, stateStore);
        }

        [Fact]
        public async Task Given_ABlockRejectedAfterItsTransactionsRan_When_TheValidBlockIsImportedNext_Then_ItIsAccepted()
        {
            var author = await BlockPipelineHarness.CreateAsync();
            var transactions = new List<ISignedTransaction> { BlockPipelineHarness.Transfer(nonce: 0) };
            var produced = await author.ProduceAsync(transactions);

            var rejected = BlockPipelineHarness.CloneHeader(produced.Header);
            rejected.ReceiptHash = new byte[32];

            var follower = await OpenJournalingFollowerAsync();
            var refusal = await follower.ImportAsync(rejected, transactions, uncles: null, withdrawals: null, CancellationToken.None);
            var accepted = await follower.ImportAsync(produced.Header, transactions, uncles: null, withdrawals: null, CancellationToken.None);

            Assert.True(refusal.ReceiptsRootMismatch);
            Assert.True(accepted.RootMatches, accepted.DescribeRejection());
        }

        [Fact]
        public async Task Given_ABlockWhoseTransactionsRootIsWrong_When_ImportedOnAStoreWithoutAJournal_Then_ItIsRefusedWithoutExecutionAndTheValidBlockIsAcceptedNext()
        {
            var author = await BlockPipelineHarness.CreateAsync();
            var transactions = new List<ISignedTransaction> { BlockPipelineHarness.Transfer(nonce: 0) };
            var produced = await author.ProduceAsync(transactions);

            var rejected = BlockPipelineHarness.CloneHeader(produced.Header);
            rejected.TransactionsHash = new byte[32];

            var follower = await OpenFollowerAsync(new InMemoryStateStore());
            var refusal = await follower.ImportAsync(rejected, transactions, uncles: null, withdrawals: null, CancellationToken.None);
            var accepted = await follower.ImportAsync(produced.Header, transactions, uncles: null, withdrawals: null, CancellationToken.None);

            Assert.Equal(new[] { BlockValidityCheck.TransactionsRoot }, refusal.FailedValidityChecks);
            Assert.Null(refusal.ComputedStateRoot);
            Assert.True(accepted.RootMatches, accepted.DescribeRejection());
        }

        [Fact]
        public async Task Given_ABlockWhoseWithdrawalsRootIsWrong_When_ImportedOnAStoreWithoutAJournal_Then_ItIsRefusedWithoutExecutionAndTheValidBlockIsAcceptedNext()
        {
            var author = await BlockPipelineHarness.CreateAsync();
            var transactions = new List<ISignedTransaction> { BlockPipelineHarness.Transfer(nonce: 0) };
            var produced = await author.ProduceAsync(transactions);

            var rejected = BlockPipelineHarness.CloneHeader(produced.Header);
            rejected.WithdrawalsRoot = new byte[32];

            var follower = await OpenFollowerAsync(new InMemoryStateStore());
            var refusal = await follower.ImportAsync(rejected, transactions, uncles: null, withdrawals: null, CancellationToken.None);
            var accepted = await follower.ImportAsync(produced.Header, transactions, uncles: null, withdrawals: null, CancellationToken.None);

            Assert.Equal(new[] { BlockValidityCheck.WithdrawalsRoot }, refusal.FailedValidityChecks);
            Assert.Null(refusal.ComputedStateRoot);
            Assert.True(accepted.RootMatches, accepted.DescribeRejection());
        }
    }
}
