using Nethereum.RLP;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    internal static class RlpStreamHelpers
    {
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
