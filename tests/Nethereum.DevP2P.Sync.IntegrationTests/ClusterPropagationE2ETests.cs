using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class ClusterPropagationE2ETests
    {
        private const string SenderKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string Recipient = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private static readonly System.Numerics.BigInteger TenEther =
            System.Numerics.BigInteger.Parse("10000000000000000000");

        private readonly ITestOutputHelper _output;

        public ClusterPropagationE2ETests(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task Given_AThreeNodeTrustedCluster_When_ATransactionIsPushedToOneNode_Then_EveryOtherNodeAdmitsIt()
        {
            var nodes = await DevChainNetwork.TrustedClusterAsync(3);
            try
            {
                var sender = new EthECKey(SenderKey).GetPublicAddress();
                foreach (var node in nodes) await node.FundAccountAsync(sender, TenEther);

                var source = nodes[0];
                var receivers = nodes.Skip(1).ToList();
                var tx = DevChainTransactions.SignEip1559(SenderKey, (int)source.ChainId, Recipient, nonce: 0, value: 7000);

                foreach (var receiver in receivers)
                    await PushAsync(source, receiver, tx);

                foreach (var receiver in receivers)
                {
                    var admitted = await DevChainNetwork.WaitUntilAsync(
                        () => receiver.Mempool.PendingCount > 0,
                        TimeSpan.FromSeconds(15));

                    Assert.Null(receiver.TrustedAdmissionError);
                    Assert.True(admitted,
                        $"a cluster sibling did not admit the pushed transaction " +
                        $"(trustedPushes={receiver.TrustedPushCount}, admitted={receiver.TrustedAdmittedCount})");
                }

                _output.WriteLine($"all {receivers.Count} sibling(s) admitted the transaction pushed by node 0");
            }
            finally
            {
                foreach (var node in nodes) await node.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AThreeNodeTrustedCluster_When_ATransactionIsAdmitted_Then_ItIsNotReGossipedOnward()
        {
            var nodes = await DevChainNetwork.TrustedClusterAsync(3);
            try
            {
                var sender = new EthECKey(SenderKey).GetPublicAddress();
                foreach (var node in nodes) await node.FundAccountAsync(sender, TenEther);

                var source = nodes[0];
                var middle = nodes[1];
                var far = nodes[2];
                var tx = DevChainTransactions.SignEip1559(SenderKey, (int)source.ChainId, Recipient, nonce: 0, value: 7000);

                await PushAsync(source, middle, tx);

                var admitted = await DevChainNetwork.WaitUntilAsync(
                    () => middle.Mempool.PendingCount > 0,
                    TimeSpan.FromSeconds(15));
                Assert.True(admitted, "the middle node did not admit the pushed transaction");

                var noOnwardGossip = await DevChainNetwork.StaysFalseAsync(
                    () => far.ObservedAnnouncements.Any(h => ByteUtil.AreEqual(h, tx.Hash)),
                    TimeSpan.FromSeconds(3));

                Assert.True(noOnwardGossip,
                    "the middle node re-gossiped an admitted transaction onward — admission must not re-announce");
                _output.WriteLine("middle node admitted without re-announcing to the far node");
            }
            finally
            {
                foreach (var node in nodes) await node.DisposeAsync();
            }
        }

        private static async Task PushAsync(DevChainNode from, DevChainNode to, ISignedTransaction tx)
        {
            var session = from.OutboundSessionTo(to);
            Assert.NotNull(session);

            var ethOffset = session.Connection.GetCapabilityOffset("eth");
            var payload = TransactionsMessageEncoder.Encode(
                new TransactionsMessage { Transactions = new List<ISignedTransaction> { tx } });

            await session.Connection.SendMessageAsync(ethOffset + EthMessageIds.Transactions, payload);
        }
    }
}
