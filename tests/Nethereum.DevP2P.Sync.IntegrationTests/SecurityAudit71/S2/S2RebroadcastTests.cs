using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.IntegrationTests.SecurityAudit71.S2
{
    public class S2RebroadcastTests
    {
        private const string SenderKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string Recipient = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private static readonly System.Numerics.BigInteger TenEther =
            System.Numerics.BigInteger.Parse("10000000000000000000");

        [Fact]
        public async Task S2_06_Given_ATransactionAlreadyPendingAndBroadcast_When_TheSameTransactionIsSubmittedAgain_Then_ThePeerReceivesItOnlyOnce()
        {
            var (a, b) = await DevChainNetwork.TwoNodesWithASingleSessionFromAToBAsync();
            try
            {
                var sender = DevChainTransactions.AddressOf(SenderKey);
                await a.FundAccountAsync(sender, TenEther);
                var tx = DevChainTransactions.SignEip1559(SenderKey, (int)a.ChainId, Recipient, nonce: 0, value: 1000);

                var first = await a.Mempool.SubmitAsync(tx);
                Assert.True(first.Accepted, first.RejectMessage);
                Assert.True(await DevChainNetwork.WaitUntilAsync(
                    () => b.ObservedRemoteTransactions.Any(t => ByteUtil.AreEqual(t.Hash, tx.Hash)),
                    TimeSpan.FromSeconds(15)));

                for (var i = 0; i < 4; i++) await a.Mempool.SubmitAsync(tx);
                await Task.Delay(TimeSpan.FromSeconds(2));

                var received = b.ObservedRemoteTransactions.Count(t => ByteUtil.AreEqual(t.Hash, tx.Hash));
                Assert.True(received == 1, $"the same transaction was sent to the peer {received} times");
            }
            finally
            {
                await a.DisposeAsync();
                await b.DisposeAsync();
            }
        }
    }
}
