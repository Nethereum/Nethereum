using Nethereum.Documentation;
using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Permit2SchemePayload - the Permit2 payload")]
public class Permit2SchemePayload
{
    [JsonPropertyName("signature")]
    public string Signature { get; set; } = null!;

    [JsonPropertyName("permit2Authorization")]
    public Permit2Authorization Permit2Authorization { get; set; } = null!;
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Permit2Authorization - the signed Permit2 fields")]
public class Permit2Authorization
{
    [JsonPropertyName("from")]
    public string From { get; set; } = null!;

    [JsonPropertyName("permitted")]
    public Permit2TokenPermissions Permitted { get; set; } = null!;

    [JsonPropertyName("spender")]
    public string Spender { get; set; } = null!;

    [JsonPropertyName("nonce")]
    public string Nonce { get; set; } = null!;

    [JsonPropertyName("deadline")]
    public string Deadline { get; set; } = null!;

    [JsonPropertyName("witness")]
    public Permit2WitnessData Witness { get; set; } = null!;
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Permit2TokenPermissions - the permitted token and amount")]
public class Permit2TokenPermissions
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = null!;

    [JsonPropertyName("amount")]
    public string Amount { get; set; } = null!;
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Permit2WitnessData - the witness recipient and validAfter")]
public class Permit2WitnessData
{
    [JsonPropertyName("to")]
    public string To { get; set; } = null!;

    [JsonPropertyName("validAfter")]
    public string ValidAfter { get; set; } = null!;
}
