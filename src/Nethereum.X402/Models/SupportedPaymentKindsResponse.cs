using Nethereum.Documentation;
using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "PaymentKind - one supported scheme and network")]
public class PaymentKind
{
    [JsonPropertyName("x402Version")]
    public int X402Version { get; set; }

    [JsonPropertyName("scheme")]
    public string Scheme { get; set; } = null!;

    [JsonPropertyName("network")]
    public string Network { get; set; } = null!;

    [JsonPropertyName("extra")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Extra { get; set; }
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "SupportedPaymentKindsResponse - kinds, extensions and signers a facilitator supports")]
public class SupportedPaymentKindsResponse
{
    [JsonPropertyName("kinds")]
    public List<PaymentKind> Kinds { get; set; } = null!;

    [JsonPropertyName("extensions")]
    public List<string> Extensions { get; set; } = new();

    [JsonPropertyName("signers")]
    public Dictionary<string, List<string>> Signers { get; set; } = new();
}
