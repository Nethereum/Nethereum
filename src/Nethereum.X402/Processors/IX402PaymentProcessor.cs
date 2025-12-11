using System.Threading;
using System.Threading.Tasks;
using Nethereum.X402.Models;

namespace Nethereum.X402.Processors;

public interface IX402PaymentProcessor
{
    Task<VerificationResponse> VerifyPaymentAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default
    );

    Task<SettlementResponse> SettlePaymentAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default
    );

    Task<SupportedPaymentKindsResponse> GetSupportedAsync(
        CancellationToken cancellationToken = default
    );
}
