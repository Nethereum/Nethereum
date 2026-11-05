using Nethereum.Documentation;
using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Authorization - the EIP-3009 authorization fields")]
public class Authorization
{
    [JsonPropertyName("from")]
    public string From { get; set; } = null!;

    [JsonPropertyName("to")]
    public string To { get; set; } = null!;

    [JsonPropertyName("value")]
    public string Value { get; set; } = null!;

    [JsonPropertyName("validAfter")]
    public string ValidAfter { get; set; } = null!;

    [JsonPropertyName("validBefore")]
    public string ValidBefore { get; set; } = null!;

    [JsonPropertyName("nonce")]
    public string Nonce { get; set; } = null!;
}
