using Nethereum.Documentation;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.X402.Models;

namespace Nethereum.X402.Processors;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "IX402PaymentProcessor - verify, settle and list supported kinds for the exact scheme")]
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
