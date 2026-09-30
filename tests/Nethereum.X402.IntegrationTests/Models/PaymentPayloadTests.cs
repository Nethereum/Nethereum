using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Models;

public class PaymentPayloadTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static PaymentPayload Sample() => new()
    {
        X402Version = 2,
        Accepted = new PaymentRequirements
        {
            Scheme = "exact",
            Network = "eip155:84532",
            Amount = "10000",
            Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
            PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            MaxTimeoutSeconds = 60,
            Extra = new { name = "USDC", version = "2" }
        },
        Payload = new ExactSchemePayload
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
        }
    };

    [Fact]
    public void SerializedShape_MatchesV2Spec()
    {
        var json = JsonSerializer.Serialize(Sample(), JsonOptions);
        var obj = JsonNode.Parse(json)!.AsObject();

        Assert.Equal(2, obj["x402Version"]!.GetValue<int>());
        Assert.True(obj.ContainsKey("accepted"));
        Assert.True(obj.ContainsKey("payload"));
        Assert.False(obj.ContainsKey("scheme"));
        Assert.False(obj.ContainsKey("network"));

        var accepted = obj["accepted"]!.AsObject();
        Assert.Equal("exact", accepted["scheme"]!.GetValue<string>());
        Assert.Equal("eip155:84532", accepted["network"]!.GetValue<string>());

        var payload = obj["payload"]!.AsObject();
        Assert.True(payload.ContainsKey("signature"));
        Assert.True(payload.ContainsKey("authorization"));
    }

    [Fact]
    public void DeserializesV2Json()
    {
        var json = @"{
            ""x402Version"": 2,
            ""accepted"": {
                ""scheme"": ""exact"",
                ""network"": ""eip155:84532"",
                ""amount"": ""10000"",
                ""asset"": ""0x036CbD53842c5426634e7929541eC2318f3dCF7e"",
                ""payTo"": ""0x209693Bc6afc0C5328bA36FaF03C514EF312287C"",
                ""maxTimeoutSeconds"": 60
            },
            ""payload"": {
                ""signature"": ""0x2d6a7588d6acca505cbf0d9a4a227e0c52c6c34008c8e8986a1283259764173608a2ce6496642e377d6da8dbbf5836e9bd15092f9ecab05ded3d6293af148b571c"",
                ""authorization"": {
                    ""from"": ""0x857b06519E91e3A54538791bDbb0E22373e36b66"",
                    ""to"": ""0x209693Bc6afc0C5328bA36FaF03C514EF312287C"",
                    ""value"": ""10000"",
                    ""validAfter"": ""1740672089"",
                    ""validBefore"": ""1740672154"",
                    ""nonce"": ""0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480""
                }
            }
        }";

        var payload = JsonSerializer.Deserialize<PaymentPayload>(json, JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal(2, payload!.X402Version);
        Assert.Equal("exact", payload.Accepted.Scheme);
        Assert.Equal("eip155:84532", payload.Accepted.Network);
        Assert.NotNull(payload.Payload);
    }

    [Fact]
    public void RoundTrip_PreservesData()
    {
        var original = Sample();

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PaymentPayload>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.X402Version, deserialized!.X402Version);
        Assert.Equal(original.Accepted.Scheme, deserialized.Accepted.Scheme);
        Assert.Equal(original.Accepted.Amount, deserialized.Accepted.Amount);
        Assert.NotNull(deserialized.Payload);
    }
}
