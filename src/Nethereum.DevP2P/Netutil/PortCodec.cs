namespace Nethereum.DevP2P.Netutil
{
    internal static class PortCodec
    {
        public static byte[] EncodeBigEndian(ushort port)
        {
            if (port == 0) return System.Array.Empty<byte>();
            return new[] { (byte)((port >> 8) & 0xff), (byte)(port & 0xff) };
        }

        public static ushort DecodeBigEndian(byte[] data)
        {
            if (data == null || data.Length == 0) return 0;
            if (data.Length == 1) return data[0];
            return (ushort)((data[0] << 8) | data[1]);
        }
    }
}
