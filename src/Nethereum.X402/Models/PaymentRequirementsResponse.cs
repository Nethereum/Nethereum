using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

/// <summary>
/// Represents the 402 Payment Required response.
/// Spec Reference: Section 5.1 - PaymentRequirementsResponse Schema
/// </summary>
public class PaymentRequirementsResponse
{
    [JsonPropertyName("x402Version")]
    public int X402Version { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = null!;

    [JsonPropertyName("accepts")]
    public List<PaymentRequirements> Accepts { get; set; } = null!;
}
