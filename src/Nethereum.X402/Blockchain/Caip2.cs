using System;
using System.Globalization;

namespace Nethereum.X402.Blockchain;

public static class Caip2
{
    public const string Eip155Namespace = "eip155";

    private const int MaxReferenceDigits = 78;

    public static string FormatEip155(int chainId) => $"{Eip155Namespace}:{chainId}";

    public static bool IsEip155(string networkId) =>
        TryParseEip155ChainId(networkId, out _);

    public static bool TryParseEip155ChainId(string networkId, out int chainId)
    {
        chainId = 0;
        if (string.IsNullOrEmpty(networkId)) return false;

        var separator = networkId.IndexOf(':');
        if (separator <= 0 || separator == networkId.Length - 1) return false;

        var ns = networkId.Substring(0, separator);
        if (!ns.Equals(Eip155Namespace, StringComparison.OrdinalIgnoreCase)) return false;

        var reference = networkId.Substring(separator + 1);
        if (reference.Length > MaxReferenceDigits || reference[0] < '1' || reference[0] > '9')
            return false;

        foreach (var c in reference)
        {
            if (c < '0' || c > '9')
                return false;
        }

        return int.TryParse(reference, NumberStyles.None, CultureInfo.InvariantCulture, out chainId);
    }

    public static int ParseEip155ChainId(string networkId)
    {
        if (!TryParseEip155ChainId(networkId, out var chainId))
        {
            throw new ArgumentException(
                $"'{networkId}' is not a valid eip155 CAIP-2 network identifier (expected 'eip155:<chainId>').",
                nameof(networkId));
        }

        return chainId;
    }
}
