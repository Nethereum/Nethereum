using System.Globalization;
using System.Numerics;

namespace Nethereum.X402;

internal static class X402Number
{
    public const int MaxDigits = 78;

    public static bool TryParse(string? text, out BigInteger value)
    {
        value = default;
        if (string.IsNullOrEmpty(text) || text.Length > MaxDigits)
            return false;

        return BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    public static BigInteger Parse(string? text) =>
        TryParse(text, out var value)
            ? value
            : throw new FormatException("The value is not a non-negative integer of at most 78 decimal digits.");
}
