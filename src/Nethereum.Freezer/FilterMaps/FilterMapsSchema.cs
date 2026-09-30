using System;
using System.Text;

namespace Nethereum.Freezer.FilterMaps
{
    public static class FilterMapsSchema
    {
        private static readonly byte[] RangeKeyBytes = Encoding.ASCII.GetBytes("fm-R");
        private static readonly byte[] RowPrefix = Encoding.ASCII.GetBytes("fm-r");
        private static readonly byte[] LastBlockPrefix = Encoding.ASCII.GetBytes("fm-b");
        private static readonly byte[] BlockLvPrefix = Encoding.ASCII.GetBytes("fm-p");

        public static byte[] RangeKey() => (byte[])RangeKeyBytes.Clone();

        public static byte[] BaseRowKey(long mapRowIndex) => RowKey(mapRowIndex, extLength: 9);

        public static byte[] ExtRowKey(long mapRowIndex) => RowKey(mapRowIndex, extLength: 8);

        private static byte[] RowKey(long mapRowIndex, int extLength)
        {
            var key = new byte[RowPrefix.Length + extLength];
            Buffer.BlockCopy(RowPrefix, 0, key, 0, RowPrefix.Length);
            WriteUInt64BE(key, RowPrefix.Length, unchecked((ulong)mapRowIndex));
            return key;
        }

        public static byte[] LastBlockOfMapKey(long mapIndex)
        {
            var key = new byte[LastBlockPrefix.Length + 4];
            Buffer.BlockCopy(LastBlockPrefix, 0, key, 0, LastBlockPrefix.Length);
            WriteUInt32BE(key, LastBlockPrefix.Length, unchecked((uint)mapIndex));
            return key;
        }

        public static byte[] BlockLvPointerKey(long blockNumber)
        {
            var key = new byte[BlockLvPrefix.Length + 8];
            Buffer.BlockCopy(BlockLvPrefix, 0, key, 0, BlockLvPrefix.Length);
            WriteUInt64BE(key, BlockLvPrefix.Length, unchecked((ulong)blockNumber));
            return key;
        }

        public static long MapRowIndex(long mapIndex, int rowIndex, FilterMapsParams p)
        {
            var epochIndex = mapIndex >> p.LogMapsPerEpoch;
            var mapSubIndex = mapIndex & (p.MapsPerEpoch - 1);
            return (((epochIndex << p.LogMapHeight) + rowIndex) << p.LogMapsPerEpoch) + mapSubIndex;
        }

        private static void WriteUInt64BE(byte[] buffer, int offset, ulong value)
        {
            for (var i = 7; i >= 0; i--)
            {
                buffer[offset + i] = (byte)(value & 0xFF);
                value >>= 8;
            }
        }

        private static void WriteUInt32BE(byte[] buffer, int offset, uint value)
        {
            for (var i = 3; i >= 0; i--)
            {
                buffer[offset + i] = (byte)(value & 0xFF);
                value >>= 8;
            }
        }
    }
}
