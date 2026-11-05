using System;

namespace Nethereum.ZkProofsVerifier.Circom
{
    internal static class FieldElementText
    {
        public const int MaxDecimalDigits = 78;

        public static bool IsBoundedDecimal(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > MaxDecimalDigits)
                return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] < '0' || text[i] > '9')
                    return false;
            }
            return true;
        }

        public static string RequireBoundedDecimal(string text)
        {
            if (!IsBoundedDecimal(text))
                throw new FormatException("Field element must be 1 to " + MaxDecimalDigits + " ASCII decimal digits");
            return text;
        }
    }
}
