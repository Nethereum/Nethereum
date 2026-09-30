using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Chain.TestData;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    /// <summary>
    /// Every EIP-2718 transaction type this node can sign, carried end to end over a real two-node
    /// devchain: signed, admitted through the production mempool validator, announced over loopback
    /// RLPx, and served back on request. One scenario per type, so a type that stops round-tripping
    /// names itself.
    /// </summary>
    public class TransactionTypePropagationE2ETests
    {
        private static readonly ChainAccount SenderAccount = ChainAccounts.Sender;
        private static readonly ChainAccount SecondSenderAccount = ChainAccounts.SecondSender;
        private static readonly string SenderKey = ChainAccounts.Sender.PrivateKey;
        private static readonly string SecondSenderKey = ChainAccounts.SecondSender.PrivateKey;
        private static readonly string Recipient = ChainAccounts.Recipient.Address;
        private static readonly BigInteger TenEther = ChainAccounts.DefaultBalance;

        private readonly ITestOutputHelper _output;
        public TransactionTypePropagationE2ETests(ITestOutputHelper output) => _output = output;

        public static IEnumerable<object[]> TransactionTypes() => TransactionScenarios.AsMemberData();

        [Theory]
        [MemberData(nameof(TransactionTypes))]
        public async Task Given_ATransactionOfEachType_When_SubmittedToA_Then_ItIsAdmittedAndAnnouncedToB(string type)
        {
            var (a, b) = await DevChainNetwork.TwoConnectedNodesAsync();
            try
            {
                await a.FundAccountAsync(SenderAccount.Address, TenEther);

                var tx = Sign(type, SenderKey, a.ChainId, nonce: 0);
                var result = await a.Mempool.SubmitAsync(tx);

                Assert.True(result.Accepted, $"{type} rejected: {result.RejectMessage}");
                Assert.Equal(1, a.Mempool.PendingCount);

                var announced = await DevChainNetwork.WaitUntilAsync(
                    () => b.ObservedTransaction(result.TransactionHash),
                    TimeSpan.FromSeconds(15));

                Assert.True(announced, $"B was neither sent the {type} body nor told its hash");
                _output.WriteLine($"{type} admitted and announced 0x{result.TransactionHash.ToHex()}");
            }
            finally
            {
                await a.DisposeAsync();
                await b.DisposeAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TransactionTypes))]
        public async Task Given_AnAnnouncedTransactionOfEachType_When_BRequestsIt_Then_AServesAByteIdenticalBody(string type)
        {
            var (a, b) = await DevChainNetwork.TwoConnectedNodesAsync();
            try
            {
                await a.FundAccountAsync(SenderAccount.Address, TenEther);

                var tx = Sign(type, SenderKey, a.ChainId, nonce: 0);
                var result = await a.Mempool.SubmitAsync(tx);
                Assert.True(result.Accepted, $"{type} rejected: {result.RejectMessage}");

                await DevChainNetwork.WaitUntilAsync(
                    () => b.ObservedTransaction(result.TransactionHash),
                    TimeSpan.FromSeconds(15));

                var session = b.OutboundSessionTo(a);
                var served = await session.GetPooledTransactionsAsync(
                    new List<byte[]> { result.TransactionHash }, default);

                Assert.NotNull(served);
                Assert.Single(served);
                Assert.Equal(tx.GetRLPEncoded().ToHex(), served[0].GetRLPEncoded().ToHex());
                _output.WriteLine($"{type} served back byte-identical");
            }
            finally
            {
                await a.DisposeAsync();
                await b.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_EveryTransactionTypeFromOneSender_When_SubmittedInNonceOrder_Then_AllAreAdmittedAndAnnounced()
        {
            var (a, b) = await DevChainNetwork.TwoConnectedNodesAsync();
            try
            {
                await a.FundAccountAsync(SenderAccount.Address, TenEther);

                var hashes = new List<byte[]>();
                BigInteger nonce = 0;
                foreach (var type in TransactionScenarios.All.Select(t => t.Name))
                {
                    var result = await a.Mempool.SubmitAsync(Sign(type, SenderKey, a.ChainId, nonce));
                    Assert.True(result.Accepted, $"{type} at nonce {nonce} rejected: {result.RejectMessage}");
                    hashes.Add(result.TransactionHash);
                    nonce += 1;
                }

                Assert.Equal(TransactionScenarios.All.Count, a.Mempool.PendingCount);

                var allAnnounced = await DevChainNetwork.WaitUntilAsync(
                    () => hashes.All(h => b.ObservedTransaction(h)),
                    TimeSpan.FromSeconds(20));

                Assert.True(allAnnounced, "B was not told about every transaction, by either path");
                _output.WriteLine("all four transaction types admitted and announced from one sender");
            }
            finally
            {
                await a.DisposeAsync();
                await b.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_TwoSendersEachSubmittingToADifferentNode_When_BothAnnounce_Then_EachNodeObservesTheOther()
        {
            var (a, b) = await DevChainNetwork.TwoConnectedNodesAsync();
            try
            {
                await a.FundAccountAsync(SenderAccount.Address, TenEther);
                await b.FundAccountAsync(SecondSenderAccount.Address, TenEther);

                var fromA = await a.Mempool.SubmitAsync(
                    DevChainTransactions.SignEip1559(SenderKey, a.ChainId, Recipient, 0, 1000));
                var fromB = await b.Mempool.SubmitAsync(
                    DevChainTransactions.SignEip1559(SecondSenderKey, b.ChainId, Recipient, 0, 2000));

                Assert.True(fromA.Accepted, fromA.RejectMessage);
                Assert.True(fromB.Accepted, fromB.RejectMessage);

                var crossed = await DevChainNetwork.WaitUntilAsync(
                    () => b.ObservedTransaction(fromA.TransactionHash)
                       && a.ObservedTransaction(fromB.TransactionHash),
                    TimeSpan.FromSeconds(20));

                Assert.True(crossed, "announcements did not cross in both directions");
                _output.WriteLine("both directions announced");
            }
            finally
            {
                await a.DisposeAsync();
                await b.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AnUnfundedSender_When_ATransactionIsSubmitted_Then_ItIsRejectedAndNothingIsAnnounced()
        {
            var (a, b) = await DevChainNetwork.TwoConnectedNodesAsync();
            try
            {
                var tx = DevChainTransactions.SignEip1559(
                    Nethereum.Chain.TestData.ChainAccounts.Unfunded.PrivateKey, a.ChainId, Recipient, 0, 1000);

                var result = await a.Mempool.SubmitAsync(tx);

                Assert.False(result.Accepted);
                Assert.Equal(0, a.Mempool.PendingCount);

                var silent = await DevChainNetwork.StaysFalseAsync(
                    () => b.ObservedAnnouncements.Any(h => ByteUtil.AreEqual(h, tx.Hash)),
                    TimeSpan.FromSeconds(3));

                Assert.True(silent, "a rejected transaction was announced to B");
                _output.WriteLine($"unfunded sender rejected: {result.RejectMessage}");
            }
            finally
            {
                await a.DisposeAsync();
                await b.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_ATransactionSignedForAnotherChain_When_Submitted_Then_ItIsRejected()
        {
            var (a, b) = await DevChainNetwork.TwoConnectedNodesAsync();
            try
            {
                await a.FundAccountAsync(SenderAccount.Address, TenEther);

                var wrongChain = DevChainTransactions.SignEip1559(
                    SenderKey, a.ChainId + 1, Recipient, 0, 1000);

                var result = await a.Mempool.SubmitAsync(wrongChain);

                Assert.False(result.Accepted);
                Assert.Equal(0, a.Mempool.PendingCount);
                _output.WriteLine($"wrong chain id rejected: {result.RejectMessage}");
            }
            finally
            {
                await a.DisposeAsync();
                await b.DisposeAsync();
            }
        }

        private static ISignedTransaction Sign(string scenarioName, string key, ulong chainId, BigInteger nonce)
            => TransactionScenarios.ByName(scenarioName).Build(
                new TransactionScenarioContext(chainId, SenderAccount, Recipient, nonce));
    }
}
