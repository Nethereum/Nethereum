using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Models;

public class X402V2InteropTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private const string ReferencePaymentRequired = @"{
      ""x402Version"": 2,
      ""error"": ""PAYMENT-SIGNATURE header is required"",
      ""resource"": {
        ""url"": ""https://api.example.com/premium-data"",
        ""description"": ""Access to premium market data"",
        ""mimeType"": ""application/json""
      },
      ""accepts"": [
        {
          ""scheme"": ""exact"",
          ""network"": ""eip155:84532"",
          ""amount"": ""10000"",
          ""asset"": ""0x036CbD53842c5426634e7929541eC2318f3dCF7e"",
          ""payTo"": ""0x209693Bc6afc0C5328bA36FaF03C514EF312287C"",
          ""maxTimeoutSeconds"": 60,
          ""extra"": { ""name"": ""USDC"", ""version"": ""2"" }
        }
      ],
      ""extensions"": {}
    }";

    private const string ReferenceExactEip3009Payload = @"{
      ""x402Version"": 2,
      ""resource"": {
        ""url"": ""https://api.example.com/premium-data"",
        ""description"": ""Access to premium market data"",
        ""mimeType"": ""application/json""
      },
      ""accepted"": {
        ""scheme"": ""exact"",
        ""network"": ""eip155:84532"",
        ""amount"": ""10000"",
        ""asset"": ""0x036CbD53842c5426634e7929541eC2318f3dCF7e"",
        ""payTo"": ""0x209693Bc6afc0C5328bA36FaF03C514EF312287C"",
        ""maxTimeoutSeconds"": 60,
        ""extra"": { ""assetTransferMethod"": ""eip3009"", ""name"": ""USDC"", ""version"": ""2"" }
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
      },
      ""extensions"": {}
    }";

    [Fact]
    public void ReferencePaymentRequired_DeserializesIntoOurModel()
    {
        var pr = JsonSerializer.Deserialize<PaymentRequired>(ReferencePaymentRequired, JsonOptions);

        Assert.NotNull(pr);
        Assert.Equal(2, pr!.X402Version);
        Assert.Equal("https://api.example.com/premium-data", pr.Resource.Url);
        Assert.Equal("application/json", pr.Resource.MimeType);
        Assert.Single(pr.Accepts);
        var req = pr.Accepts[0];
        Assert.Equal("exact", req.Scheme);
        Assert.Equal("eip155:84532", req.Network);
        Assert.Equal("10000", req.Amount);
        Assert.Equal("0x036CbD53842c5426634e7929541eC2318f3dCF7e", req.Asset);
        Assert.Equal("0x209693Bc6afc0C5328bA36FaF03C514EF312287C", req.PayTo);
        Assert.Equal(60, req.MaxTimeoutSeconds);
        var extra = ExactSchemeExtra.FromRequirements(req);
        Assert.Equal("USDC", extra!.Name);
        Assert.Equal("2", extra.Version);
    }

    [Fact]
    public void ReferenceExactEip3009Payload_DeserializesIntoOurModel()
    {
        var payload = JsonSerializer.Deserialize<PaymentPayload>(ReferenceExactEip3009Payload, JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal(2, payload!.X402Version);
        Assert.Equal("exact", payload.Accepted.Scheme);
        Assert.Equal("eip155:84532", payload.Accepted.Network);

        var extra = ExactSchemeExtra.FromRequirements(payload.Accepted);
        Assert.Equal("eip3009", extra!.AssetTransferMethod);
        Assert.Equal("USDC", extra.Name);

        var exact = JsonSerializer.Deserialize<ExactSchemePayload>(
            ((JsonElement)payload.Payload).GetRawText(), JsonOptions);
        Assert.Equal("0x857b06519E91e3A54538791bDbb0E22373e36b66", exact!.Authorization.From);
        Assert.Equal("0x209693Bc6afc0C5328bA36FaF03C514EF312287C", exact.Authorization.To);
        Assert.Equal("10000", exact.Authorization.Value);
        Assert.Equal("1740672089", exact.Authorization.ValidAfter);
        Assert.Equal("1740672154", exact.Authorization.ValidBefore);
        Assert.StartsWith("0x2d6a7588", exact.Signature);
    }

    [Fact]
    public void OurPaymentRequired_SerializesToReferenceKeys()
    {
        var pr = JsonSerializer.Deserialize<PaymentRequired>(ReferencePaymentRequired, JsonOptions)!;
        var obj = JsonNode.Parse(JsonSerializer.Serialize(pr, JsonOptions))!.AsObject();

        Assert.True(obj.ContainsKey("x402Version"));
        Assert.True(obj.ContainsKey("error"));
        Assert.True(obj.ContainsKey("resource"));
        Assert.True(obj.ContainsKey("accepts"));

        var req = obj["accepts"]!.AsArray()[0]!.AsObject();
        foreach (var key in new[] { "scheme", "network", "amount", "asset", "payTo", "maxTimeoutSeconds", "extra" })
            Assert.True(req.ContainsKey(key), $"requirement missing '{key}'");
        Assert.False(req.ContainsKey("maxAmountRequired"));
        Assert.False(req.ContainsKey("resource"));
    }
}
