using System;
using System.Collections.Generic;

namespace Nethereum.Freezer.FilterMaps
{
    public static class FilterMapsRowCodec
    {
        private static int ColumnByteLength(FilterMapsParams p) => p.LogMapWidth / 8;

        public static byte[] EncodeBaseRowGroup(IReadOnlyList<FilterRow> rowsOfGroup, FilterMapsParams p)
        {
            if (rowsOfGroup == null) throw new ArgumentNullException(nameof(rowsOfGroup));

            var columnByteLength = ColumnByteLength(p);

            var entryCount = 0;
            var lastNonEmptyRowIndex = -1;
            for (var i = 0; i < rowsOfGroup.Count; i++)
            {
                var columns = rowsOfGroup[i].Columns;
                if (columns.Count > 0)
                {
                    entryCount += columns.Count;
                    lastNonEmptyRowIndex = i;
                }

                for (var j = 0; j < columns.Count; j++)
                {
                    AssertColumnInRange(columns[j], p);
                }
            }

            if (entryCount == 0)
            {
                return Array.Empty<byte>();
            }

            var headerBitLength = lastNonEmptyRowIndex + entryCount;
            var headerLength = (headerBitLength + 7) / 8;
            var encoded = new byte[headerLength + entryCount * columnByteLength];

            var nextEntryOffset = headerLength;
            var headerBytePos = 0;
            var headerBitMask = 1;

            for (var i = 0; i <= lastNonEmptyRowIndex; i++)
            {
                var columns = rowsOfGroup[i].Columns;
                for (var j = 0; j < columns.Count; j++)
                {
                    WriteColumnLittleEndian(encoded, nextEntryOffset, columns[j], columnByteLength);
                    nextEntryOffset += columnByteLength;
                    WriteHeaderBit(encoded, ref headerBytePos, ref headerBitMask, true);
                }

                if (i == lastNonEmptyRowIndex)
                {
                    break;
                }

                WriteHeaderBit(encoded, ref headerBytePos, ref headerBitMask, false);
            }

            return encoded;
        }

        public static IReadOnlyList<FilterRow> DecodeBaseRowGroup(ReadOnlySpan<byte> value, int rowCount, FilterMapsParams p)
        {
            if (rowCount < 0) throw new ArgumentOutOfRangeException(nameof(rowCount), "row count cannot be negative");

            var columnByteLength = ColumnByteLength(p);
            var rowSizes = new int[rowCount];

            var headerLength = value.Length == 0 ? 0 : DecodeHeader(value, rowCount, columnByteLength, rowSizes);

            return BuildRows(value, headerLength, rowSizes, columnByteLength);
        }

        private static int DecodeHeader(ReadOnlySpan<byte> value, int rowCount, int columnByteLength, int[] rowSizes)
        {
            var encLen = value.Length;
            var headerLength = 0;
            var entryCount = 0;
            var entriesInRow = 0;
            var rowIndex = 0;
            var headerBitsRemaining = 0;
            byte headerByte = 0;

            while (headerLength + columnByteLength * entryCount < encLen)
            {
                if (headerBitsRemaining == 0)
                {
                    headerByte = value[headerLength];
                    headerLength++;
                    headerBitsRemaining = 8;
                }

                if ((headerByte & 1) != 0)
                {
                    entriesInRow++;
                    entryCount++;
                }
                else
                {
                    rowSizes[GuardedRowIndex(rowIndex, rowCount)] = entriesInRow;
                    entriesInRow = 0;
                    rowIndex++;
                }

                headerByte >>= 1;
                headerBitsRemaining--;
            }

            if (headerLength + columnByteLength * entryCount > encLen)
            {
                throw new FormatException("invalid encoded base filter row group: header/entry length mismatch");
            }

            if (entriesInRow > 0)
            {
                rowSizes[GuardedRowIndex(rowIndex, rowCount)] = entriesInRow;
            }

            return headerLength;
        }

        private static int GuardedRowIndex(int rowIndex, int rowCount)
        {
            if (rowIndex >= rowCount)
            {
                throw new FormatException("invalid encoded base filter row group: row index exceeds group size");
            }

            return rowIndex;
        }

        private static IReadOnlyList<FilterRow> BuildRows(ReadOnlySpan<byte> value, int headerLength, int[] rowSizes, int columnByteLength)
        {
            var result = new FilterRow[rowSizes.Length];
            var offset = headerLength;

            for (var i = 0; i < rowSizes.Length; i++)
            {
                var size = rowSizes[i];
                if (size == 0)
                {
                    result[i] = FilterRow.Empty;
                    continue;
                }

                var columns = new uint[size];
                for (var j = 0; j < size; j++)
                {
                    columns[j] = ReadColumnLittleEndian(value.Slice(offset, columnByteLength));
                    offset += columnByteLength;
                }

                result[i] = new FilterRow(columns);
            }

            return result;
        }

        public static byte[] EncodeExtRow(FilterRow row, FilterMapsParams p)
        {
            var columnByteLength = ColumnByteLength(p);
            var columns = row.Columns;
            var encoded = new byte[columns.Count * columnByteLength];

            for (var i = 0; i < columns.Count; i++)
            {
                AssertColumnInRange(columns[i], p);
                WriteColumnLittleEndian(encoded, i * columnByteLength, columns[i], columnByteLength);
            }

            return encoded;
        }

        public static FilterRow DecodeExtRow(ReadOnlySpan<byte> value, FilterMapsParams p)
        {
            var columnByteLength = ColumnByteLength(p);
            if (value.Length % columnByteLength != 0)
            {
                throw new FormatException("invalid encoded extended filter row length");
            }

            var count = value.Length / columnByteLength;
            var columns = new uint[count];
            for (var i = 0; i < count; i++)
            {
                columns[i] = ReadColumnLittleEndian(value.Slice(i * columnByteLength, columnByteLength));
            }

            return new FilterRow(columns);
        }

        private static void WriteColumnLittleEndian(byte[] buffer, int offset, uint value, int columnByteLength)
        {
            for (var i = 0; i < columnByteLength; i++)
            {
                buffer[offset + i] = (byte)(value >> (8 * i));
            }
        }

        private static uint ReadColumnLittleEndian(ReadOnlySpan<byte> bytes)
        {
            uint value = 0;
            for (var i = 0; i < bytes.Length; i++)
            {
                value |= (uint)bytes[i] << (8 * i);
            }

            return value;
        }

        private static void AssertColumnInRange(uint column, FilterMapsParams p)
        {
            if (column >= (uint)p.MapWidth)
            {
                throw new ArgumentOutOfRangeException(nameof(column), column, $"column must be < MapWidth ({p.MapWidth})");
            }
        }

        private static void WriteHeaderBit(byte[] buffer, ref int bytePos, ref int bitMask, bool bit)
        {
            if (bit)
            {
                buffer[bytePos] |= (byte)bitMask;
            }

            bitMask <<= 1;
            if (bitMask == 256)
            {
                bitMask = 1;
                bytePos++;
            }
        }
    }
}
