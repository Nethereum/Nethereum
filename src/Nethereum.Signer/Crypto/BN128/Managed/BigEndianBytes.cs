using System.Numerics;

namespace Nethereum.Signer.Crypto.BN128.Managed
{
    internal static class BigEndianBytes
    {
        public static ulong ReadUInt64(byte[] data, int offset)
        {
            ulong value = 0;
            for (var i = 0; i < 8; i++) value = (value << 8) | data[offset + i];
            return value;
        }

        public static void WriteUInt64(byte[] destination, int offset, ulong value)
        {
            for (var i = 7; i >= 0; i--)
            {
                destination[offset + i] = (byte)value;
                value >>= 8;
            }
        }

        public static BigInteger ReadUnsignedBigInteger(byte[] data, int offset, int length)
        {
            var littleEndian = new byte[length + 1];
            for (var i = 0; i < length; i++) littleEndian[i] = data[offset + length - 1 - i];
            return new BigInteger(littleEndian);
        }

        public static int BitLength(BigInteger value)
        {
            var bytes = BigInteger.Abs(value).ToByteArray();
            var length = bytes.Length;
            while (length > 0 && bytes[length - 1] == 0) length--;
            if (length == 0) return 0;
            var top = bytes[length - 1];
            var topBits = 0;
            while (top != 0)
            {
                topBits++;
                top >>= 1;
            }
            return (length - 1) * 8 + topBits;
        }
    }
}
