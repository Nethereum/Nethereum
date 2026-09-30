using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public static class StateTestScalar
    {
        private const string BigIntMarker = "0x:bigint ";
        private const int BytesInA256BitField = 32;

        public static BigInteger Parse(LegacyTransactionField field, string text)
        {
            var digits = WithoutTheBigIntMarker(text);
            if (string.IsNullOrEmpty(digits)) return BigInteger.Zero;

            var bytes = digits.HexToByteArray();
            if (bytes.Length > BytesInA256BitField)
                throw new ScalarWiderThanItsFieldException(field, bytes.Length);

            return digits.HexToBigInteger(false);
        }

        private static string WithoutTheBigIntMarker(string text)
        {
            if (text == null) return null;
            return text.StartsWith(BigIntMarker)
                ? text.Substring(BigIntMarker.Length)
                : text;
        }
    }
}
