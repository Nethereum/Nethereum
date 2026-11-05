using System;
using Xunit;

namespace Nethereum.RLP.UnitTests
{
    public class RlpDecodeAmplificationSecurityTests
    {
        private const int BlobBytes = 4 * 1024 * 1024;

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_BlobWrappedInMaxDecodeDepthLists_When_Decoded_Then_RetainedBytesStayWithinAFewTimesTheInput()
        {
            var encoded = RLP.EncodeElement(new byte[BlobBytes]);
            for (var i = 0; i < RLP.MaxDecodeDepth; i++)
                encoded = RLP.EncodeList(encoded);

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var decoded = RLP.Decode(encoded);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            GC.KeepAlive(decoded);

            Assert.True(allocated <= 4L * encoded.Length,
                $"input {encoded.Length} bytes, decoding allocated {allocated} bytes ({allocated / (double)encoded.Length:0.0}x)");
        }

        [Fact]
        public void Given_NestedLists_When_RLPDataIsRead_Then_EveryLevelReturnsItsOwnEncodingAndIsCached()
        {
            var inner = RLP.EncodeList(RLP.EncodeElement(new byte[] {1, 2, 3}), RLP.EncodeElement(new byte[70]));
            var middle = RLP.EncodeList(inner, RLP.EncodeElement(new byte[] {9}));
            var outer = RLP.EncodeList(middle);

            var decoded = (RLPCollection) RLP.Decode(outer);
            var decodedMiddle = (RLPCollection) decoded[0];
            var decodedInner = (RLPCollection) decodedMiddle[0];

            Assert.Equal(outer, decoded.RLPData);
            Assert.Equal(middle, decodedMiddle.RLPData);
            Assert.Equal(inner, decodedInner.RLPData);
            Assert.Same(decodedInner.RLPData, decodedInner.RLPData);
        }

        [Fact]
        public void Given_DecodedList_When_RLPDataIsSet_Then_TheAssignedValueIsReturned()
        {
            var decoded = (RLPCollection) RLP.Decode(RLP.EncodeList(RLP.EncodeElement(new byte[] {1})));
            var assigned = new byte[] {7, 7};

            decoded.RLPData = assigned;
            Assert.Same(assigned, decoded.RLPData);

            decoded.RLPData = null;
            Assert.Null(decoded.RLPData);
        }

        [Fact]
        public void Given_ListDeclaringMoreBytesThanTheInput_When_Decoded_Then_InvalidRlpIsThrown()
        {
            var ex = Assert.Throws<Exception>(() => RLP.Decode(new byte[] {0xc8, 0x01, 0x02}));
            Assert.StartsWith("Invalid RLP", ex.Message);
        }

        [Fact]
        public void Given_NullOrEmptyInput_When_Decoded_Then_InvalidRlpIsThrown()
        {
            Assert.StartsWith("Invalid RLP", Assert.Throws<Exception>(() => RLP.Decode(new byte[0])).Message);
            Assert.StartsWith("Invalid RLP", Assert.Throws<Exception>(() => RLP.Decode((byte[]) null)).Message);
            Assert.StartsWith("Invalid RLP", Assert.Throws<Exception>(() => RLP.DecodeFirstElement(new byte[0], 0)).Message);
            Assert.StartsWith("Invalid RLP", Assert.Throws<Exception>(() => RLP.DecodeFirstElement(null, 0)).Message);
            Assert.StartsWith("Invalid RLP", Assert.Throws<Exception>(() => RLP.DecodeCollection(null)).Message);
        }

        [Fact]
        public void Given_EmptyInput_When_DecodedAsCollection_Then_AnEmptyCollectionIsReturned()
        {
            var decoded = Assert.IsType<RLPCollection>(RLP.DecodeCollection(new byte[0]));
            Assert.Empty(decoded);
        }
    }
}
