using System;

namespace Nethereum.DevP2P.Rlpx
{
    internal static class RlpxMessageId
    {
        public static byte[] Encode(int id)
        {
            if (id == 0) return new byte[] { 0x80 };
            if (id < 0x80) return new byte[] { (byte)id };
            if (id < 0x100) return new byte[] { 0x81, (byte)id };
            return new byte[] { 0x82, (byte)(id >> 8), (byte)id };
        }

        public static (int messageId, int bytesConsumed) Decode(byte[] data)
        {
            if (data[0] == 0x80) return (0, 1);
            if (data[0] < 0x80) return (data[0], 1);
            if (data[0] == 0x81) return (data[1], 2);
            if (data[0] == 0x82) return ((data[1] << 8) | data[2], 3);
            throw new InvalidOperationException($"Unexpected RLP msg-id prefix: 0x{data[0]:x2}");
        }
    }
}
