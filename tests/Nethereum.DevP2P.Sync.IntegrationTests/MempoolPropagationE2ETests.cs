using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.DevP2P.Sync.Mempool;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class MempoolPropagationE2ETests
    {
        private const string SenderKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string NeverFundedKey = "0xdbda1821b80551c9d65939329250298aa3472ba22feea921c0cf5d620ea67b97";
        private const string Recipient = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private static readonly System.Numerics.BigInteger TenEther =
            System.Numerics.BigInteger.Parse("10000000000000000000");

        private readonly ITestOutputHelper _output;
        public MempoolPropagationE2ETests(ITestOutputHelper output) => _output = output;

        private static bool ContainsHash(IReadOnlyList<byte[]> hashes, byte[] target)
            => hashes.Any(h => ByteUtil.AreEqual(h, target));

        private static int FullBodyCount(IReadOnlyList<ISignedTransaction> txs, byte[] target)
            => txs.Count(t => ByteUtil.AreEqual(t.Hash, target));

        [Fact]
        public async Task Given_TwoConnectedNodes_When_TxnSubmittedToA_Then_QueuesOnA_AndSendsFullBodyToB()
        {
            var (a, b) = await DevChainNetwork.TwoConnectedNodesAsync();
            try
            {
                var sender = DevChainTransactions.AddressOf(SenderKey);
                await a.FundAccountAsync(sender, TenEther);

                var tx = DevChainTransactions.SignEip1559(SenderKey, (int)a.ChainId, Recipient, nonce: 0, value: 1000);

                var result = await a.Mempool.SubmitAsync(tx);

                Assert.True(result.Accepted, result.RejectMessage);
                Assert.Equal(1, a.Mempool.PendingCount);

                var received = await DevChainNetwork.WaitUntilAsync(
                    () => b.ObservedRemoteTransactions.Any(t => ByteUtil.AreEqual(t.Hash, tx.Hash)),
                    TimeSpan.FromSeconds(15));

                Assert.True(received, "B did not receive A's full-body Transactions(0x02) as the sole sqrt-direct peer");
            }
            finally
            {
                await a.DisposeAsync();
                await b.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AnnouncedTxn_When_BRequestsGetPooledTransactions_Then_AServesTheBody()
        {
            var (a, b) = await DevChainNetwork.TwoConnectedNodesAsync();
            try
            {
                var sender = DevChainTransactions.AddressOf(SenderKey);
                await a.FundAccountAsync(sender, TenEther);
                var tx = DevChainTransactions.SignEip1559(SenderKey, (int)a.ChainId, Recipient, nonce: 0, value: 2000);

                var result = await a.Mempool.SubmitAsync(tx);
                Assert.True(result.Accepted, result.RejectMessage);

                var bToA = b.OutboundSessionTo(a);
                Assert.NotNull(bToA);

                var bodies = await bToA.GetPooledTransactionsAsync(
                    new List<byte[]> { result.TransactionHash }, CancellationToken.None);

                Assert.Single(bodies);
                Assert.True(ByteUtil.AreEqual(bodies[0].Hash, tx.Hash),
                    "served body hash does not match the submitted transaction");
                Assert.True(ByteUtil.AreEqual(bodies[0].GetRLPEncoded(), tx.GetRLPEncoded()),
                    "served body bytes do not match the submitted transaction");
                _output.WriteLine($"B fetched full body for 0x{result.TransactionHash.ToHex()} from A");
            }
            finally
            {
                await a.DisposeAsync();
                await b.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_TxnFailingAdmission_When_SubmittedToA_Then_Rejected_AndNotAnnounced()
        {
            var (a, b) = await DevChainNetwork.TwoConnectedNodesAsync();
            try
            {
                var tx = DevChainTransactions.SignEip1559(NeverFundedKey, (int)a.ChainId, Recipient, nonce: 0, value: 5000);

                var result = await a.Mempool.SubmitAsync(tx);

                Assert.False(result.Accepted);
                Assert.Equal(MempoolRejectReason.InsufficientBalance, result.RejectReason);
                Assert.Equal(0, a.Mempool.PendingCount);

                var neverPropagated = await DevChainNetwork.StaysFalseAsync(
                    () => ContainsHash(b.ObservedAnnouncements, tx.Hash)
                        || b.ObservedRemoteTransactions.Any(t => ByteUtil.AreEqual(t.Hash, tx.Hash)),
                    TimeSpan.FromSeconds(3));

                Assert.True(neverPropagated, "B observed a propagated (announced or full-body) transaction A rejected");
                Assert.Equal(0, a.Mempool.PendingCount);
                _output.WriteLine($"A rejected unfunded tx ({result.RejectReason}); B saw no announcement");
            }
            finally
            {
                await a.DisposeAsync();
                await b.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_RemoteTransactionsPush_When_ReceivedByA_Then_ANeitherRegossipsNorExecutes()
        {
            var (a, b) = await DevChainNetwork.TwoConnectedNodesAsync();
            try
            {
                var tx = DevChainTransactions.SignEip1559(SenderKey, (int)a.ChainId, Recipient, nonce: 0, value: 7000);

                var bToA = b.OutboundSessionTo(a);
                Assert.NotNull(bToA);
                var ethOffset = bToA.Connection.GetCapabilityOffset("eth");
                var payload = TransactionsMessageEncoder.Encode(
                    new TransactionsMessage { Transactions = new List<ISignedTransaction> { tx } });
                await bToA.Connection.SendMessageAsync(ethOffset + EthMessageIds.Transactions, payload);

                var observed = await DevChainNetwork.WaitUntilAsync(
                    () => a.ObservedRemoteTransactions.Any(t => ByteUtil.AreEqual(t.Hash, tx.Hash)),
                    TimeSpan.FromSeconds(15));
                Assert.True(observed, "A did not receive B's Transactions(0x02) push");

                Assert.Equal(0, a.Mempool.PendingCount);
                var noRegossip = await DevChainNetwork.StaysFalseAsync(
                    () => ContainsHash(b.ObservedAnnouncements, tx.Hash),
                    TimeSpan.FromSeconds(3));
                Assert.True(noRegossip, "A re-gossiped a remote transaction (leaf rule violated)");
                Assert.Equal(0, a.Mempool.PendingCount);
                _output.WriteLine("A observed the remote push, pooled nothing, and re-gossiped nothing");
            }
            finally
            {
                await a.DisposeAsync();
                await b.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_HubWithTwoPeers_When_TxnSubmitted_Then_OnePeerGetsFullBody_AndTheOtherAnAnnouncement()
        {
            var (a, b, c) = await DevChainNetwork.HubWithTwoPeersAsync();
            try
            {
                var sender = DevChainTransactions.AddressOf(SenderKey);
                await a.FundAccountAsync(sender, TenEther);
                var tx = DevChainTransactions.SignEip1559(SenderKey, (int)a.ChainId, Recipient, nonce: 0, value: 1234);

                var result = await a.Mempool.SubmitAsync(tx);
                Assert.True(result.Accepted, result.RejectMessage);

                int FullBodies() => FullBodyCount(b.ObservedRemoteTransactions, tx.Hash)
                    + FullBodyCount(c.ObservedRemoteTransactions, tx.Hash);
                int Announcements() => (ContainsHash(b.ObservedAnnouncements, tx.Hash) ? 1 : 0)
                    + (ContainsHash(c.ObservedAnnouncements, tx.Hash) ? 1 : 0);

                var split = await DevChainNetwork.WaitUntilAsync(
                    () => FullBodies() == 1 && Announcements() == 1,
                    TimeSpan.FromSeconds(15));

                Assert.True(split,
                    $"expected sqrt-fan-out (1 full body + 1 announce) across two peers; got full={FullBodies()} announce={Announcements()}");
            }
            finally
            {
                await a.DisposeAsync();
                await b.DisposeAsync();
                await c.DisposeAsync();
            }
        }
    }
}
