using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

/// <summary>
/// Represents the response from the POST /settle endpoint.
/// Spec Reference: Section 7.2 - POST /settle, Section 5.3 - SettlementResponse Schema
/// </summary>
public class SettlementResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("errorReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorReason { get; set; }

    [JsonPropertyName("transaction")]
    public string Transaction { get; set; } = null!;

    [JsonPropertyName("network")]
    public string Network { get; set; } = null!;

    [JsonPropertyName("payer")]
    public string Payer { get; set; } = null!;
}
