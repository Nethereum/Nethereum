using System;
using System.Linq;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.SecurityAudit71.S2.PartB
{
    public class S2DecodeCapTests
    {
        private static byte[] ReceiptsReply(int receiptsInTheBlock)
        {
            var junk = Enumerable.Repeat(RLP.RLP.EncodeElement(new byte[] { 0x01 }), receiptsInTheBlock).ToArray();
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(new byte[] { 0x01 }),
                RLP.RLP.EncodeList(RLP.RLP.EncodeList(junk)));
        }

        private static byte[] AccountRangeReply(int proofNodes)
        {
            var proof = Enumerable.Range(0, proofNodes).Select(_ => RLP.RLP.EncodeElement(new byte[] { 0x01 })).ToArray();
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(new byte[] { 0x01 }),
                RLP.RLP.EncodeList(),
                RLP.RLP.EncodeList(proof));
        }

        [Fact]
        public void S2_DB3_Given_ABlockOfReceiptsLongerThanAnyBlockCanHold_When_TheReplyIsDecoded_Then_ItIsRefusedByTheCountBeforeAnyReceiptIsRead()
        {
            var fault = Assert.ThrowsAny<Exception>(
                () => ReceiptsMessageEncoder.Decode(ReceiptsReply(PeerResponseBounds.MaxTransactionsPerBlock + 1)));

            Assert.Contains("receipts list holds more than", fault.Message);
        }

        [Fact]
        public void S2_DB6_Given_AnAccountRangeWithMoreProofNodesThanTheLimit_When_TheReplyIsDecoded_Then_ItIsRefusedByTheCount()
        {
            var fault = Assert.ThrowsAny<Exception>(
                () => AccountRangeMessageEncoder.Decode(AccountRangeReply(SnapServingLimits.MaxResponseProofNodes + 1)));

            Assert.Contains("holds more than " + SnapServingLimits.MaxResponseProofNodes + " items", fault.Message);
        }

        [Fact]
        public void S2_DB6_Given_AnAccountRangeWithExactlyTheLimitOfProofNodes_When_TheReplyIsDecoded_Then_ItIsAccepted()
        {
            var message = AccountRangeMessageEncoder.Decode(AccountRangeReply(SnapServingLimits.MaxResponseProofNodes));

            Assert.Equal(SnapServingLimits.MaxResponseProofNodes, message.Proof.Count);
        }
    }
}
