using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

/// <summary>
/// Represents a payment kind (scheme + network + version).
/// Spec Reference: Section 7.3 - GET /supported
/// </summary>
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

/// <summary>
/// Represents the response from the GET /supported endpoint.
/// Spec Reference: Section 7.3 - GET /supported
/// </summary>
public class SupportedPaymentKindsResponse
{
    [JsonPropertyName("kinds")]
    public List<PaymentKind> Kinds { get; set; } = null!;
}
