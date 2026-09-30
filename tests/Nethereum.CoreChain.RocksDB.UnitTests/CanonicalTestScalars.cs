using System;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    internal static class CanonicalTestScalars
    {
        public static byte[] Scalar(long value)
        {
            if (value <= 0) return Array.Empty<byte>();
            var bigEndian = BitConverter.GetBytes(value);
            Array.Reverse(bigEndian);
            var start = 0;
            while (start < bigEndian.Length && bigEndian[start] == 0) start++;
            return bigEndian[start..];
        }
    }
}
