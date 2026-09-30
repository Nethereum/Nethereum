using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Models;

public class SupportedPaymentKindsResponseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Given_ValidSupportedPaymentKindsResponse_When_CreatingObject_Then_AllRequiredFieldsArePresent()
    {
        var response = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>
            {
                new() { X402Version = 1, Scheme = "exact", Network = "base-sepolia" },
                new() { X402Version = 1, Scheme = "invoice", Network = "ethereum-mainnet" }
            }
        };

        Assert.NotNull(response.Kinds);
        Assert.Equal(2, response.Kinds.Count);
    }

    [Fact]
    public void Given_SupportedPaymentKindsResponse_When_SerializedToJson_Then_FieldNamesMatchSpec()
    {
        var response = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>
            {
                new() { X402Version = 1, Scheme = "exact", Network = "base-sepolia" },
                new() { X402Version = 1, Scheme = "invoice", Network = "ethereum-mainnet" }
            }
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        Assert.True(jsonObject.ContainsKey("kinds"), "Missing 'kinds' field");
    }

    [Fact]
    public void Given_SupportedPaymentKindsResponse_When_SerializedToJson_Then_SupportedPaymentKindsIsArray()
    {
        var response = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>
            {
                new() { X402Version = 1, Scheme = "exact", Network = "base-sepolia" },
                new() { X402Version = 1, Scheme = "invoice", Network = "ethereum-mainnet" }
            }
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);

        Assert.Equal(JsonValueKind.Array, jsonNode!["kinds"]!.GetValueKind());
        var array = jsonNode["kinds"]!.AsArray();
        Assert.Equal(2, array.Count);
        Assert.Equal("exact", array[0]!["scheme"]!.GetValue<string>());
        Assert.Equal("invoice", array[1]!["scheme"]!.GetValue<string>());
    }

    [Fact]
    public void Given_SpecCompliantJson_When_DeserializedToSupportedPaymentKindsResponse_Then_AllFieldsAreCorrect()
    {
        var json = @"{
            ""kinds"": [
                {
                    ""x402Version"": 1,
                    ""scheme"": ""exact"",
                    ""network"": ""base-sepolia""
                },
                {
                    ""x402Version"": 1,
                    ""scheme"": ""invoice"",
                    ""network"": ""ethereum-mainnet""
                }
            ]
        }";

        var response = JsonSerializer.Deserialize<SupportedPaymentKindsResponse>(json, JsonOptions);

        Assert.NotNull(response);
        Assert.NotNull(response!.Kinds);
        Assert.Equal(2, response.Kinds.Count);
        Assert.Equal("exact", response.Kinds[0].Scheme);
        Assert.Equal("base-sepolia", response.Kinds[0].Network);
        Assert.Equal("invoice", response.Kinds[1].Scheme);
        Assert.Equal("ethereum-mainnet", response.Kinds[1].Network);
    }

    [Fact]
    public void Given_SupportedPaymentKindsWithSingleScheme_When_Serialized_Then_ArrayIsMaintained()
    {
        var response = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>
            {
                new() { X402Version = 1, Scheme = "exact", Network = "base-sepolia" }
            }
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SupportedPaymentKindsResponse>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Single(deserialized!.Kinds);
        Assert.Equal("exact", deserialized.Kinds[0].Scheme);
        Assert.Equal("base-sepolia", deserialized.Kinds[0].Network);
    }

    [Fact]
    public void Given_SupportedPaymentKindsWithEmptyArray_When_Serialized_Then_EmptyArrayIsPreserved()
    {
        var response = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>()
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SupportedPaymentKindsResponse>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.Kinds);
        Assert.Empty(deserialized.Kinds);
    }

    [Fact]
    public void Given_SupportedPaymentKindsResponse_When_RoundTripSerialization_Then_AllDataIsPreserved()
    {
        var original = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>
            {
                new() { X402Version = 1, Scheme = "exact", Network = "base-sepolia" },
                new() { X402Version = 1, Scheme = "invoice", Network = "ethereum-mainnet" },
                new() { X402Version = 1, Scheme = "invoice-async", Network = "polygon-mainnet" }
            }
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SupportedPaymentKindsResponse>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.Kinds);
        Assert.Equal(original.Kinds.Count, deserialized.Kinds.Count);
        for (int i = 0; i < original.Kinds.Count; i++)
        {
            Assert.Equal(original.Kinds[i].X402Version, deserialized.Kinds[i].X402Version);
            Assert.Equal(original.Kinds[i].Scheme, deserialized.Kinds[i].Scheme);
            Assert.Equal(original.Kinds[i].Network, deserialized.Kinds[i].Network);
        }
    }
}
