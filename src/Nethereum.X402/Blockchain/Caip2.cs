using System;
using System.Globalization;

namespace Nethereum.X402.Blockchain;

/// <summary>
/// Helpers for CAIP-2 blockchain network identifiers of the EVM (<c>eip155</c>) namespace,
/// e.g. <c>eip155:8453</c>. x402 protocol version 2 uses CAIP-2 identifiers in place of the
/// plain network names used by version 1.
/// </summary>
public static class Caip2
{
    public const string Eip155Namespace = "eip155";

    /// <summary>
    /// Formats an EVM chain ID as its CAIP-2 identifier, e.g. <c>8453</c> becomes <c>eip155:8453</c>.
    /// </summary>
    public static string FormatEip155(int chainId) => $"{Eip155Namespace}:{chainId}";

    /// <summary>
    /// Returns true when <paramref name="networkId"/> is an <c>eip155</c> CAIP-2 identifier.
    /// </summary>
    public static bool IsEip155(string networkId) =>
        TryParseEip155ChainId(networkId, out _);

    /// <summary>
    /// Parses the chain ID from an <c>eip155</c> CAIP-2 identifier.
    /// Returns false for null, non-<c>eip155</c>, or malformed identifiers.
    /// </summary>
    public static bool TryParseEip155ChainId(string networkId, out int chainId)
    {
        chainId = 0;
        if (string.IsNullOrEmpty(networkId)) return false;

        var separator = networkId.IndexOf(':');
        if (separator <= 0 || separator == networkId.Length - 1) return false;

        var ns = networkId.Substring(0, separator);
        if (!ns.Equals(Eip155Namespace, StringComparison.OrdinalIgnoreCase)) return false;

        var reference = networkId.Substring(separator + 1);
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
