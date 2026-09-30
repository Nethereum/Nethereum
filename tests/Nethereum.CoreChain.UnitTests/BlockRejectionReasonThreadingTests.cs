using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockRejectionReasonThreadingTests
    {
        [Fact]
        public async Task Given_ABlockWhoseSecondTransactionHasAMismatchedNonce_When_Imported_Then_TheResultNamesNonceMismatchAtThatIndex()
        {
            var author = await BlockPipelineHarness.CreateAsync();
            var txs = new List<ISignedTransaction>
            {
                BlockPipelineHarness.Transfer(nonce: 0),
                BlockPipelineHarness.Transfer(nonce: 5)
            };
            var produced = await author.ProduceAsync(txs);

            var follower = await BlockPipelineHarness.CreateAsync();
            var result = await follower.ImportAsync(produced.Header, txs);

            Assert.Equal(2, result.ExecutionResults.Count);
            Assert.True(result.ExecutionResults[1].Skipped);

            Assert.Equal(TransactionError.NonceMismatch, result.InvalidTransactionReason);
            Assert.Equal(1, result.InvalidTransactionIndex);
        }
    }
}
