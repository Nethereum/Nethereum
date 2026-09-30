using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Models;

public class SettlementResponseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Given_ValidSettlementResponse_When_CreatingObject_Then_AllRequiredFieldsArePresent()
    {
        var response = new SettlementResponse
        {
            Success = true,
            Transaction = "0xabc123",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        Assert.True(response.Success);
        Assert.NotNull(response.Transaction);
        Assert.NotNull(response.Network);
        Assert.NotNull(response.Payer);
    }

    [Fact]
    public void Given_SettlementResponseWithFailure_When_CreatingObject_Then_FailureReasonIsPresent()
    {
        var response = new SettlementResponse
        {
            Success = false,
            ErrorReason = "Insufficient balance",
            Transaction = "0x",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        Assert.False(response.Success);
        Assert.NotNull(response.ErrorReason);
        Assert.Equal("Insufficient balance", response.ErrorReason);
    }

    [Fact]
    public void Given_SettlementResponse_When_SerializedToJson_Then_FieldNamesMatchSpec()
    {
        var response = new SettlementResponse
        {
            Success = true,
            Transaction = "0xabc123",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        Assert.True(jsonObject.ContainsKey("success"), "Missing 'success' field");
        Assert.True(jsonObject.ContainsKey("transaction"), "Missing 'transaction' field");
        Assert.True(jsonObject.ContainsKey("network"), "Missing 'network' field");
        Assert.True(jsonObject.ContainsKey("payer"), "Missing 'payer' field");
    }

    [Fact]
    public void Given_SettlementResponse_When_SerializedToJson_Then_SettledFieldIsBoolean()
    {
        var response = new SettlementResponse
        {
            Success = true,
            Transaction = "0xabc123",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);

        Assert.Equal(JsonValueKind.True, jsonNode!["success"]!.GetValueKind());
    }

    [Fact]
    public void Given_SpecCompliantSuccessJson_When_DeserializedToSettlementResponse_Then_AllFieldsAreCorrect()
    {
        var json = @"{
            ""success"": true,
            ""transaction"": ""0xabc123"",
            ""network"": ""base-sepolia"",
            ""payer"": ""0x857b06519E91e3A54538791bDbb0E22373e36b66""
        }";

        var response = JsonSerializer.Deserialize<SettlementResponse>(json, JsonOptions);

        Assert.NotNull(response);
        Assert.True(response!.Success);
        Assert.Null(response.ErrorReason);
        Assert.Equal("0xabc123", response.Transaction);
        Assert.Equal("base-sepolia", response.Network);
        Assert.Equal("0x857b06519E91e3A54538791bDbb0E22373e36b66", response.Payer);
    }

    [Fact]
    public void Given_SpecCompliantFailureJson_When_DeserializedToSettlementResponse_Then_AllFieldsAreCorrect()
    {
        var json = @"{
            ""success"": false,
            ""errorReason"": ""Insufficient balance"",
            ""transaction"": ""0x"",
            ""network"": ""base-sepolia"",
            ""payer"": ""0x857b06519E91e3A54538791bDbb0E22373e36b66""
        }";

        var response = JsonSerializer.Deserialize<SettlementResponse>(json, JsonOptions);

        Assert.NotNull(response);
        Assert.False(response!.Success);
        Assert.Equal("Insufficient balance", response.ErrorReason);
        Assert.Equal("0x", response.Transaction);
        Assert.Equal("base-sepolia", response.Network);
        Assert.Equal("0x857b06519E91e3A54538791bDbb0E22373e36b66", response.Payer);
    }

    [Fact]
    public void Given_SettlementResponseWithoutFailure_When_Serialized_Then_FailureReasonIsOmitted()
    {
        var response = new SettlementResponse
        {
            Success = true,
            ErrorReason = null,
            Transaction = "0xabc123",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        Assert.False(jsonObject.ContainsKey("errorReason"), "errorReason should be omitted when null");
    }

    [Fact]
    public void Given_SettlementResponse_When_RoundTripSerialization_Then_AllDataIsPreserved()
    {
        var original = new SettlementResponse
        {
            Success = false,
            ErrorReason = "Insufficient balance",
            Transaction = "0x",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SettlementResponse>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Success, deserialized!.Success);
        Assert.Equal(original.ErrorReason, deserialized.ErrorReason);
        Assert.Equal(original.Transaction, deserialized.Transaction);
        Assert.Equal(original.Network, deserialized.Network);
        Assert.Equal(original.Payer, deserialized.Payer);
    }
}
