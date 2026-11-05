using Nethereum.Documentation;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;

namespace Nethereum.X402.AspNetCore;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "PaymentReplayIdentity - the replay key of a payment")]
public static class PaymentReplayIdentity
{
    private const int Permit2NonceBits = 256;

    public static string? Extract(PaymentPayload payment)
    {
        switch (TypedPayload(payment))
        {
            case ExactSchemePayload exact when exact.Authorization != null:
                return Compose(exact.Authorization.From, exact.Authorization.Nonce);
            case Permit2SchemePayload permit2 when permit2.Permit2Authorization != null:
                return Compose(permit2.Permit2Authorization.From, permit2.Permit2Authorization.Nonce);
            default:
                return null;
        }
    }

    public static string? Extract(PaymentPayload payment, PaymentRequirements requirements)
    {
        if (requirements == null
            || string.IsNullOrEmpty(requirements.Network)
            || !X402Address.TryCanonical(requirements.Asset, out var asset))
            return null;

        var method = X402RequirementSupport.ResolveAssetTransferMethod(requirements);
        string? from;
        string? nonce;
        if (method == X402AssetTransferMethods.Eip3009)
        {
            var exact = TypedPayload<ExactSchemePayload>(payment);
            if (exact?.Authorization == null)
                return null;

            from = exact.Authorization.From;
            nonce = CanonicalEip3009Nonce(exact.Authorization.Nonce);
        }
        else if (method == X402AssetTransferMethods.Permit2)
        {
            var permit2 = TypedPayload<Permit2SchemePayload>(payment);
            if (permit2?.Permit2Authorization == null)
                return null;

            from = permit2.Permit2Authorization.From;
            nonce = CanonicalPermit2Nonce(permit2.Permit2Authorization.Nonce);
        }
        else
        {
            return null;
        }

        if (nonce == null || !X402Address.TryCanonical(from, out var canonicalFrom))
            return null;

        return $"{requirements.Network.ToLowerInvariant()}:{method}:{asset}:{canonicalFrom}:{nonce}";
    }

    private static object? TypedPayload(PaymentPayload? payment)
    {
        try
        {
            switch (payment?.Payload)
            {
                case ExactSchemePayload exact:
                    return exact;
                case Permit2SchemePayload permit2:
                    return permit2;
                case JsonElement json when json.ValueKind == JsonValueKind.Object:
                    return X402ExactSchemeProcessor.IsPermit2Payload(payment)
                        ? json.Deserialize<Permit2SchemePayload>()
                        : json.Deserialize<ExactSchemePayload>();
                default:
                    return null;
            }
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static T? TypedPayload<T>(PaymentPayload? payment) where T : class
    {
        try
        {
            return payment?.Payload switch
            {
                T typed => typed,
                JsonElement json when json.ValueKind == JsonValueKind.Object => json.Deserialize<T>(),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Compose(string? from, string? nonce)
    {
        var canonical = CanonicalNonce(nonce);
        return from == null || canonical == null ? null : $"{from.ToLowerInvariant()}:{canonical}";
    }

    private static string? CanonicalEip3009Nonce(string? nonce) =>
        X402Address.TryCanonicalWord(nonce, out var canonical) ? canonical : null;

    private static string? CanonicalPermit2Nonce(string? nonce)
    {
        if (!X402Number.TryParse(nonce, out var value) || value.GetBitLength() > Permit2NonceBits)
            return null;

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string? CanonicalNonce(string? nonce)
    {
        if (string.IsNullOrEmpty(nonce) || nonce.Length > 130)
            return null;

        try
        {
            if (nonce.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || nonce.StartsWith("0X", StringComparison.Ordinal))
            {
                var bytes = nonce.Substring(2).HexToByteArray();
                if (bytes.Length == 0 || bytes.Length > 32)
                    return null;
                var padded = new byte[32];
                Buffer.BlockCopy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
                return padded.ToHex(true);
            }

            return BigInteger.TryParse(nonce, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? value.ToString(CultureInfo.InvariantCulture)
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
