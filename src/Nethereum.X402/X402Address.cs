namespace Nethereum.X402;

internal static class X402Address
{
    public static bool TryCanonical(string? address, out string canonical)
    {
        canonical = string.Empty;
        if (address == null)
            return false;

        var hex = HasPrefix(address) ? address.Substring(2) : address;
        if (hex.Length != 40 || !IsHex(hex))
            return false;

        canonical = "0x" + hex.ToLowerInvariant();
        return true;
    }

    public static bool TryCanonicalWord(string? word, out string canonical)
    {
        canonical = string.Empty;
        if (word == null)
            return false;

        var hex = HasPrefix(word) ? word.Substring(2) : word;
        if (hex.Length != 64 || !IsHex(hex))
            return false;

        canonical = "0x" + hex.ToLowerInvariant();
        return true;
    }

    public static bool HasPrefix(string text) =>
        text.Length >= 2 && text[0] == '0' && (text[1] == 'x' || text[1] == 'X');

    public static bool IsHex(string text)
    {
        foreach (var c in text)
        {
            var isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!isHex)
                return false;
        }

        return true;
    }
}
