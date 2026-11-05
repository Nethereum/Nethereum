using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;

namespace Nethereum.X402.Blockchain;

internal static class ExactVerification
{
    public static string? ProtocolRejection(
        PaymentPayload paymentPayload, PaymentRequirements requirements, string expectedTransferMethod)
    {
        if (paymentPayload.X402Version != X402Protocol.Version)
            return X402ErrorCodes.InvalidX402Version;

        if (paymentPayload.Accepted?.Scheme != X402Schemes.Exact || requirements.Scheme != X402Schemes.Exact)
            return X402ErrorCodes.InvalidScheme;

        if (X402RequirementSupport.ResolveAssetTransferMethod(requirements) != expectedTransferMethod)
            return X402ErrorCodes.UnsupportedPayloadType;

        return null;
    }

    public static string? AssetRejection(X402FacilitatorOptions options, PaymentRequirements requirements) =>
        options.IsAssetAllowed(requirements.Network, requirements.Asset) ? null : X402ErrorCodes.AssetNotAllowed;

    public static T? ReadPayload<T>(PaymentPayload paymentPayload) where T : class =>
        paymentPayload.Payload switch
        {
            T typed => typed,
            JsonElement jsonElement => JsonSerializer.Deserialize<T>(jsonElement.GetRawText()),
            _ => null
        };

    public static string FailureReason(ILogger logger, string scheme, Exception ex)
    {
        if (ex is JsonException or FormatException or OverflowException or InvalidOperationException or ArgumentException)
        {
            logger.LogDebug(ex, "x402 {Scheme} verify: malformed payment payload", scheme);
            return X402ErrorCodes.InvalidPayload;
        }

        logger.LogError(ex, "x402 {Scheme} verify failed", scheme);
        return X402ErrorCodes.UnexpectedVerifyError;
    }
}
