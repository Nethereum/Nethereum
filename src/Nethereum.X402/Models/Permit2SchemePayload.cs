using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

public class Permit2SchemePayload
{
    [JsonPropertyName("signature")]
    public string Signature { get; set; } = null!;

    [JsonPropertyName("permit2Authorization")]
    public Permit2Authorization Permit2Authorization { get; set; } = null!;
}

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

public class Permit2TokenPermissions
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = null!;

    [JsonPropertyName("amount")]
    public string Amount { get; set; } = null!;
}

/// <summary>
/// The x402 witness bound into the permit2 signature: the enforced recipient (<c>to</c>) and the
/// not-before time (<c>validAfter</c>). EIP-712 type <c>Witness(address to,uint256 validAfter)</c>.
/// </summary>
public class Permit2WitnessData
{
    [JsonPropertyName("to")]
    public string To { get; set; } = null!;

    [JsonPropertyName("validAfter")]
    public string ValidAfter { get; set; } = null!;
}
