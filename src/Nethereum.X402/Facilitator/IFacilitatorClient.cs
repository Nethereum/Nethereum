using Nethereum.X402.Models;

namespace Nethereum.X402.Facilitator;

/// <summary>
/// Client for x402 facilitator service.
/// Spec Reference: Section 7 - Facilitator API
/// </summary>
public interface IFacilitatorClient
{
    /// <summary>
    /// Verifies a payment without settling it on-chain.
    /// Spec Reference: Section 7.1 - POST /verify
    /// </summary>
    /// <param name="paymentPayload">The payment payload to verify</param>
    /// <param name="requirements">The payment requirements</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Verification response indicating validity</returns>
    Task<VerificationResponse> VerifyAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles a payment on-chain.
    /// Spec Reference: Section 7.2 - POST /settle
    /// </summary>
    /// <param name="paymentPayload">The payment payload to settle</param>
    /// <param name="requirements">The payment requirements</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Settlement response with transaction details</returns>
    Task<SettlementResponse> SettleAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets supported payment kinds from the facilitator.
    /// Spec Reference: Section 7.3 - GET /supported
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of supported payment kinds</returns>
    Task<SupportedPaymentKindsResponse> GetSupportedAsync(
        CancellationToken cancellationToken = default);
}
