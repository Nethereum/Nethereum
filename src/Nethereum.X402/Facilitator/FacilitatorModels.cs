using Nethereum.X402.Models;

namespace Nethereum.X402.Facilitator;

public class FacilitatorVerifyRequest
{
    public PaymentPayload PaymentPayload { get; set; }
    public PaymentRequirements PaymentRequirements { get; set; }
}

public class FacilitatorSettleRequest
{
    public PaymentPayload PaymentPayload { get; set; }
    public PaymentRequirements PaymentRequirements { get; set; }
}
