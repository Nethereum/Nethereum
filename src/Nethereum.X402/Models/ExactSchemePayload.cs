using Nethereum.Documentation;
using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "ExactSchemePayload - the EIP-3009 payload")]
public class ExactSchemePayload
{
    [JsonPropertyName("signature")]
    public string Signature { get; set; } = null!;

    [JsonPropertyName("authorization")]
    public Authorization Authorization { get; set; } = null!;
}
