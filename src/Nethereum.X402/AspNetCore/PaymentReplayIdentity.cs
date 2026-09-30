using System.Text.Json;
using Nethereum.X402.Models;

namespace Nethereum.X402.AspNetCore;

public static class PaymentReplayIdentity
{
    public static string Extract(PaymentPayload payment)
    {
        switch (payment?.Payload)
        {
            case ExactSchemePayload exact when exact.Authorization != null:
                return Compose(exact.Authorization.From, exact.Authorization.Nonce);
            case Permit2SchemePayload permit2 when permit2.Permit2Authorization != null:
                return Compose(permit2.Permit2Authorization.From, permit2.Permit2Authorization.Nonce);
            case JsonElement json:
                return FromJson(json);
            default:
                return null;
        }
    }

    private static string FromJson(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            return null;

        if (TryGetProperty(payload, "authorization", out var auth) ||
            TryGetProperty(payload, "permit2Authorization", out auth))
        {
            var from = GetString(auth, "from");
            var nonce = GetString(auth, "nonce");
            if (from != null && nonce != null)
                return Compose(from, nonce);
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, System.StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string GetString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        return TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string Compose(string from, string nonce) => $"{from?.ToLowerInvariant()}:{nonce?.ToLowerInvariant()}";
}
