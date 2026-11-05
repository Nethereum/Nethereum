using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.RLP.UnitTests
{
    public class RlpReaderTests
    {
        private static byte[] Hex(string hex) => hex.HexToByteArray();

        private static string Walk(RlpReader reader)
        {
            if (reader.PeekKind() != RlpKind.List)
                return reader.ReadBytes().ToHex();

            var scope = reader.ReadListHeader();
            var items = new List<string>();
            while (!reader.IsAtEnd(scope))
                items.Add(Walk(reader));
            reader.EnsureEnd(scope);
            return "[" + string.Join(",", items) + "]";
        }

        private static int Consume(RlpReader reader)
        {
            if (reader.PeekKind() != RlpKind.List)
            {
                reader.ReadBytes();
                return 1;
            }

            var count = 1;
            var scope = reader.ReadListHeader();
            while (!reader.IsAtEnd(scope))
                count += Consume(reader);
            reader.EnsureEnd(scope);
            return count;
        }

        private static string WalkWhole(byte[] data)
        {
            var reader = new RlpReader(data);
            var walked = Walk(reader);
            reader.EnsureEnd();
            return walked;
        }

        private static string Flatten(IRLPElement element)
        {
            if (element is RLPCollection collection)
            {
                var items = new List<string>();
                foreach (var child in collection) items.Add(Flatten(child));
                return "[" + string.Join(",", items) + "]";
            }
            return (element.RLPData ?? new byte[0]).ToHex();
        }

        [Theory]
        [InlineData("8100")]
        [InlineData("8101")]
        [InlineData("817f")]
        [InlineData("b801ff")]
        [InlineData("b837" + "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000")]
        [InlineData("b90001ff")]
        [InlineData("bf0f000000000000021111")]
        [InlineData("ff0f000000000000021111")]
        [InlineData("f80180")]
        [InlineData("f80100")]
        [InlineData("fb0000004000")]
        [InlineData("c1")]
        [InlineData("81")]
        [InlineData("c3c2c0")]
        [InlineData("b8")]
        [InlineData("c2b8")]
        public void Given_AnInvalidRlpVector_When_WalkedWithTheReader_Then_ItIsRejectedLikeTheTreeDecoder(string hex)
        {
            var data = Hex(hex);

            Assert.ThrowsAny<Exception>(() => RLP.Decode(data));
            Assert.Throws<InvalidOperationException>(() => WalkWhole(data));
        }

        [Fact]
        public void Given_BytesAfterTheTopLevelValue_When_EnsureEndIsCalled_Then_ItIsRejected()
        {
            var reader = new RlpReader(Hex("c0c0"));
            var scope = reader.ReadListHeader();
            reader.EnsureEnd(scope);

            Assert.Throws<InvalidOperationException>(() => reader.EnsureEnd());
        }

        [Fact]
        public void Given_AListWithAnUnreadElement_When_EnsureEndOfTheScopeIsCalled_Then_ItIsRejected()
        {
            var reader = new RlpReader(Hex("c20102"));
            var scope = reader.ReadListHeader();
            reader.ReadUInt64();

            Assert.Throws<InvalidOperationException>(() => reader.EnsureEnd(scope));
        }

        [Fact]
        public void Given_AnElementLargerThanItsContainingList_When_ItIsRead_Then_ItIsRejectedEvenIfTheInputHoldsMoreBytes()
        {
            var reader = new RlpReader(Hex("c283aabbccdd"));
            reader.ReadListHeader();

            Assert.Throws<InvalidOperationException>(() => reader.ReadBytes());
        }

        [Fact]
        public void Given_ReadingPastTheEndOfAList_When_AnotherElementIsRead_Then_ItIsRejected()
        {
            var reader = new RlpReader(Hex("c101" + "02"));
            var scope = reader.ReadListHeader();
            reader.ReadUInt64();

            Assert.True(reader.IsAtEnd(scope));
            Assert.Throws<InvalidOperationException>(() => reader.ReadUInt64());
        }

        [Fact]
        public void Given_ListsNestedAtTheDepthLimit_When_Read_Then_TheyAreAcceptedAndOneMoreIsRejected()
        {
            var atLimit = RLP.EncodeList();
            for (var i = 1; i < RLP.MaxDecodeDepth; i++)
                atLimit = RLP.EncodeList(atLimit);
            var overLimit = RLP.EncodeList(atLimit);

            Assert.NotNull(WalkWhole(atLimit));
            Assert.Throws<InvalidOperationException>(() => WalkWhole(overLimit));
        }

        [Fact]
        public void Given_NestedListsClosedAndReopened_When_Read_Then_TheDepthCounterIsRestored()
        {
            var one = RLP.EncodeList(RLP.EncodeList());
            var items = new byte[RLP.MaxDecodeDepth * 3][];
            for (var i = 0; i < items.Length; i++) items[i] = one;
            var reader = new RlpReader(RLP.EncodeList(items));
            var outer = reader.ReadListHeader();
            for (var i = 0; i < items.Length; i++)
            {
                var scope = reader.ReadListHeader();
                var inner = reader.ReadListHeader();
                reader.EnsureEnd(inner);
                reader.EnsureEnd(scope);
            }
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
        }

        [Theory]
        [InlineData("80", 0UL)]
        [InlineData("01", 1UL)]
        [InlineData("7f", 127UL)]
        [InlineData("8180", 128UL)]
        [InlineData("8203e8", 1000UL)]
        [InlineData("88ffffffffffffffff", ulong.MaxValue)]
        public void Given_ACanonicalInteger_When_ReadAsUInt64_Then_TheValueIsReturned(string hex, ulong expected)
        {
            var reader = new RlpReader(Hex(hex));

            Assert.Equal(expected, reader.ReadUInt64());
            reader.EnsureEnd();
        }

        [Theory]
        [InlineData("00")]
        [InlineData("820001")]
        [InlineData("8100")]
        [InlineData("890100000000000000" + "00")]
        [InlineData("c0")]
        public void Given_ANonCanonicalOrOversizedInteger_When_ReadAsUInt64_Then_ItIsRejected(string hex)
        {
            var reader = new RlpReader(Hex(hex));

            Assert.Throws<InvalidOperationException>(() => reader.ReadUInt64());
        }

        [Fact]
        public void Given_ABigInteger_When_Read_Then_TheValueMatchesAndLeadingZerosAreRejected()
        {
            var value = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935");
            var encoded = RLP.EncodeElement(value.ToBytesForRLPEncoding());

            Assert.Equal(value, new RlpReader(encoded).ReadBigInteger());
            Assert.Equal(BigInteger.Zero, new RlpReader(Hex("80")).ReadBigInteger());
            Assert.Throws<InvalidOperationException>(() => new RlpReader(Hex("820001")).ReadBigInteger());
        }

        [Fact]
        public void Given_FixedLengthStrings_When_ReadFixed_Then_OnlyTheExactLengthIsAccepted()
        {
            var hash = new byte[32];
            hash[0] = 0xaa;
            var encoded = RLP.EncodeElement(hash);

            Assert.Equal(hash, new RlpReader(encoded).ReadFixed(32));
            Assert.Throws<InvalidOperationException>(() => new RlpReader(encoded).ReadFixed(31));
            Assert.Throws<InvalidOperationException>(() => new RlpReader(encoded).ReadFixed(33));
            Assert.Throws<InvalidOperationException>(() => new RlpReader(Hex("c0")).ReadFixed(0));
        }

        [Fact]
        public void Given_AStringWhereAListIsExpectedAndViceVersa_When_Read_Then_TheyAreRejected()
        {
            Assert.Throws<InvalidOperationException>(() => new RlpReader(Hex("83646f67")).ReadListHeader());
            Assert.Throws<InvalidOperationException>(() => new RlpReader(Hex("c0")).ReadBytes());
            Assert.Throws<InvalidOperationException>(() => new RlpReader(Hex("c0")).ReadBigInteger());
        }

        [Theory]
        [InlineData("80", false)]
        [InlineData("01", true)]
        public void Given_ABoolean_When_Read_Then_OnlyZeroAndOneAreAccepted(string hex, bool expected)
        {
            Assert.Equal(expected, new RlpReader(Hex(hex)).ReadBool());
        }

        [Fact]
        public void Given_ABooleanAboveOne_When_Read_Then_ItIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => new RlpReader(Hex("02")).ReadBool());
        }

        [Fact]
        public void Given_EachElementKind_When_PeekKindIsCalled_Then_TheKindIsReportedWithoutConsuming()
        {
            var reader = new RlpReader(RLP.EncodeList(RLP.EncodeElement(new byte[] { 0x05 }),
                RLP.EncodeElement(new byte[] { 0xaa, 0xbb }), RLP.EncodeList()));
            reader.ReadListHeader();

            Assert.Equal(RlpKind.Byte, reader.PeekKind());
            Assert.Equal(RlpKind.Byte, reader.PeekKind());
            reader.Skip();
            Assert.Equal(RlpKind.String, reader.PeekKind());
            Assert.Equal(2, reader.PeekSize());
            reader.Skip();
            Assert.Equal(RlpKind.List, reader.PeekKind());
        }

        [Fact]
        public void Given_AnElement_When_ReadRaw_Then_TheExactEncodingIsReturned()
        {
            var inner = RLP.EncodeList(RLP.EncodeElement(new byte[] { 0xaa, 0xbb }), RLP.EncodeElement(new byte[0]));
            var message = RLP.EncodeList(inner, RLP.EncodeElement(new byte[] { 0x01, 0x02 }));
            var reader = new RlpReader(message);
            reader.ReadListHeader();

            var segment = reader.ReadRawSegment();

            Assert.Equal(inner.Length, segment.Length);
            Assert.Equal(inner, new RlpReader(message, segment.Offset, segment.Length).ReadRaw());
            Assert.Equal(RLP.EncodeElement(new byte[] { 0x01, 0x02 }), reader.ReadRaw());
        }

        [Fact]
        public void Given_AListOfItems_When_CountItemsIsCalled_Then_ItMatchesTheTreeAndDoesNotMoveTheReader()
        {
            var items = new byte[5][];
            for (var i = 0; i < items.Length; i++)
                items[i] = i % 2 == 0 ? RLP.EncodeElement(new byte[i + 1]) : RLP.EncodeList(RLP.EncodeElement(new byte[] { 0xff }));
            var reader = new RlpReader(RLP.EncodeList(items));
            var scope = reader.ReadListHeader();
            var position = reader.Position;

            Assert.Equal(5, reader.CountItems(scope));
            Assert.Equal(position, reader.Position);
        }

        [Fact]
        public void Given_ANonCanonicalItemInsideAList_When_CountItemsIsCalled_Then_ItIsRejected()
        {
            var reader = new RlpReader(Hex("c3" + "01" + "8101"));
            var scope = reader.ReadListHeader();

            Assert.Throws<InvalidOperationException>(() => reader.CountItems(scope));
        }

        [Fact]
        public void Given_AListOfAHundredThousandStrings_When_CountItemsAndSkipRun_Then_NothingIsAllocated()
        {
            var item = RLP.EncodeElement(new byte[] { 0xaa, 0xbb, 0xcc });
            var content = new byte[item.Length * 100000];
            for (var i = 0; i < 100000; i++)
                Array.Copy(item, 0, content, i * item.Length, item.Length);
            var message = RLP.EncodeList(content);
            var reader = new RlpReader(message);
            var scope = reader.ReadListHeader();
            reader.CountItems(scope);

            var before = GC.GetAllocatedBytesForCurrentThread();
            var count = reader.CountItems(scope);
            while (!reader.IsAtEnd(scope))
                reader.Skip();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(100000, count);
            Assert.Equal(0, allocated);
        }

        [Fact]
        public void Given_ANestedListFlood_When_ItIsWalkedWithTheReader_Then_AllocationStaysUnderTwiceThePayload()
        {
            var chain = new byte[] { 0xc0 };
            for (var i = 1; i < 54; i++)
            {
                var next = new byte[chain.Length + 1];
                next[0] = (byte)(0xc0 + chain.Length);
                Array.Copy(chain, 0, next, 1, chain.Length);
                chain = next;
            }
            var block = new List<byte>(chain);
            block.Add(0x93);
            for (var i = 0; i < 19; i++) block.Add(0xaa);
            var content = new List<byte>();
            for (var i = 0; i < 20000; i++) content.AddRange(block);
            var message = RLP.EncodeList(content.ToArray());

            Consume(new RlpReader(message));
            var before = GC.GetAllocatedBytesForCurrentThread();
            var consumed = Consume(new RlpReader(message));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(20000 * 55 + 1, consumed);
            Assert.True(allocated < 2L * message.Length, "walking " + message.Length + " bytes allocated " + allocated);
        }

        [Fact]
        public void Given_RandomNestedStructures_When_WalkedWithTheReader_Then_TheyEqualTheTreeDecoder()
        {
            var random = new Random(20261027);
            for (var round = 0; round < 300; round++)
            {
                var encoded = RandomList(random, 0);

                Assert.Equal(Flatten(RLP.Decode(encoded)), WalkWhole(encoded));
            }
        }

        [Fact]
        public void Given_AReaderOverASubrange_When_Read_Then_ItStaysInsideTheRange()
        {
            var inner = RLP.EncodeElement(new byte[] { 0x01, 0x02 });
            var buffer = new byte[3 + inner.Length + 3];
            Array.Copy(inner, 0, buffer, 3, inner.Length);
            buffer[3 + inner.Length] = 0x01;

            var reader = new RlpReader(buffer, 3, inner.Length);

            Assert.Equal(new byte[] { 0x01, 0x02 }, reader.ReadBytes());
            Assert.True(reader.IsAtEnd());
            reader.EnsureEnd();
            Assert.Throws<InvalidOperationException>(() => reader.ReadBytes());
        }

        [Fact]
        public void Given_AListAndAStringAndAByte_When_ReadData_Then_AListIsItsFullEncodingAndAStringIsItsPayload()
        {
            var list = RLP.EncodeList(RLP.EncodeElement(new byte[] { 0x01, 0x02 }));
            var message = RLP.EncodeList(list, RLP.EncodeElement(new byte[] { 0x01, 0x02, 0x03 }), RLP.EncodeElement(new byte[] { 0x05 }), RLP.EncodeElement(new byte[0]));
            var reader = new RlpReader(message);
            var outer = reader.ReadListHeader();

            Assert.Equal(list, reader.ReadData());
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, reader.ReadData());
            Assert.Equal(new byte[] { 0x05 }, reader.ReadData());
            Assert.Empty(reader.ReadData());
            reader.EnsureEnd(outer);
        }

        private static byte[] RandomList(Random random, int depth)
        {
            var count = random.Next(0, 6);
            var items = new byte[count][];
            for (var i = 0; i < count; i++)
                items[i] = depth < 4 && random.Next(3) == 0 ? RandomList(random, depth + 1) : RandomString(random);
            return RLP.EncodeList(items);
        }

        private static byte[] RandomString(Random random)
        {
            int[] sizes = { 0, 1, 1, 2, 31, 32, 55, 56, 57, 255, 256, 300 };
            var size = sizes[random.Next(sizes.Length)];
            var data = new byte[size];
            random.NextBytes(data);
            if (size == 1 && data[0] == 0) data[0] = 1;
            return RLP.EncodeElement(data);
        }
    }
}
