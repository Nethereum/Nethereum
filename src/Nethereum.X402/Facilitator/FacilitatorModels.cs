using Nethereum.Documentation;
using System.Text.Json.Serialization;
using Nethereum.X402.Models;

namespace Nethereum.X402.Facilitator;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "FacilitatorVerifyRequest - the body of POST verify")]
public class FacilitatorVerifyRequest
{
    [JsonPropertyName("x402Version")]
    public int X402Version { get; set; } = X402Protocol.Version;

    public PaymentPayload PaymentPayload { get; set; }
    public PaymentRequirements PaymentRequirements { get; set; }
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "FacilitatorSettleRequest - the body of POST settle")]
public class FacilitatorSettleRequest
{
    [JsonPropertyName("x402Version")]
    public int X402Version { get; set; } = X402Protocol.Version;

    public PaymentPayload PaymentPayload { get; set; }
    public PaymentRequirements PaymentRequirements { get; set; }
}
