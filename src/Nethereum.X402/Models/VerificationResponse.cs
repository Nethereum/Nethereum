using Nethereum.Documentation;
using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "VerificationResponse - the result of verifying a payment")]
public class VerificationResponse
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("invalidReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InvalidReason { get; set; }

    [JsonPropertyName("invalidMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InvalidMessage { get; set; }

    [JsonPropertyName("payer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Payer { get; set; } = null!;

    [JsonPropertyName("extensions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Extensions { get; set; }

    [JsonPropertyName("extra")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Extra { get; set; }
}
