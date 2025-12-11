using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

/// <summary>
/// Represents the payment payload submitted by the client.
/// Spec Reference: Section 5.2
/// </summary>
public class PaymentPayload
{
    [JsonPropertyName("x402Version")]
    public int X402Version { get; set; } = 1;

    [JsonPropertyName("scheme")]
    public string Scheme { get; set; } = null!;

    [JsonPropertyName("network")]
    public string Network { get; set; } = null!;

    [JsonPropertyName("payload")]
    public object Payload { get; set; } = null!;
}
