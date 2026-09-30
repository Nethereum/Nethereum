using System;
using System.Buffers.Binary;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    public static class ForkIdEncoder
    {
        public static byte[] Encode(uint forkHash, ulong forkNext)
        {
            var hashBytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(hashBytes, forkHash);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(hashBytes),
                RLP.RLP.EncodeElement(forkNext == 0
                    ? new byte[0]
                    : ((long)forkNext).ToBytesForRLPEncoding()));
        }

        public static void Decode(byte[] rlpData, out uint forkHash, out ulong forkNext)
        {
            var items = (RLPCollection)RLP.RLP.Decode(rlpData);
            DecodeItems(items, out forkHash, out forkNext);
        }

        public static void DecodeItems(RLPCollection items, out uint forkHash, out ulong forkNext)
        {
            var hashBytes = items[0].RLPData;
            forkHash = BinaryPrimitives.ReadUInt32BigEndian(hashBytes);
            forkNext = items[1].RLPData == null || items[1].RLPData.Length == 0
                ? 0
                : (ulong)items[1].RLPData.ToLongFromRLPDecoded();
        }
    }
}
