using Nethereum.RLP;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    /// <summary>
    /// Low-level RLP byte-stream helpers needed to walk concatenated RLP records
    /// (e.g. <c>chain.rlp</c>) without first materialising the whole stream.
    /// Shared between <see cref="GethTestdataChainBackedEthHandler"/> and
    /// <see cref="GethTestdataHistoricalStateBuilder"/> so the prefix-length walker
    /// only exists once in the test project.
    /// </summary>
    internal static class RlpStreamHelpers
    {
        /// <summary>
        /// Returns the byte length consumed by the RLP item starting at <paramref name="pos"/>
        /// inside <paramref name="data"/> — single-byte values, short strings, long strings,
        /// short lists and long lists per the canonical RLP prefix-byte ranges.
        /// </summary>
        public static int GetRlpItemLength(byte[] data, int pos)
        {
            byte prefix = data[pos];
            if (prefix < 0x80) return 1;
            if (prefix < 0xb8) return 1 + (prefix - 0x80);
            if (prefix < 0xc0)
            {
                int n = prefix - 0xb7;
                int len = 0;
                for (int i = 0; i < n; i++) len = (len << 8) | data[pos + 1 + i];
                return 1 + n + len;
            }
            if (prefix < 0xf8) return 1 + (prefix - 0xc0);
            int nn = prefix - 0xf7;
            int llen = 0;
            for (int i = 0; i < nn; i++) llen = (llen << 8) | data[pos + 1 + i];
            return 1 + nn + llen;
        }

        /// <summary>
        /// Recursively re-encode an <see cref="RLPCollection"/> as a canonical RLP list —
        /// used to round-trip header / transaction / withdrawal records when re-hashing.
        /// </summary>
        public static byte[] ReEncodeAsList(RLPCollection coll)
        {
            var items = new byte[coll.Count][];
            for (int i = 0; i < coll.Count; i++)
            {
                if (coll[i] is RLPCollection sub) items[i] = ReEncodeAsList(sub);
                else items[i] = Nethereum.RLP.RLP.EncodeElement(coll[i].RLPData);
            }
            return Nethereum.RLP.RLP.EncodeList(items);
        }
    }
}
