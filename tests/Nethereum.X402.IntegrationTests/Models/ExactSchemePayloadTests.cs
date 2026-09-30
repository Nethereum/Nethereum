using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Models;

public class ExactSchemePayloadTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Given_ValidExactSchemePayload_When_CreatingObject_Then_AllRequiredFieldsArePresent()
    {
        var payload = new ExactSchemePayload
        {
            Signature = "0x2d6a7588d6acca505cbf0d9a4a227e0c52c6c34008c8e8986a1283259764173608a2ce6496642e377d6da8dbbf5836e9bd15092f9ecab05ded3d6293af148b571c",
            Authorization = new Authorization
            {
                From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
                To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                Value = "10000",
                ValidAfter = "1740672089",
                ValidBefore = "1740672154",
                Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
            }
        };

        Assert.NotNull(payload.Signature);
        Assert.NotNull(payload.Authorization);
        Assert.NotNull(payload.Authorization.From);
    }

    [Fact]
    public void Given_ExactSchemePayload_When_SerializedToJson_Then_FieldNamesMatchSpec()
    {
        var payload = new ExactSchemePayload
        {
            Signature = "0x2d6a7588d6acca505cbf0d9a4a227e0c52c6c34008c8e8986a1283259764173608a2ce6496642e377d6da8dbbf5836e9bd15092f9ecab05ded3d6293af148b571c",
            Authorization = new Authorization
            {
                From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
                To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                Value = "10000",
                ValidAfter = "1740672089",
                ValidBefore = "1740672154",
                Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
            }
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        Assert.True(jsonObject.ContainsKey("signature"), "Missing 'signature' field");
        Assert.True(jsonObject.ContainsKey("authorization"), "Missing 'authorization' field");

        var authObject = jsonObject["authorization"]!.AsObject();
        Assert.True(authObject.ContainsKey("from"), "Missing 'from' in authorization");
        Assert.True(authObject.ContainsKey("to"), "Missing 'to' in authorization");
        Assert.True(authObject.ContainsKey("value"), "Missing 'value' in authorization");
    }

    [Fact]
    public void Given_SpecCompliantJson_When_DeserializedToExactSchemePayload_Then_AllFieldsAreCorrect()
    {
        var json = @"{
            ""signature"": ""0x2d6a7588d6acca505cbf0d9a4a227e0c52c6c34008c8e8986a1283259764173608a2ce6496642e377d6da8dbbf5836e9bd15092f9ecab05ded3d6293af148b571c"",
            ""authorization"": {
                ""from"": ""0x857b06519E91e3A54538791bDbb0E22373e36b66"",
                ""to"": ""0x209693Bc6afc0C5328bA36FaF03C514EF312287C"",
                ""value"": ""10000"",
                ""validAfter"": ""1740672089"",
                ""validBefore"": ""1740672154"",
                ""nonce"": ""0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480""
            }
        }";

        var payload = JsonSerializer.Deserialize<ExactSchemePayload>(json, JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal("0x2d6a7588d6acca505cbf0d9a4a227e0c52c6c34008c8e8986a1283259764173608a2ce6496642e377d6da8dbbf5836e9bd15092f9ecab05ded3d6293af148b571c", payload!.Signature);
        Assert.NotNull(payload.Authorization);
        Assert.Equal("0x857b06519E91e3A54538791bDbb0E22373e36b66", payload.Authorization.From);
        Assert.Equal("10000", payload.Authorization.Value);
    }

    [Fact]
    public void Given_ExactSchemePayloadWithSignature_When_Serialized_Then_SignatureFormatIsPreserved()
    {
        var signature = "0x2d6a7588d6acca505cbf0d9a4a227e0c52c6c34008c8e8986a1283259764173608a2ce6496642e377d6da8dbbf5836e9bd15092f9ecab05ded3d6293af148b571c";
        var payload = new ExactSchemePayload
        {
            Signature = signature,
            Authorization = new Authorization
            {
                From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
                To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                Value = "10000",
                ValidAfter = "1740672089",
                ValidBefore = "1740672154",
                Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
            }
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ExactSchemePayload>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.StartsWith("0x", deserialized!.Signature);
        Assert.Equal(132, deserialized.Signature.Length);
        Assert.Equal(signature, deserialized.Signature);
    }

    [Fact]
    public void Given_ExactSchemePayload_When_RoundTripSerialization_Then_AllDataIsPreserved()
    {
        var original = new ExactSchemePayload
        {
            Signature = "0x2d6a7588d6acca505cbf0d9a4a227e0c52c6c34008c8e8986a1283259764173608a2ce6496642e377d6da8dbbf5836e9bd15092f9ecab05ded3d6293af148b571c",
            Authorization = new Authorization
            {
                From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
                To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                Value = "10000",
                ValidAfter = "1740672089",
                ValidBefore = "1740672154",
                Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
            }
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ExactSchemePayload>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Signature, deserialized!.Signature);
        Assert.Equal(original.Authorization.From, deserialized.Authorization.From);
        Assert.Equal(original.Authorization.To, deserialized.Authorization.To);
        Assert.Equal(original.Authorization.Value, deserialized.Authorization.Value);
        Assert.Equal(original.Authorization.ValidAfter, deserialized.Authorization.ValidAfter);
        Assert.Equal(original.Authorization.ValidBefore, deserialized.Authorization.ValidBefore);
        Assert.Equal(original.Authorization.Nonce, deserialized.Authorization.Nonce);
    }
}
