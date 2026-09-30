using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.X402.Models;

namespace Nethereum.X402.Processors;

/// <summary>
/// Dispatches "exact" scheme verify/settle to the correct asset-transfer method: the permit2
/// processor when the payload carries a <c>permit2Authorization</c>, otherwise the default EIP-3009
/// (transferWithAuthorization) processor. Mirrors the coinbase/x402 reference router
/// (@x402/evm exact/facilitator/scheme.ts, which selects by <c>isPermit2Payload</c>).
/// </summary>
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
        PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
        Select(paymentPayload).VerifyPaymentAsync(paymentPayload, requirements, cancellationToken);

    public Task<SettlementResponse> SettlePaymentAsync(
        PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
        Select(paymentPayload).SettlePaymentAsync(paymentPayload, requirements, cancellationToken);

    public async Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default)
    {
        var eip3009Kinds = await _eip3009.GetSupportedAsync(cancellationToken);
        var permit2Kinds = await _permit2.GetSupportedAsync(cancellationToken);
        var kinds = new List<PaymentKind>();
        if (eip3009Kinds?.Kinds != null) kinds.AddRange(eip3009Kinds.Kinds);
        if (permit2Kinds?.Kinds != null) kinds.AddRange(permit2Kinds.Kinds);
        return new SupportedPaymentKindsResponse { Kinds = kinds };
    }

    private IX402PaymentProcessor Select(PaymentPayload paymentPayload) =>
        IsPermit2Payload(paymentPayload) ? _permit2 : _eip3009;

    private static bool IsPermit2Payload(PaymentPayload paymentPayload)
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
}
