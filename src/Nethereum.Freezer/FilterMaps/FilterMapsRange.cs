using System;
using Nethereum.RLP;

namespace Nethereum.Freezer.FilterMaps
{
    public sealed class FilterMapsRange
    {
        public uint Version { get; }
        public bool HeadIndexed { get; }
        public long HeadDelimiter { get; }
        public long BlocksFirst { get; }
        public long BlocksAfterLast { get; }
        public long MapsFirst { get; }
        public long MapsAfterLast { get; }
        public long TailPartialEpoch { get; }

        public FilterMapsRange(
            uint version,
            bool headIndexed,
            long headDelimiter,
            long blocksFirst,
            long blocksAfterLast,
            long mapsFirst,
            long mapsAfterLast,
            long tailPartialEpoch)
        {
            Version = version;
            HeadIndexed = headIndexed;
            HeadDelimiter = headDelimiter;
            BlocksFirst = blocksFirst;
            BlocksAfterLast = blocksAfterLast;
            MapsFirst = mapsFirst;
            MapsAfterLast = mapsAfterLast;
            TailPartialEpoch = tailPartialEpoch;
        }

        public byte[] Encode()
        {
            var fields = new byte[][]
            {
                ((ulong)Version).ToBytesForRLPEncoding(),
                EncodeBool(HeadIndexed),
                ((ulong)HeadDelimiter).ToBytesForRLPEncoding(),
                ((ulong)BlocksFirst).ToBytesForRLPEncoding(),
                ((ulong)BlocksAfterLast).ToBytesForRLPEncoding(),
                ((ulong)MapsFirst).ToBytesForRLPEncoding(),
                ((ulong)MapsAfterLast).ToBytesForRLPEncoding(),
                ((ulong)TailPartialEpoch).ToBytesForRLPEncoding(),
            };
            return RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(fields);
        }

        public static FilterMapsRange Decode(ReadOnlySpan<byte> rlp)
        {
            var fields = DecodeFields(rlp);
            if (fields.Count != 8)
                throw new FormatException($"fm-R range value must have exactly 8 fields, got {fields.Count}");

            return new FilterMapsRange(
                version: (uint)fields[0].RLPData.ToULongFromRLPDecoded(),
                headIndexed: DecodeBool(fields[1].RLPData),
                headDelimiter: (long)fields[2].RLPData.ToULongFromRLPDecoded(),
                blocksFirst: (long)fields[3].RLPData.ToULongFromRLPDecoded(),
                blocksAfterLast: (long)fields[4].RLPData.ToULongFromRLPDecoded(),
                mapsFirst: (long)fields[5].RLPData.ToULongFromRLPDecoded(),
                mapsAfterLast: (long)fields[6].RLPData.ToULongFromRLPDecoded(),
                tailPartialEpoch: (long)fields[7].RLPData.ToULongFromRLPDecoded());
        }

        private static RLPCollection DecodeFields(ReadOnlySpan<byte> rlp)
        {
            try
            {
                return (RLPCollection)RLP.RLP.Decode(rlp.ToArray());
            }
            catch (Exception ex) when (!(ex is FormatException))
            {
                throw new FormatException("fm-R range value is not valid RLP", ex);
            }
        }

        private static byte[] EncodeBool(bool value) => value ? new byte[] { 1 } : Array.Empty<byte>();

        private static bool DecodeBool(byte[] rlpData) => rlpData != null && rlpData.Length > 0 && rlpData[0] != 0;
    }
}
