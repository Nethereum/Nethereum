using System;
using System.Numerics;

namespace Nethereum.ABI.EIP712
{
    internal static class Eip712Values
    {
        private static readonly BigInteger TwoPow256 = BigInteger.One << 256;

        public static bool TryParseInteger(object value, out BigInteger result)
        {
            result = BigInteger.Zero;
            switch (value)
            {
                case BigInteger big:
                    result = big;
                    return true;
                case int i:
                    result = i;
                    return true;
                case long l:
                    result = l;
                    return true;
                case short s:
                    result = s;
                    return true;
                case sbyte sb:
                    result = sb;
                    return true;
                case byte b:
                    result = b;
                    return true;
                case ushort us:
                    result = us;
                    return true;
                case uint ui:
                    result = ui;
                    return true;
                case ulong ul:
                    result = ul;
                    return true;
                case double d:
                    if (double.IsNaN(d) || double.IsInfinity(d) || d < long.MinValue || d >= long.MaxValue || Math.Floor(d) != d)
                        return false;
                    result = (long)d;
                    return true;
                case string text:
                    return TryParseHexOrDecimal(text, out result);
                default:
                    return false;
            }
        }

        public static bool TryParseHexOrDecimal(string text, out BigInteger result)
        {
            result = BigInteger.Zero;
            if (text == null) return false;
            if (text.Length == 0) return true;

            if (text.Length >= 2 && text[0] == '0' && (text[1] == 'x' || text[1] == 'X'))
            {
                if (text.Length == 2) return false;
                var parsed = BigInteger.Zero;
                for (var i = 2; i < text.Length; i++)
                {
                    var digit = HexDigit(text[i]);
                    if (digit < 0) return false;
                    parsed = (parsed << 4) + digit;
                }
                if (parsed >= TwoPow256) return false;
                result = parsed;
                return true;
            }

            var start = text[0] == '-' ? 1 : 0;
            if (text.Length == start) return false;
            var magnitude = BigInteger.Zero;
            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (c < '0' || c > '9') return false;
                magnitude = magnitude * 10 + (c - '0');
            }
            if (magnitude >= TwoPow256) return false;
            result = start == 1 ? -magnitude : magnitude;
            return true;
        }

        public static bool TryParseHexBytes(string text, out byte[] result)
        {
            result = null;
            if (text == null || text.Length < 2 || text[0] != '0' || (text[1] != 'x' && text[1] != 'X')) return false;
            if ((text.Length & 1) != 0) return false;
            var bytes = new byte[(text.Length - 2) / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                var high = HexDigit(text[2 + i * 2]);
                var low = HexDigit(text[3 + i * 2]);
                if (high < 0 || low < 0) return false;
                bytes[i] = (byte)(high * 16 + low);
            }
            result = bytes;
            return true;
        }

        public static bool IsHexAddress(string text)
        {
            if (text == null) return false;
            var start = 0;
            if (text.Length == 42 && text[0] == '0' && (text[1] == 'x' || text[1] == 'X')) start = 2;
            else if (text.Length != 40) return false;
            for (var i = start; i < text.Length; i++)
            {
                if (HexDigit(text[i]) < 0) return false;
            }
            return true;
        }

        public static bool TryGetIntegerWidth(string type, out int width, out bool signed)
        {
            width = 0;
            signed = false;
            string digits;
            if (type.StartsWith("uint", StringComparison.Ordinal)) digits = type.Substring(4);
            else if (type.StartsWith("int", StringComparison.Ordinal))
            {
                signed = true;
                digits = type.Substring(3);
            }
            else return false;

            if (digits.Length == 0)
            {
                width = 256;
                return true;
            }
            return TryParseWidth(digits, out width) && width >= 8 && width <= 256 && width % 8 == 0;
        }

        public static bool TryGetBytesWidth(string type, out int width)
        {
            width = 0;
            if (!type.StartsWith("bytes", StringComparison.Ordinal)) return false;
            return TryParseWidth(type.Substring(5), out width) && width >= 1 && width <= 32;
        }

        public static bool IsPrimitiveType(string type)
        {
            switch (type)
            {
                case "address":
                case "bool":
                case "string":
                case "bytes":
                    return true;
            }
            return TryGetBytesWidth(type, out _) || TryGetIntegerWidth(type, out _, out _);
        }

        private static bool TryParseWidth(string digits, out int width)
        {
            width = 0;
            if (digits.Length == 0 || digits.Length > 3 || digits[0] == '0') return false;
            foreach (var c in digits)
            {
                if (c < '0' || c > '9') return false;
                width = width * 10 + (c - '0');
            }
            return true;
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }
    }
}
