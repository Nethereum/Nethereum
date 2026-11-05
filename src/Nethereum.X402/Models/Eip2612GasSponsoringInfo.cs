using Nethereum.Documentation;
using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Eip2612GasSponsoringInfo - the signed EIP-2612 permit a client attaches")]
public class Eip2612GasSponsoringInfo
{
    [JsonPropertyName("from")]
    public string From { get; set; } = null!;

    [JsonPropertyName("asset")]
    public string Asset { get; set; } = null!;

    [JsonPropertyName("spender")]
    public string Spender { get; set; } = null!;

    [JsonPropertyName("amount")]
    public string Amount { get; set; } = null!;

    [JsonPropertyName("nonce")]
    public string Nonce { get; set; } = null!;

    [JsonPropertyName("deadline")]
    public string Deadline { get; set; } = null!;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = null!;

    [JsonPropertyName("version")]
    public string Version { get; set; } = null!;
}
