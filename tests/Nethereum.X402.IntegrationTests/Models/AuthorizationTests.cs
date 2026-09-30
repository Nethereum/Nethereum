using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Models;

public class AuthorizationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Given_ValidAuthorization_When_CreatingObject_Then_AllRequiredFieldsArePresent()
    {
        var authorization = new Authorization
        {
            From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
            To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Value = "10000",
            ValidAfter = "1740672089",
            ValidBefore = "1740672154",
            Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
        };

        Assert.NotNull(authorization.From);
        Assert.NotNull(authorization.To);
        Assert.NotNull(authorization.Value);
        Assert.NotNull(authorization.ValidAfter);
        Assert.NotNull(authorization.ValidBefore);
        Assert.NotNull(authorization.Nonce);
    }

    [Fact]
    public void Given_Authorization_When_SerializedToJson_Then_FieldNamesMatchSpec()
    {
        var authorization = new Authorization
        {
            From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
            To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Value = "10000",
            ValidAfter = "1740672089",
            ValidBefore = "1740672154",
            Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
        };

        var json = JsonSerializer.Serialize(authorization, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        Assert.True(jsonObject.ContainsKey("from"), "Missing 'from' field");
        Assert.True(jsonObject.ContainsKey("to"), "Missing 'to' field");
        Assert.True(jsonObject.ContainsKey("value"), "Missing 'value' field");
        Assert.True(jsonObject.ContainsKey("validAfter"), "Field must be 'validAfter' (camelCase)");
        Assert.True(jsonObject.ContainsKey("validBefore"), "Field must be 'validBefore' (camelCase)");
        Assert.True(jsonObject.ContainsKey("nonce"), "Missing 'nonce' field");
    }

    [Fact]
    public void Given_Authorization_When_SerializedToJson_Then_NumericFieldsAreStrings()
    {
        var authorization = new Authorization
        {
            From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
            To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Value = "10000",
            ValidAfter = "1740672089",
            ValidBefore = "1740672154",
            Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
        };

        var json = JsonSerializer.Serialize(authorization, JsonOptions);
        var jsonNode = JsonNode.Parse(json);

        Assert.Equal(JsonValueKind.String, jsonNode!["value"]!.GetValueKind());
        Assert.Equal("10000", jsonNode["value"]!.GetValue<string>());

        Assert.Equal(JsonValueKind.String, jsonNode["validAfter"]!.GetValueKind());
        Assert.Equal("1740672089", jsonNode["validAfter"]!.GetValue<string>());

        Assert.Equal(JsonValueKind.String, jsonNode["validBefore"]!.GetValueKind());
        Assert.Equal("1740672154", jsonNode["validBefore"]!.GetValue<string>());
    }

    [Fact]
    public void Given_SpecCompliantJson_When_DeserializedToAuthorization_Then_AllFieldsAreCorrect()
    {
        var json = @"{
            ""from"": ""0x857b06519E91e3A54538791bDbb0E22373e36b66"",
            ""to"": ""0x209693Bc6afc0C5328bA36FaF03C514EF312287C"",
            ""value"": ""10000"",
            ""validAfter"": ""1740672089"",
            ""validBefore"": ""1740672154"",
            ""nonce"": ""0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480""
        }";

        var authorization = JsonSerializer.Deserialize<Authorization>(json, JsonOptions);

        Assert.NotNull(authorization);
        Assert.Equal("0x857b06519E91e3A54538791bDbb0E22373e36b66", authorization!.From);
        Assert.Equal("0x209693Bc6afc0C5328bA36FaF03C514EF312287C", authorization.To);
        Assert.Equal("10000", authorization.Value);
        Assert.Equal("1740672089", authorization.ValidAfter);
        Assert.Equal("1740672154", authorization.ValidBefore);
        Assert.Equal("0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480", authorization.Nonce);
    }

    [Fact]
    public void Given_Authorization_When_RoundTripSerialization_Then_AllDataIsPreserved()
    {
        var original = new Authorization
        {
            From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
            To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Value = "10000",
            ValidAfter = "1740672089",
            ValidBefore = "1740672154",
            Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Authorization>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.From, deserialized!.From);
        Assert.Equal(original.To, deserialized.To);
        Assert.Equal(original.Value, deserialized.Value);
        Assert.Equal(original.ValidAfter, deserialized.ValidAfter);
        Assert.Equal(original.ValidBefore, deserialized.ValidBefore);
        Assert.Equal(original.Nonce, deserialized.Nonce);
    }

    [Fact]
    public void Given_AuthorizationWithNonce_When_Serialized_Then_NonceFormatIsPreserved()
    {
        var authorization = new Authorization
        {
            From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
            To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Value = "10000",
            ValidAfter = "1740672089",
            ValidBefore = "1740672154",
            Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
        };

        var json = JsonSerializer.Serialize(authorization, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Authorization>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.StartsWith("0x", deserialized!.Nonce);
        Assert.Equal(66, deserialized.Nonce.Length);
        Assert.Equal(authorization.Nonce, deserialized.Nonce);
    }
}
