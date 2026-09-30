using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nethereum.X402.Models;

public class ExactSchemeExtra
{
    /// <summary>
    /// The EVM asset transfer method for the exact scheme: "eip3009" (default) or "permit2".
    /// Absent means eip3009 per the v2 spec. This library implements eip3009.
    /// </summary>
    [JsonPropertyName("assetTransferMethod")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssetTransferMethod { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("version")]
    public string Version { get; set; } = null!;

    public static ExactSchemeExtra? FromRequirements(PaymentRequirements requirements)
    {
        switch (requirements?.Extra)
        {
            case null:
                return null;
            case ExactSchemeExtra typed:
                return typed;
            case JsonElement jsonElement:
                return jsonElement.Deserialize<ExactSchemeExtra>();
            default:
                return null;
        }
    }
}
