using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

/// <summary>
/// Represents the payment requirements for accessing a protected resource.
/// Spec Reference: Section 5.1.2
/// </summary>
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
}
