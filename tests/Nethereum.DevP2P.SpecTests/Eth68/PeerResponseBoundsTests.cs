using System.Collections.Generic;
using Nethereum.Model.P2P;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Eth68
{
    public class PeerResponseBoundsTests
    {
        private static readonly byte[] EmptyBody =
            RLP.RLP.EncodeList(RLP.RLP.EncodeList(), RLP.RLP.EncodeList());

        private static byte[] BodyWithTransactionCount(int count)
        {
            var txs = new byte[count][];
            for (var i = 0; i < count; i++) txs[i] = new byte[] { 0x01 };
            return RLP.RLP.EncodeList(RLP.RLP.EncodeList(txs), RLP.RLP.EncodeList());
        }

        private static byte[] BodiesMessage(params byte[][] bodies) =>
            RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)7).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(bodies));

        private static RLPCollection ListOf(int count)
        {
            var items = new byte[count][];
            for (var i = 0; i < count; i++) items[i] = new byte[] { 0x01 };
            return (RLPCollection)RLP.RLP.Decode(RLP.RLP.EncodeList(items));
        }

        [Fact]
        public void Given_ExactlyAsManyItemsAsABlockCanHold_When_Bounded_Then_ItIsAllowed()
        {
            Assert.False(PeerResponseBounds.ExceedsWhatABlockCanHold(
                ListOf(PeerResponseBounds.MaxTransactionsPerBlock)));
        }

        [Fact]
        public void Given_OneMoreItemThanABlockCanHold_When_Bounded_Then_ItIsRefused()
        {
            Assert.True(PeerResponseBounds.ExceedsWhatABlockCanHold(
                ListOf(PeerResponseBounds.MaxTransactionsPerBlock + 1)));
        }

        [Fact]
        public void Given_TheBound_Then_ItClearsTheLargestRealBlockByOverTwentyFold()
        {
            const int transactionsInA36MGasBlock = 36000000 / PeerResponseBounds.MinimumTransactionGas;
            Assert.True(PeerResponseBounds.MaxTransactionsPerBlock > transactionsInA36MGasBlock * 20,
                $"bound {PeerResponseBounds.MaxTransactionsPerBlock} leaves too little headroom " +
                $"over {transactionsInA36MGasBlock}");
            Assert.True(PeerResponseBounds.MaxTransactionsPerBlock < 65535,
                "the bound must refuse the 65535-transaction body that was actually served");
        }

        [Fact]
        public void Given_AResponseEndingInAnImplausibleBody_When_Decoded_Then_TheGoodBodiesSurvive()
        {
            var msg = BlockBodiesMessageEncoder.Decode(BodiesMessage(
                EmptyBody,
                EmptyBody,
                BodyWithTransactionCount(PeerResponseBounds.MaxTransactionsPerBlock + 1)));

            Assert.Equal(2, msg.Bodies.Count);
        }

        [Fact]
        public void Given_AnImplausibleBodyInTheMiddle_When_Decoded_Then_NothingAfterItIsReturned()
        {
            var msg = BlockBodiesMessageEncoder.Decode(BodiesMessage(
                EmptyBody,
                BodyWithTransactionCount(PeerResponseBounds.MaxTransactionsPerBlock + 1),
                EmptyBody,
                EmptyBody));

            Assert.Single(msg.Bodies);
        }

        [Fact]
        public void Given_EveryBodyIsPlausible_When_Decoded_Then_AllOfThemAreReturned()
        {
            var msg = BlockBodiesMessageEncoder.Decode(
                BodiesMessage(EmptyBody, EmptyBody, EmptyBody, EmptyBody));

            Assert.Equal(4, msg.Bodies.Count);
        }
    }
}
