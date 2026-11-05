using Nethereum.Documentation;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "PaymentRequirements - one accepted payment option")]
public class PaymentRequirements
{
    [JsonPropertyName("scheme")]
    public string Scheme { get; set; } = null!;

    [JsonPropertyName("network")]
    public string Network { get; set; } = null!;

    [JsonPropertyName("amount")]
    public string Amount { get; set; } = null!;

    [JsonPropertyName("asset")]
    public string Asset { get; set; } = null!;

    [JsonPropertyName("payTo")]
    public string PayTo { get; set; } = null!;

    [JsonPropertyName("maxTimeoutSeconds")]
    public int MaxTimeoutSeconds { get; set; }

    [JsonPropertyName("extra")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Extra { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
