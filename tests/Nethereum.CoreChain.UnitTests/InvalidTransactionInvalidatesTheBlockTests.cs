using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class InvalidTransactionInvalidatesTheBlockTests
    {
        private static ISignedTransaction BelowTheIntrinsicFloor(int nonce) =>
            BlockPipelineHarness.Transfer(nonce, gasLimit: BlockPipelineHarness.IntrinsicTransferGas - 1);

        private static async Task<(BlockHeader Header, List<ISignedTransaction> AllTransactions)>
            MintBlockOmittingAsync(ISignedTransaction invalid)
        {
            var author = await BlockPipelineHarness.CreateAsync();
            var mempool = new List<ISignedTransaction> { BlockPipelineHarness.Transfer(nonce: 0), invalid };
            var produced = await author.ProduceAsync(mempool);
            return (produced.Header, mempool);
        }

        [Fact]
        public async Task Given_ABlockContainingATransactionBelowTheIntrinsicFloor_When_Imported_Then_TheBlockIsRejected()
        {
            var (header, transactions) = await MintBlockOmittingAsync(BelowTheIntrinsicFloor(nonce: 1));
            var follower = await BlockPipelineHarness.CreateAsync();

            var result = await follower.ImportAsync(header, transactions);

            Assert.False(result.RootMatches);
            Assert.Null(result.BlockHash);
            Assert.Contains("invalidTransaction", result.FailedChecks);
        }

        [Fact]
        public async Task Given_ABlockContainingANonceMismatchedTransaction_When_Imported_Then_TheBlockIsRejected()
        {
            var (header, transactions) = await MintBlockOmittingAsync(BlockPipelineHarness.Transfer(nonce: 5));
            var follower = await BlockPipelineHarness.CreateAsync();

            var result = await follower.ImportAsync(header, transactions);

            Assert.False(result.RootMatches);
            Assert.Null(result.BlockHash);
            Assert.Contains("invalidTransaction", result.FailedChecks);
        }

        [Fact]
        public async Task Given_TheSameInvalidTransactionInTheMempool_When_ABlockIsPRODUCED_Then_ItIsExcludedAndTheBlockIsValid()
        {
            var author = await BlockPipelineHarness.CreateAsync();
            var good = BlockPipelineHarness.Transfer(nonce: 0);
            var mempool = new List<ISignedTransaction> { good, BelowTheIntrinsicFloor(nonce: 1) };

            var produced = await author.ProduceAsync(mempool);

            Assert.NotNull(produced.BlockHash);
            Assert.Single(produced.TransactionResults);
            Assert.Equal(good.Hash, produced.TransactionResults.Single().TxHash);

            var follower = await BlockPipelineHarness.CreateAsync();
            var imported = await follower.ImportAsync(produced.Header, new List<ISignedTransaction> { good });

            Assert.True(imported.RootMatches);
            Assert.NotNull(imported.BlockHash);

            Assert.True(
                author.TrieNodeStore.ContainsKey(produced.Header.StateRoot),
                "the produced block's state root must be resolvable from the author's own trie node store");
        }

        [Fact]
        public async Task Given_ABlockWhoseTransactionsAreAllValid_When_Imported_Then_ItIsStillAccepted()
        {
            var author = await BlockPipelineHarness.CreateAsync();
            var transactions = new List<ISignedTransaction>
            {
                BlockPipelineHarness.Transfer(nonce: 0),
                BlockPipelineHarness.Transfer(nonce: 1)
            };
            var produced = await author.ProduceAsync(transactions);
            Assert.Equal(2, produced.TransactionResults.Count);

            var follower = await BlockPipelineHarness.CreateAsync();
            var result = await follower.ImportAsync(produced.Header, transactions);

            Assert.True(result.RootMatches);
            Assert.NotNull(result.BlockHash);
            Assert.Empty(result.FailedChecks);
        }

        [Fact]
        public async Task Given_ARejectedBlock_When_Imported_Then_TheResultNamesTheFailingTransactionIndexAndReason()
        {
            var (header, transactions) = await MintBlockOmittingAsync(BelowTheIntrinsicFloor(nonce: 1));
            var follower = await BlockPipelineHarness.CreateAsync();

            var result = await follower.ImportAsync(header, transactions);

            Assert.False(result.RootMatches);
            Assert.Equal(TransactionError.IntrinsicGasTooLow, result.InvalidTransactionReason);
            Assert.Equal(1, result.InvalidTransactionIndex);
            Assert.Equal(transactions.Count, result.ExecutionResults.Count);
        }

        [Fact]
        public async Task Given_ABlockRejectedForAnInvalidTransaction_When_Described_Then_ItNamesTheTransactionNotAStateRootComparison()
        {
            var (header, transactions) = await MintBlockOmittingAsync(BelowTheIntrinsicFloor(nonce: 1));
            var follower = await BlockPipelineHarness.CreateAsync();

            var result = await follower.ImportAsync(header, transactions);

            Assert.False(result.StateRootMismatch);
            Assert.Equal(result.ExpectedStateRoot, result.ComputedStateRoot);

            var description = result.DescribeRejection();
            Assert.Contains("invalidTransaction", description);
            Assert.Contains("transaction 1", description);
            Assert.Contains(nameof(TransactionError.IntrinsicGasTooLow), description);
            Assert.DoesNotContain("state root computed", description);
        }

        [Fact]
        public async Task Given_ABlockRejectedForAGenuineRootDivergence_When_Described_Then_ItStillComparesTheRoots()
        {
            var author = await BlockPipelineHarness.CreateAsync();
            var transactions = new List<ISignedTransaction> { BlockPipelineHarness.Transfer(nonce: 0) };
            var produced = await author.ProduceAsync(transactions);

            var tampered = BlockPipelineHarness.CloneHeader(produced.Header);
            tampered.StateRoot[0] ^= 0xff;

            var follower = await BlockPipelineHarness.CreateAsync();
            var result = await follower.ImportAsync(tampered, transactions);

            Assert.True(result.StateRootMismatch);

            var description = result.DescribeRejection();
            Assert.Contains("state root computed", description);
            Assert.DoesNotContain("invalidTransaction", description);
        }
    }
}
