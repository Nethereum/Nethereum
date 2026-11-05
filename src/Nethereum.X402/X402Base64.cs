namespace Nethereum.X402;

internal static class X402Base64
{
    public static byte[] Decode(string value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        var trimmed = value.Trim();
        var remainder = trimmed.Length % 4;
        if (remainder == 1)
            throw new FormatException("The value is not valid base64.");

        if (remainder != 0)
            trimmed = trimmed.PadRight(trimmed.Length + 4 - remainder, '=');

        return Convert.FromBase64String(trimmed);
    }
}
