using Nethereum.Documentation;
using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Erc20ApprovalGasSponsoringInfo - the signed ERC-20 approve transaction a client attaches")]
public class Erc20ApprovalGasSponsoringInfo
{
    [JsonPropertyName("from")]
    public string From { get; set; } = null!;

    [JsonPropertyName("asset")]
    public string Asset { get; set; } = null!;

    [JsonPropertyName("spender")]
    public string Spender { get; set; } = null!;

    [JsonPropertyName("amount")]
    public string Amount { get; set; } = null!;

    [JsonPropertyName("signedTransaction")]
    public string SignedTransaction { get; set; } = null!;

    [JsonPropertyName("version")]
    public string Version { get; set; } = null!;
}
