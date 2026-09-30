using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Models;

public class PaymentRequirementsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static PaymentRequirements Sample() => new()
    {
        Scheme = "exact",
        Network = "eip155:84532",
        Amount = "10000",
        Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
        MaxTimeoutSeconds = 60,
        Extra = new { name = "USDC", version = "2" }
    };

    [Fact]
    public void SerializedFieldNames_MatchV2Spec()
    {
        var json = JsonSerializer.Serialize(Sample(), JsonOptions);
        var obj = JsonNode.Parse(json)!.AsObject();

        Assert.True(obj.ContainsKey("scheme"));
        Assert.True(obj.ContainsKey("network"));
        Assert.True(obj.ContainsKey("amount"));
        Assert.True(obj.ContainsKey("asset"));
        Assert.True(obj.ContainsKey("payTo"));
        Assert.True(obj.ContainsKey("maxTimeoutSeconds"));
        Assert.True(obj.ContainsKey("extra"));

        Assert.False(obj.ContainsKey("maxAmountRequired"));
        Assert.False(obj.ContainsKey("resource"));
        Assert.False(obj.ContainsKey("description"));
        Assert.False(obj.ContainsKey("mimeType"));
        Assert.False(obj.ContainsKey("outputSchema"));
    }

    [Fact]
    public void Amount_SerializesAsString()
    {
        var json = JsonSerializer.Serialize(Sample(), JsonOptions);
        var amount = JsonNode.Parse(json)!["amount"];

        Assert.NotNull(amount);
        Assert.Equal(JsonValueKind.String, amount!.GetValueKind());
        Assert.Equal("10000", amount.GetValue<string>());
    }

    [Fact]
    public void DeserializesV2Json()
    {
        var json = @"{
            ""scheme"": ""exact"",
            ""network"": ""eip155:84532"",
            ""amount"": ""10000"",
            ""asset"": ""0x036CbD53842c5426634e7929541eC2318f3dCF7e"",
            ""payTo"": ""0x209693Bc6afc0C5328bA36FaF03C514EF312287C"",
            ""maxTimeoutSeconds"": 60,
            ""extra"": { ""name"": ""USDC"", ""version"": ""2"" }
        }";

        var requirements = JsonSerializer.Deserialize<PaymentRequirements>(json, JsonOptions);

        Assert.NotNull(requirements);
        Assert.Equal("exact", requirements!.Scheme);
        Assert.Equal("eip155:84532", requirements.Network);
        Assert.Equal("10000", requirements.Amount);
        Assert.Equal("0x036CbD53842c5426634e7929541eC2318f3dCF7e", requirements.Asset);
        Assert.Equal("0x209693Bc6afc0C5328bA36FaF03C514EF312287C", requirements.PayTo);
        Assert.Equal(60, requirements.MaxTimeoutSeconds);
        Assert.NotNull(requirements.Extra);
    }

    [Fact]
    public void RoundTrip_PreservesData()
    {
        var original = Sample();

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PaymentRequirements>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Scheme, deserialized!.Scheme);
        Assert.Equal(original.Network, deserialized.Network);
        Assert.Equal(original.Amount, deserialized.Amount);
        Assert.Equal(original.Asset, deserialized.Asset);
        Assert.Equal(original.PayTo, deserialized.PayTo);
        Assert.Equal(original.MaxTimeoutSeconds, deserialized.MaxTimeoutSeconds);
    }

    [Fact]
    public void NullExtra_IsOmitted()
    {
        var requirements = Sample();
        requirements.Extra = null;

        var json = JsonSerializer.Serialize(requirements, JsonOptions);
        var obj = JsonNode.Parse(json)!.AsObject();

        Assert.False(obj.ContainsKey("extra"));
        Assert.True(obj.ContainsKey("scheme"));
        Assert.True(obj.ContainsKey("amount"));
    }
}
