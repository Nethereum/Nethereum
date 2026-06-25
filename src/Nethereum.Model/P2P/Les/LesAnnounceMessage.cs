using System.Collections.Generic;
using System.Numerics;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model.P2P.Les
{
    /// <summary>
    /// les/4 Announce (0x01): server announces a new head block to clients.
    /// Format: [headHash, headNumber, headTd, reorgDepth, [[key, value], ...]]
    /// </summary>
    public class LesAnnounceMessage
    {
        public byte[] HeadHash { get; set; } = new byte[32];
        public ulong HeadNumber { get; set; }
        public BigInteger HeadTd { get; set; }
        public ulong ReorgDepth { get; set; }
        public Dictionary<string, byte[]> Auxiliary { get; set; } = new();
    }

    public static class LesAnnounceMessageEncoder
    {
        public static byte[] Encode(LesAnnounceMessage msg)
        {
            var auxEncoded = new List<byte[]>();
            foreach (var kv in msg.Auxiliary)
            {
                auxEncoded.Add(RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(System.Text.Encoding.ASCII.GetBytes(kv.Key)),
                    RLP.RLP.EncodeElement(kv.Value ?? new byte[0])
                ));
            }

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(msg.HeadHash),
                RLP.RLP.EncodeElement(LongToRlp((long)msg.HeadNumber)),
                RLP.RLP.EncodeElement(msg.HeadTd.IsZero
                    ? new byte[0]
                    : msg.HeadTd.ToByteArrayUnsignedBigEndian()),
                RLP.RLP.EncodeElement(LongToRlp((long)msg.ReorgDepth)),
                RLP.RLP.EncodeList(auxEncoded.ToArray())
            );
        }

        public static LesAnnounceMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var msg = new LesAnnounceMessage
            {
                HeadHash = outer[0].RLPData,
                HeadNumber = (ulong)outer[1].RLPData.ToLongFromRLPDecoded(),
                HeadTd = (outer[2].RLPData ?? new byte[0]).ToBigIntegerFromUnsignedBigEndian(),
                ReorgDepth = (ulong)outer[3].RLPData.ToLongFromRLPDecoded()
            };

            if (outer.Count > 4)
            {
                var auxList = (RLPCollection)outer[4];
                foreach (RLPCollection entry in auxList)
                {
                    var key = System.Text.Encoding.ASCII.GetString(entry[0].RLPData);
                    msg.Auxiliary[key] = entry[1].RLPData ?? new byte[0];
                }
            }
            return msg;
        }

        private static byte[] LongToRlp(long value)
        {
            if (value == 0) return new byte[0];
            return value.ToBytesForRLPEncoding();
        }
    }
}
