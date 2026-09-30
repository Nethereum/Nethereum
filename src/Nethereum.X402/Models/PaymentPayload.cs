using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

public class PaymentPayload
{
    [JsonPropertyName("x402Version")]
    public int X402Version { get; set; } = 2;

    [JsonPropertyName("resource")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResourceInfo? Resource { get; set; }

    [JsonPropertyName("accepted")]
    public PaymentRequirements Accepted { get; set; } = null!;

    [JsonPropertyName("payload")]
    public object Payload { get; set; } = null!;

    [JsonPropertyName("extensions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Extensions { get; set; }
}
