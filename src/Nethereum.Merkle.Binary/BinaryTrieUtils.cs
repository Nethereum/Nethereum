namespace Nethereum.Merkle.Binary
{
    public static class BinaryTrieUtils
    {
        public static int GetBit(byte[] data, int bitIndex)
        {
            if (data == null || bitIndex < 0 || bitIndex / 8 >= data.Length)
                return 0;
            return (data[bitIndex / 8] >> (7 - (bitIndex % 8))) & 1;
        }

        public static bool ByteArrayEquals(byte[] a, byte[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        public static bool ByteArrayEquals(byte[] a, byte[] b, int length)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a.Length < length || b.Length < length) return false;
            for (int i = 0; i < length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
