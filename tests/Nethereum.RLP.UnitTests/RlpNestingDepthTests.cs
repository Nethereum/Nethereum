using System;
using System.Diagnostics;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.RLP.UnitTests
{
    public class RlpNestingDepthTests
    {
        private static byte[] NestedLists(int depth)
        {
            var encoded = RLP.EncodeList();
            for (var i = 1; i < depth; i++)
                encoded = RLP.EncodeList(encoded);
            return encoded;
        }

        private static int Depth(IRLPElement element)
        {
            var depth = 0;
            while (element is RLPCollection collection && collection.Count > 0)
            {
                depth++;
                element = collection[0];
            }
            return depth;
        }

        [Fact]
        public void Given_ListsNestedExactlyAtTheLimit_When_Decoded_Then_TheyDecode()
        {
            var encoded = NestedLists(RLP.MaxDecodeDepth);

            var decoded = RLP.Decode(encoded);

            Assert.IsType<RLPCollection>(decoded);
        }

        [Fact]
        public void Given_ListsNestedOneBeyondTheLimit_When_Decoded_Then_TheInvalidRlpExceptionIsThrown()
        {
            var encoded = NestedLists(RLP.MaxDecodeDepth + 1);

            var ex = Assert.Throws<Exception>(() => RLP.Decode(encoded));

            Assert.StartsWith("Invalid RLP", ex.Message);
            Assert.NotNull(ex.InnerException);
        }

        [Fact]
        public void Given_AMebibyteOfNestedListHeaders_When_Decoded_Then_ItThrowsQuicklyWithBoundedAllocation()
        {
            var payload = new byte[1024 * 1024];
            Array.Fill(payload, (byte)0xc1);

            var before = GC.GetAllocatedBytesForCurrentThread();
            var clock = Stopwatch.StartNew();
            var ex = Assert.Throws<Exception>(() => RLP.Decode(payload));
            clock.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.StartsWith("Invalid RLP", ex.Message);
            Assert.True(ex.Message.Length < 1024, "error message must be bounded");
            Assert.True(clock.ElapsedMilliseconds < 1000, "took " + clock.ElapsedMilliseconds + " ms");
            Assert.True(allocated < 32L * 1024 * 1024, "allocated " + allocated);
        }

        [Fact]
        public void Given_AMebibyteOfNestedListHeaders_When_DecodedAsCollectionOrFirstElement_Then_BothThrowTheInvalidRlpException()
        {
            var payload = new byte[1024 * 1024];
            Array.Fill(payload, (byte)0xc1);

            Assert.Throws<Exception>(() => RLP.DecodeCollection(payload));
            Assert.Throws<Exception>(() => RLP.DecodeFirstElement(payload, 0));
        }

        [Fact]
        public void Given_APayloadWhoseFirstItemIsAList_When_ReadingTheLeadingRequestId_Then_ItReturnsFalse()
        {
            var payload = new byte[1024 * 1024];
            Array.Fill(payload, (byte)0xc1);

            Assert.False(RLP.TryReadLeadingListUInt64(payload, out var value));
            Assert.Equal(0UL, value);
        }

        [Fact]
        public void Given_ARequestIdList_When_ReadingTheLeadingRequestId_Then_ItReturnsTheId()
        {
            var requestId = RLP.EncodeElement(new byte[] { 0x01, 0x02, 0x03 });
            var payload = RLP.EncodeList(requestId, RLP.EncodeList(RLP.EncodeElement(new byte[] { 0x07 })));

            Assert.True(RLP.TryReadLeadingListUInt64(payload, out var value));
            Assert.Equal(0x010203UL, value);
        }

        [Fact]
        public void Given_SingleByteAndLongRequestIds_When_ReadingTheLeadingRequestId_Then_ValuesAreRead()
        {
            Assert.True(RLP.TryReadLeadingListUInt64(RLP.EncodeList(new byte[] { 0x05 }), out var small));
            Assert.Equal(5UL, small);

            var eight = RLP.EncodeList(RLP.EncodeElement(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.True(RLP.TryReadLeadingListUInt64(eight, out var big));
            Assert.Equal(0x0102030405060708UL, big);
        }

        [Theory]
        [InlineData("c1")]
        [InlineData("c2")]
        [InlineData("c2c0")]
        [InlineData("c283")]
        [InlineData("c283aabb")]
        [InlineData("c980")]
        [InlineData("c0")]
        [InlineData("f8")]
        [InlineData("c2b8")]
        public void Given_TruncatedOrDegenerateHeaders_When_ReadingTheLeadingRequestId_Then_ItReturnsFalseWithoutThrowing(string hex)
        {
            Assert.False(RLP.TryReadLeadingListUInt64(hex.HexToByteArray(), out _));
        }

        [Fact]
        public void Given_ABlockBodyAndReceiptShapedLists_When_EncodedAndDecoded_Then_TheyRoundTripUnchanged()
        {
            var accessList = RLP.EncodeList(RLP.EncodeList(
                RLP.EncodeElement(new byte[20].Fill(0xaa)),
                RLP.EncodeList(RLP.EncodeElement(new byte[32].Fill(0x01)), RLP.EncodeElement(new byte[32].Fill(0x02)))));
            var tx = RLP.EncodeList(
                RLP.EncodeElement(new byte[] { 1 }), RLP.EncodeElement(new byte[] { 9 }),
                RLP.EncodeElement(new byte[20].Fill(0xbb)), RLP.EncodeElement(new byte[] { 0x64 }),
                RLP.EncodeElement(new byte[100].Fill(0xcd)), accessList);
            var header = RLP.EncodeList(RLP.EncodeElement(new byte[32].Fill(0x11)), RLP.EncodeElement(new byte[256].Fill(0x22)));
            var ommers = RLP.EncodeList();
            var body = RLP.EncodeList(RLP.EncodeList(tx, tx), ommers);
            var log = RLP.EncodeList(RLP.EncodeElement(new byte[20].Fill(0xee)),
                RLP.EncodeList(RLP.EncodeElement(new byte[32].Fill(0x33))), RLP.EncodeElement(new byte[40].Fill(0x44)));
            var receipt = RLP.EncodeList(RLP.EncodeElement(new byte[] { 1 }), RLP.EncodeElement(new byte[] { 0x5a }),
                RLP.EncodeElement(new byte[256].Fill(0x55)), RLP.EncodeList(log, log));
            var blockAndReceipts = RLP.EncodeList(header, body, receipt);

            var decoded = (RLPCollection)RLP.Decode(blockAndReceipts);

            Assert.Equal(3, decoded.Count);
            Assert.Equal(blockAndReceipts, decoded.RLPData);
            Assert.Equal(header, decoded[0].RLPData);
            Assert.Equal(body, decoded[1].RLPData);
            Assert.Equal(receipt, decoded[2].RLPData);
            Assert.True(Depth(decoded) <= 6);
        }
    }

    internal static class ByteFill
    {
        public static byte[] Fill(this byte[] array, byte value)
        {
            Array.Fill(array, value);
            return array;
        }
    }
}
