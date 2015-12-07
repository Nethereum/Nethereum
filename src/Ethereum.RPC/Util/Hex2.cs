using System.Linq;

namespace Ethereum.ABI.Tests.DNX
{
    public static class Hex2
    {
        public static string ToHexString(this byte[] value)
        {
            return string.Concat(value.Select(b => b.ToString("x2")));
        }

        public static bool IsNumber(this object value)
        {
            return value is sbyte
                   || value is byte
                   || value is short
                   || value is ushort
                   || value is int
                   || value is uint
                   || value is long
                   || value is ulong
                   || value is float
                   || value is double
                   || value is decimal;
        }
    }
}