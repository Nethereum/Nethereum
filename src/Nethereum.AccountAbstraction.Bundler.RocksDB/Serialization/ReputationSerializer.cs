using System.Text;
using Nethereum.RLP;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.Serialization
{
    public static class ReputationSerializer
    {
        public static byte[] Serialize(ReputationEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.Address ?? "")),
                RLP.RLP.EncodeElement(entry.OpsIncluded.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(entry.OpsFailed.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(entry.OpsDropped.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(entry.LastUpdated.ToUnixTimeMilliseconds().ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((int)entry.Status).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(entry.BannedUntil?.ToUnixTimeMilliseconds().ToBytesForRLPEncoding() ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(entry.ThrottledUntil?.ToUnixTimeMilliseconds().ToBytesForRLPEncoding() ?? Array.Empty<byte>())
            );
        }

        public static ReputationEntry? Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            var decoded = RLP.RLP.Decode(data);
            var elements = (RLPCollection)decoded;

            return new ReputationEntry
            {
                Address = Encoding.UTF8.GetString(elements[0].RLPData ?? Array.Empty<byte>()),
                OpsIncluded = (int)elements[1].RLPData.ToLongFromRLPDecoded(),
                OpsFailed = (int)elements[2].RLPData.ToLongFromRLPDecoded(),
                OpsDropped = (int)elements[3].RLPData.ToLongFromRLPDecoded(),
                LastUpdated = DateTimeOffset.FromUnixTimeMilliseconds(elements[4].RLPData.ToLongFromRLPDecoded()),
                Status = (ReputationStatus)(int)elements[5].RLPData.ToLongFromRLPDecoded(),
                BannedUntil = elements[6].RLPData?.Length > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(elements[6].RLPData.ToLongFromRLPDecoded())
                    : null,
                ThrottledUntil = elements[7].RLPData?.Length > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(elements[7].RLPData.ToLongFromRLPDecoded())
                    : null
            };
        }

        public static byte[] AddressToKey(string address)
        {
            return Encoding.UTF8.GetBytes(address?.ToLowerInvariant() ?? "");
        }
    }
}
