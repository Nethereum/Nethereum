using Nethereum.Documentation;
using Nethereum.X402.Models;

namespace Nethereum.X402.Facilitator;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "IFacilitatorClient - verify, settle and supported over a facilitator")]
public interface IFacilitatorClient
{
    Task<VerificationResponse> VerifyAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default);

    Task<SettlementResponse> SettleAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default);

    Task<SupportedPaymentKindsResponse> GetSupportedAsync(
        CancellationToken cancellationToken = default);
}
