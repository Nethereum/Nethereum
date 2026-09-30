using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

public class PaymentRequired
{
    [JsonPropertyName("x402Version")]
    public int X402Version { get; set; } = 2;

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }

    [JsonPropertyName("resource")]
    public ResourceInfo Resource { get; set; } = null!;

    [JsonPropertyName("accepts")]
    public List<PaymentRequirements> Accepts { get; set; } = null!;

    [JsonPropertyName("extensions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Extensions { get; set; }
}
