using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Freezer.FilterMaps;
using Xunit;

namespace Nethereum.Freezer.UnitTests.FilterMaps
{
    public class FilterMapsRowCodecTests
    {
        private static readonly FilterMapsParams Params = FilterMapsParams.Default;

        private static FilterRow Row(params uint[] columns) => new FilterRow(columns);

        private static int ExpectedEncodedLength(IReadOnlyList<FilterRow> rows, int columnByteLength)
        {
            var entryCount = rows.Sum(r => r.Columns.Count);
            if (entryCount == 0) return 0;
            var lastNonEmptyRowIndex = -1;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].Columns.Count > 0) lastNonEmptyRowIndex = i;
            }
            var headerLength = (lastNonEmptyRowIndex + entryCount + 7) / 8;
            return headerLength + entryCount * columnByteLength;
        }

        [Fact]
        public void Given_GroupOfRows_When_EncodeDecodeBaseRowGroup_Then_RoundTripsIdentically()
        {
            var rows = new List<FilterRow>();
            for (uint i = 0; i < 32; i++)
            {
                rows.Add(i % 4 == 0
                    ? FilterRow.Empty
                    : Row(i, i * 7 + 1, i * 13 + 2));
            }

            var encoded = FilterMapsRowCodec.EncodeBaseRowGroup(rows, Params);
            var decoded = FilterMapsRowCodec.DecodeBaseRowGroup(encoded, rows.Count, Params);

            Assert.Equal(rows.Count, decoded.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                Assert.Equal(rows[i].Columns, decoded[i].Columns);
            }
        }

        [Fact]
        public void Given_GroupWithEmptyMaps_When_Decode_Then_EmptyMapsHaveNoColumns()
        {
            var rows = new List<FilterRow>();
            for (var i = 0; i < 32; i++)
            {
                rows.Add(Row((uint)(i * 10), (uint)(i * 10 + 1)));
            }
            rows[3] = FilterRow.Empty;
            rows[7] = FilterRow.Empty;
            rows[20] = FilterRow.Empty;

            var encoded = FilterMapsRowCodec.EncodeBaseRowGroup(rows, Params);

            var columnByteLength = Params.LogMapWidth / 8;
            var expectedLength = ExpectedEncodedLength(rows, columnByteLength);
            Assert.Equal(expectedLength, encoded.Length);
            var entryCount = rows.Sum(r => r.Columns.Count);
            Assert.NotEqual(4 + entryCount * columnByteLength, encoded.Length);

            var decoded = FilterMapsRowCodec.DecodeBaseRowGroup(encoded, rows.Count, Params);

            Assert.Empty(decoded[3].Columns);
            Assert.Empty(decoded[7].Columns);
            Assert.Empty(decoded[20].Columns);
            for (var i = 0; i < rows.Count; i++)
            {
                Assert.Equal(rows[i].Columns, decoded[i].Columns);
            }
        }

        [Fact]
        public void Given_PartialTailGroup_When_EncodeDecode_Then_HeaderCoversOnlyPresentMaps()
        {
            var rows = new List<FilterRow>
            {
                Row(1, 2),
                FilterRow.Empty,
                Row(999),
                Row(5, 6, 7, 8),
                FilterRow.Empty,
            };

            var encoded = FilterMapsRowCodec.EncodeBaseRowGroup(rows, Params);
            var columnByteLength = Params.LogMapWidth / 8;
            Assert.Equal(ExpectedEncodedLength(rows, columnByteLength), encoded.Length);

            var decoded = FilterMapsRowCodec.DecodeBaseRowGroup(encoded, rows.Count, Params);

            Assert.Equal(rows.Count, decoded.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                Assert.Equal(rows[i].Columns, decoded[i].Columns);
            }
        }

        [Fact]
        public void Given_AllMapsEmpty_When_EncodeBaseRowGroup_Then_EncodesToEmptyValue()
        {
            var rows = Enumerable.Repeat(FilterRow.Empty, 32).ToList();

            var encoded = FilterMapsRowCodec.EncodeBaseRowGroup(rows, Params);

            Assert.Empty(encoded);

            var decoded = FilterMapsRowCodec.DecodeBaseRowGroup(encoded, 32, Params);
            Assert.All(decoded, r => Assert.Empty(r.Columns));
        }

        [Fact]
        public void Given_ColumnAtMaxWidth_When_Encode_Then_3ByteLE()
        {
            var maxColumn = (uint)Params.MapWidth - 1;
            var row = Row(maxColumn);

            var encoded = FilterMapsRowCodec.EncodeExtRow(row, Params);

            Assert.Equal(3, encoded.Length);
            Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF }, encoded);

            var decoded = FilterMapsRowCodec.DecodeExtRow(encoded, Params);
            Assert.Equal(new uint[] { maxColumn }, decoded.Columns);
        }


        [Fact]
        public void Given_AsymmetricColumn_When_EncodeExtRow_Then_ExactLittleEndianBytes()
        {
            var encoded = FilterMapsRowCodec.EncodeExtRow(Row(0x010203), Params);
            Assert.Equal(new byte[] { 0x03, 0x02, 0x01 }, encoded);
            Assert.Equal(new uint[] { 0x010203u }, FilterMapsRowCodec.DecodeExtRow(encoded, Params).Columns);
        }

        [Fact]
        public void Given_KnownBaseRowGroup_When_Encode_Then_ExactGethBytes()
        {
            var group = new[] { Row(5), FilterRow.Empty };

            var encoded = FilterMapsRowCodec.EncodeBaseRowGroup(group, Params);

            Assert.Equal(new byte[] { 0x01, 0x05, 0x00, 0x00 }, encoded);

            var decoded = FilterMapsRowCodec.DecodeBaseRowGroup(encoded, group.Length, Params);
            Assert.Equal(new uint[] { 5u }, decoded[0].Columns);
            Assert.Empty(decoded[1].Columns);
        }

        [Fact]
        public void Given_ColumnAtOrAboveMapWidth_When_Encode_Then_Throws()
        {
            var tooLarge = Row((uint)Params.MapWidth);

            Assert.Throws<ArgumentOutOfRangeException>(() => FilterMapsRowCodec.EncodeExtRow(tooLarge, Params));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                FilterMapsRowCodec.EncodeBaseRowGroup(new List<FilterRow> { tooLarge }, Params));
        }

        [Fact]
        public void Given_ExtRow_When_EncodeDecode_Then_RoundTrips()
        {
            var row = Row(0, 1, 4096, 16777215, 42);

            var encoded = FilterMapsRowCodec.EncodeExtRow(row, Params);
            var decoded = FilterMapsRowCodec.DecodeExtRow(encoded, Params);

            Assert.Equal(row.Columns, decoded.Columns);
        }

        [Fact]
        public void Given_EmptyExtRow_When_EncodeDecode_Then_RoundTripsToEmpty()
        {
            var encoded = FilterMapsRowCodec.EncodeExtRow(FilterRow.Empty, Params);

            Assert.Empty(encoded);

            var decoded = FilterMapsRowCodec.DecodeExtRow(encoded, Params);
            Assert.Empty(decoded.Columns);
        }

        [Fact]
        public void Given_ColumnsWithDuplicatesAndOrder_When_RoundTrip_Then_OrderAndDupsPreserved()
        {
            var columns = new uint[] { 5, 5, 1, 999999, 1, 0 };
            var extRow = Row(columns);

            var extEncoded = FilterMapsRowCodec.EncodeExtRow(extRow, Params);
            var extDecoded = FilterMapsRowCodec.DecodeExtRow(extEncoded, Params);
            Assert.Equal(columns, extDecoded.Columns);

            var groupRows = new List<FilterRow> { extRow, FilterRow.Empty, Row(2, 2, 2) };
            var groupEncoded = FilterMapsRowCodec.EncodeBaseRowGroup(groupRows, Params);
            var groupDecoded = FilterMapsRowCodec.DecodeBaseRowGroup(groupEncoded, groupRows.Count, Params);

            Assert.Equal(columns, groupDecoded[0].Columns);
            Assert.Empty(groupDecoded[1].Columns);
            Assert.Equal(new uint[] { 2, 2, 2 }, groupDecoded[2].Columns);
        }

        [Fact]
        public void Given_MalformedExtRowLength_When_Decode_Then_Throws()
        {
            var malformed = new byte[] { 1, 2 };

            Assert.Throws<FormatException>(() => FilterMapsRowCodec.DecodeExtRow(malformed, Params));
        }

        [Fact]
        public void Given_TruncatedBaseRowGroupValue_When_Decode_Then_Throws()
        {
            var rows = new List<FilterRow> { Row(1, 2, 3) };
            var encoded = FilterMapsRowCodec.EncodeBaseRowGroup(rows, Params);
            var truncated = encoded.AsSpan(0, encoded.Length - 1).ToArray();

            Assert.Throws<FormatException>(() => FilterMapsRowCodec.DecodeBaseRowGroup(truncated, rows.Count, Params));
        }
    }
}
