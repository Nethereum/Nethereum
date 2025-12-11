using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

/// <summary>
/// Represents the payload for the "exact" payment scheme.
/// Spec Reference: Section 5.2.2
/// </summary>
public class ExactSchemePayload
{
    [JsonPropertyName("signature")]
    public string Signature { get; set; } = null!;

    [JsonPropertyName("authorization")]
    public Authorization Authorization { get; set; } = null!;
}
