using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

/// <summary>
/// Represents the response from the POST /verify endpoint.
/// Spec Reference: Section 7.1 - POST /verify
/// </summary>
public class VerificationResponse
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("invalidReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InvalidReason { get; set; }

    [JsonPropertyName("payer")]
    public string Payer { get; set; } = null!;
}
