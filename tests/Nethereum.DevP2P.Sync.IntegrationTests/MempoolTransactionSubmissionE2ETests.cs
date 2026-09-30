using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.DevP2P.Sync.Mempool;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class MempoolTransactionSubmissionE2ETests
    {
        private const string SenderKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string NeverFundedKey = "0xdbda1821b80551c9d65939329250298aa3472ba22feea921c0cf5d620ea67b97";
        private const string Recipient = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private static readonly System.Numerics.BigInteger TenEther =
            System.Numerics.BigInteger.Parse("10000000000000000000");

        [Fact]
        public async Task Given_ValidTx_When_SubmittedViaSubmissionService_Then_SuccessWithHash_AndPooled()
        {
            var (a, b) = await DevChainNetwork.TwoConnectedNodesAsync();
            try
            {
                var sender = DevChainTransactions.AddressOf(SenderKey);
                await a.FundAccountAsync(sender, TenEther);
                var tx = DevChainTransactions.SignEip1559(SenderKey, (int)a.ChainId, Recipient, nonce: 0, value: 1000);

                var submission = new MempoolTransactionSubmissionService(a.Mempool);
                var result = await submission.SubmitAsync(tx);

                Assert.True(result.Success, result.RevertReason);
                Assert.NotNull(result.TransactionHash);
                Assert.True(ByteUtil.AreEqual(result.TransactionHash, tx.Hash));
                Assert.Equal(1, a.Mempool.PendingCount);
            }
            finally
            {
                await a.DisposeAsync();
                await b.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_UnfundedTx_When_SubmittedViaSubmissionService_Then_Failure_AndNotPooled()
        {
            var (a, b) = await DevChainNetwork.TwoConnectedNodesAsync();
            try
            {
                var tx = DevChainTransactions.SignEip1559(NeverFundedKey, (int)a.ChainId, Recipient, nonce: 0, value: 5000);

                var submission = new MempoolTransactionSubmissionService(a.Mempool);
                var result = await submission.SubmitAsync(tx);

                Assert.False(result.Success);
                Assert.Contains("InsufficientBalance", result.RevertReason);
                Assert.Equal(0, a.Mempool.PendingCount);
            }
            finally
            {
                await a.DisposeAsync();
                await b.DisposeAsync();
            }
        }
    }
}
