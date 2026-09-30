using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Models;

public class PaymentRequiredTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static PaymentRequired Sample() => new()
    {
        X402Version = 2,
        Error = "PAYMENT-SIGNATURE header is required",
        Resource = new ResourceInfo
        {
            Url = "https://api.example.com/premium-data",
            Description = "Access to premium market data",
            MimeType = "application/json"
        },
        Accepts = new List<PaymentRequirements>
        {
            new()
            {
                Scheme = "exact",
                Network = "eip155:84532",
                Amount = "10000",
                Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
                PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                MaxTimeoutSeconds = 60,
                Extra = new { name = "USDC", version = "2" }
            }
        }
    };

    [Fact]
    public void SerializedShape_MatchesV2Spec()
    {
        var json = JsonSerializer.Serialize(Sample(), JsonOptions);
        var obj = JsonNode.Parse(json)!.AsObject();

        Assert.Equal(2, obj["x402Version"]!.GetValue<int>());
        Assert.True(obj.ContainsKey("resource"));
        Assert.True(obj.ContainsKey("accepts"));

        var resource = obj["resource"]!.AsObject();
        Assert.Equal("https://api.example.com/premium-data", resource["url"]!.GetValue<string>());

        var accepts = obj["accepts"]!.AsArray();
        Assert.Equal(JsonValueKind.Array, obj["accepts"]!.GetValueKind());
        var first = accepts[0]!.AsObject();
        Assert.True(first.ContainsKey("scheme"));
        Assert.True(first.ContainsKey("amount"));
        Assert.False(first.ContainsKey("resource"));
        Assert.False(first.ContainsKey("description"));
    }

    [Fact]
    public void NullErrorAndExtensions_AreOmitted()
    {
        var response = Sample();
        response.Error = null;

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var obj = JsonNode.Parse(json)!.AsObject();

        Assert.False(obj.ContainsKey("error"));
        Assert.False(obj.ContainsKey("extensions"));
    }

    [Fact]
    public void DeserializesV2Json()
    {
        var json = @"{
            ""x402Version"": 2,
            ""error"": ""PAYMENT-SIGNATURE header is required"",
            ""resource"": {
                ""url"": ""https://api.example.com/premium-data"",
                ""description"": ""Access to premium market data"",
                ""mimeType"": ""application/json""
            },
            ""accepts"": [{
                ""scheme"": ""exact"",
                ""network"": ""eip155:84532"",
                ""amount"": ""10000"",
                ""asset"": ""0x036CbD53842c5426634e7929541eC2318f3dCF7e"",
                ""payTo"": ""0x209693Bc6afc0C5328bA36FaF03C514EF312287C"",
                ""maxTimeoutSeconds"": 60,
                ""extra"": { ""name"": ""USDC"", ""version"": ""2"" }
            }]
        }";

        var response = JsonSerializer.Deserialize<PaymentRequired>(json, JsonOptions);

        Assert.NotNull(response);
        Assert.Equal(2, response!.X402Version);
        Assert.Equal("https://api.example.com/premium-data", response.Resource.Url);
        Assert.Single(response.Accepts);
        Assert.Equal("exact", response.Accepts[0].Scheme);
        Assert.Equal("eip155:84532", response.Accepts[0].Network);
        Assert.Equal("10000", response.Accepts[0].Amount);
    }

    [Fact]
    public void RoundTrip_PreservesData()
    {
        var original = Sample();

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PaymentRequired>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.X402Version, deserialized!.X402Version);
        Assert.Equal(original.Resource.Url, deserialized.Resource.Url);
        Assert.Equal(original.Resource.Description, deserialized.Resource.Description);
        Assert.Single(deserialized.Accepts);
        Assert.Equal(original.Accepts[0].Scheme, deserialized.Accepts[0].Scheme);
        Assert.Equal(original.Accepts[0].Amount, deserialized.Accepts[0].Amount);
        Assert.Equal(original.Accepts[0].PayTo, deserialized.Accepts[0].PayTo);
    }
}
