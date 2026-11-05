using Nethereum.Documentation;
using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "SettlementResponse - the on-chain settlement result")]
public class SettlementResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("errorReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorReason { get; set; }

    [JsonPropertyName("errorMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("transaction")]
    public string Transaction { get; set; } = string.Empty;

    [JsonPropertyName("network")]
    public string Network { get; set; } = null!;

    [JsonPropertyName("payer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Payer { get; set; } = null!;

    [JsonPropertyName("amount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Amount { get; set; }

    [JsonPropertyName("extensions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Extensions { get; set; }

    internal static SettlementResponse Succeeded(string network, string payer, string transaction, string amount) =>
        new()
        {
            Success = true,
            ErrorReason = null,
            Transaction = transaction,
            Network = network,
            Payer = payer,
            Amount = amount
        };

    internal static SettlementResponse Failure(string network, string reason, string payer, string transaction = "", string? message = null) =>
        new()
        {
            Success = false,
            ErrorReason = reason,
            Transaction = transaction,
            Network = network,
            Payer = payer,
            ErrorMessage = message
        };
}
