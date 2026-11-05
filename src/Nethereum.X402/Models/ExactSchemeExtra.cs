using Nethereum.Documentation;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "ExactSchemeExtra - exact-scheme data in PaymentRequirements.Extra")]
public class ExactSchemeExtra
{
    [JsonPropertyName("assetTransferMethod")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssetTransferMethod { get; set; }

    [JsonPropertyName("paymentFlow")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PaymentFlow { get; set; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Name { get; set; } = null!;

    [JsonPropertyName("version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Version { get; set; } = null!;

    public static ExactSchemeExtra? FromRequirements(PaymentRequirements requirements) =>
        TryRead(requirements, out var extra) ? extra : null;

    internal static bool TryRead(PaymentRequirements requirements, out ExactSchemeExtra? extra)
    {
        extra = null;
        switch (requirements?.Extra)
        {
            case null:
                return true;
            case ExactSchemeExtra typed:
                extra = typed;
                return true;
            case JsonElement jsonElement:
                if (jsonElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    return true;

                try
                {
                    extra = jsonElement.Deserialize<ExactSchemeExtra>();
                    return extra != null;
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
                {
                    return false;
                }
            default:
                return true;
        }
    }
}
