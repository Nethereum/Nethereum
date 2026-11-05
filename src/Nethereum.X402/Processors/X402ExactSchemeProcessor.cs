using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.X402.Models;

namespace Nethereum.X402.Processors;

public class X402ExactSchemeProcessor : IX402PaymentProcessor
{
    private readonly IX402PaymentProcessor _eip3009;
    private readonly IX402PaymentProcessor _permit2;

    public X402ExactSchemeProcessor(IX402PaymentProcessor eip3009, IX402PaymentProcessor permit2)
    {
        _eip3009 = eip3009;
        _permit2 = permit2;
    }

    public Task<VerificationResponse> VerifyPaymentAsync(
        PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default)
    {
        if (!ShapeMatchesRequirements(paymentPayload, requirements))
            return Task.FromResult(new VerificationResponse { IsValid = false, InvalidReason = X402ErrorCodes.UnsupportedPayloadType });

        return Select(requirements).VerifyPaymentAsync(paymentPayload, requirements, cancellationToken);
    }

    public Task<SettlementResponse> SettlePaymentAsync(
        PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default)
    {
        if (!ShapeMatchesRequirements(paymentPayload, requirements))
            return Task.FromResult(SettlementResponse.Failure(requirements?.Network, X402ErrorCodes.UnsupportedPayloadType, string.Empty));

        return Select(requirements).SettlePaymentAsync(paymentPayload, requirements, cancellationToken);
    }

    public async Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default)
    {
        var supported = new[]
        {
            await _eip3009.GetSupportedAsync(cancellationToken),
            await _permit2.GetSupportedAsync(cancellationToken)
        };

        return new SupportedPaymentKindsResponse
        {
            Kinds = MergeKinds(supported),
            Extensions = supported.Where(s => s?.Extensions != null).SelectMany(s => s.Extensions).Distinct().ToList(),
            Signers = MergeSigners(supported)
        };
    }

    private IX402PaymentProcessor Select(PaymentRequirements requirements) =>
        X402RequirementSupport.ResolveAssetTransferMethod(requirements) == X402AssetTransferMethods.Permit2 ? _permit2 : _eip3009;

    private static bool ShapeMatchesRequirements(PaymentPayload paymentPayload, PaymentRequirements requirements)
    {
        var method = X402RequirementSupport.ResolveAssetTransferMethod(requirements);
        if (method == X402AssetTransferMethods.Permit2)
            return IsPermit2Payload(paymentPayload);

        return method == X402AssetTransferMethods.Eip3009 && !IsPermit2Payload(paymentPayload);
    }

    internal static bool IsPermit2Payload(PaymentPayload paymentPayload)
    {
        switch (paymentPayload?.Payload)
        {
            case Permit2SchemePayload:
                return true;
            case JsonElement jsonElement:
                return jsonElement.ValueKind == JsonValueKind.Object &&
                       jsonElement.TryGetProperty("permit2Authorization", out _);
            default:
                return false;
        }
    }

    private static List<PaymentKind> MergeKinds(IEnumerable<SupportedPaymentKindsResponse> supported) =>
        supported
            .Where(s => s?.Kinds != null)
            .SelectMany(s => s.Kinds)
            .GroupBy(kind => (kind.X402Version, Scheme: kind.Scheme?.ToLowerInvariant(), Network: kind.Network?.ToLowerInvariant()))
            .Select(group => new PaymentKind
            {
                X402Version = group.Key.X402Version,
                Scheme = group.First().Scheme,
                Network = group.First().Network
            })
            .ToList();

    private static Dictionary<string, List<string>> MergeSigners(IEnumerable<SupportedPaymentKindsResponse> supported) =>
        supported
            .Where(s => s?.Signers != null)
            .SelectMany(s => s.Signers)
            .GroupBy(entry => entry.Key)
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(entry => entry.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList());
}
