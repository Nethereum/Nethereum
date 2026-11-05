using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RLP.UnitTests
{
    public class RlpLazyRLPDataAliasingSecurityTests
    {
        private static byte[] EncodedTransactionLikeList() =>
            RLP.EncodeList(
                RLP.EncodeList(RLP.EncodeElement(new byte[] {1, 2, 3}), RLP.EncodeElement(new byte[40])),
                RLP.EncodeElement(new byte[] {9}));

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_DecodedFromABufferThatIsThenOverwritten_When_ANestedListRLPDataIsRead_Then_ItStillHoldsTheDecodedBytes()
        {
            var encoded = EncodedTransactionLikeList();
            var expectedInner = RLP.EncodeList(RLP.EncodeElement(new byte[] {1, 2, 3}), RLP.EncodeElement(new byte[40]));
            var receiveBuffer = (byte[]) encoded.Clone();

            var decoded = (RLPCollection) RLP.Decode(receiveBuffer);
            Array.Clear(receiveBuffer, 0, receiveBuffer.Length);

            Assert.Equal(expectedInner, ((RLPCollection) decoded[0]).RLPData);
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_ANetworkReadLoopReusingOneBuffer_When_AnEarlierMessagesNestedListRLPDataIsReadAfterTheNextMessageArrived_Then_ItIsStillTheEarlierMessage()
        {
            var first = RLP.EncodeList(RLP.EncodeList(RLP.EncodeElement(new byte[] {0xAA, 0xBB})));
            var second = RLP.EncodeList(RLP.EncodeList(RLP.EncodeElement(new byte[] {0xCC, 0xDD})));
            Assert.Equal(first.Length, second.Length);
            var reused = new byte[first.Length];

            Buffer.BlockCopy(first, 0, reused, 0, first.Length);
            var firstDecoded = (RLPCollection) RLP.Decode(reused);
            Buffer.BlockCopy(second, 0, reused, 0, second.Length);
            var secondDecoded = (RLPCollection) RLP.Decode(reused);

            Assert.Equal(RLP.EncodeList(RLP.EncodeElement(new byte[] {0xAA, 0xBB})), ((RLPCollection) firstDecoded[0]).RLPData);
            Assert.Equal(RLP.EncodeList(RLP.EncodeElement(new byte[] {0xCC, 0xDD})), ((RLPCollection) secondDecoded[0]).RLPData);
        }

        [Fact]
        public void Given_TwoThreadsReadingRLPDataOfTheSameNestedList_When_ReadConcurrently_Then_BothSeeTheCompleteBytes()
        {
            var inner = RLP.EncodeList(RLP.EncodeElement(new byte[100000]), RLP.EncodeElement(new byte[] {1}));
            var encoded = RLP.EncodeList(inner);

            for (var round = 0; round < 300; round++)
            {
                var decoded = (RLPCollection) RLP.Decode(encoded);
                var nested = (RLPCollection) decoded[0];
                using var gate = new Barrier(2);
                byte[] a = null, b = null;
                var ta = Task.Run(() => { gate.SignalAndWait(); a = nested.RLPData; });
                var tb = Task.Run(() => { gate.SignalAndWait(); b = nested.RLPData; });
                Task.WaitAll(ta, tb);

                Assert.Equal(inner, a);
                Assert.Equal(inner, b);
            }
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_AValueAssignedToRLPDataWhileAnotherThreadMaterialisesIt_When_BothComplete_Then_TheAssignedValueWins()
        {
            var inner = RLP.EncodeList(RLP.EncodeElement(new byte[2000000]));
            var encoded = RLP.EncodeList(inner);
            var assigned = new byte[] {7, 7};
            var lost = 0;

            for (var round = 0; round < 200; round++)
            {
                var nested = (RLPCollection) ((RLPCollection) RLP.Decode(encoded))[0];
                using var gate = new Barrier(2);
                var reader = Task.Run(() => { gate.SignalAndWait(); return nested.RLPData; });
                var writer = Task.Run(() => { gate.SignalAndWait(); Thread.SpinWait(200); nested.RLPData = assigned; });
                Task.WaitAll(reader, writer);
                if (!ReferenceEquals(nested.RLPData, assigned)) lost++;
            }

            Assert.Equal(0, lost);
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_ANestedListWhoseHeaderClaimsMoreBytesThanItsParentHolds_When_Decoded_Then_ItIsRejected()
        {
            var malformed = new byte[] {0xc2, 0xc5, 0x01, 0x02, 0x03, 0x04, 0x05};

            Assert.ThrowsAny<Exception>(() => RLP.Decode(malformed));
        }
    }
}
