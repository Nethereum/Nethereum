using System.Text.RegularExpressions;

namespace Nethereum.X402.Permit2;

internal static class X402GasSponsoringFormat
{
    private static readonly Regex AddressPattern = new(@"^0x[a-fA-F0-9]{40}\z", RegexOptions.Compiled);
    private static readonly Regex NumberPattern = new(@"^[0-9]+\z", RegexOptions.Compiled);
    private static readonly Regex HexPattern = new(@"^0x[a-fA-F0-9]+\z", RegexOptions.Compiled);
    private static readonly Regex VersionPattern = new(@"^[0-9]+(\.[0-9]+)*\z", RegexOptions.Compiled);

    public static bool IsAddress(string? value) => value != null && AddressPattern.IsMatch(value);

    public static bool IsNumber(string? value) => value != null && value.Length <= X402Number.MaxDigits && NumberPattern.IsMatch(value);

    public static bool IsHex(string? value) => value != null && HexPattern.IsMatch(value);

    public static bool IsVersion(string? value) => value != null && VersionPattern.IsMatch(value);
}

