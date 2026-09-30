using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Models;

public class VerificationResponseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Given_ValidVerificationResponse_When_CreatingObject_Then_AllRequiredFieldsArePresent()
    {
        var response = new VerificationResponse
        {
            IsValid = true,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        Assert.True(response.IsValid);
        Assert.NotNull(response.Payer);
    }

    [Fact]
    public void Given_VerificationResponseWithFailure_When_CreatingObject_Then_FailureReasonIsPresent()
    {
        var response = new VerificationResponse
        {
            IsValid = false,
            InvalidReason = "Invalid signature",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        Assert.False(response.IsValid);
        Assert.NotNull(response.InvalidReason);
        Assert.Equal("Invalid signature", response.InvalidReason);
    }

    [Fact]
    public void Given_VerificationResponse_When_SerializedToJson_Then_FieldNamesMatchSpec()
    {
        var response = new VerificationResponse
        {
            IsValid = true,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        Assert.True(jsonObject.ContainsKey("isValid"), "Missing 'isValid' field");
        Assert.True(jsonObject.ContainsKey("payer"), "Missing 'payer' field");
    }

    [Fact]
    public void Given_VerificationResponse_When_SerializedToJson_Then_ValidFieldIsBoolean()
    {
        var response = new VerificationResponse
        {
            IsValid = true,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);

        Assert.Equal(JsonValueKind.True, jsonNode!["isValid"]!.GetValueKind());
    }

    [Fact]
    public void Given_SpecCompliantSuccessJson_When_DeserializedToVerificationResponse_Then_AllFieldsAreCorrect()
    {
        var json = @"{
            ""isValid"": true,
            ""payer"": ""0x857b06519E91e3A54538791bDbb0E22373e36b66""
        }";

        var response = JsonSerializer.Deserialize<VerificationResponse>(json, JsonOptions);

        Assert.NotNull(response);
        Assert.True(response!.IsValid);
        Assert.Null(response.InvalidReason);
        Assert.Equal("0x857b06519E91e3A54538791bDbb0E22373e36b66", response.Payer);
    }

    [Fact]
    public void Given_SpecCompliantFailureJson_When_DeserializedToVerificationResponse_Then_AllFieldsAreCorrect()
    {
        var json = @"{
            ""isValid"": false,
            ""invalidReason"": ""Invalid signature"",
            ""payer"": ""0x857b06519E91e3A54538791bDbb0E22373e36b66""
        }";

        var response = JsonSerializer.Deserialize<VerificationResponse>(json, JsonOptions);

        Assert.NotNull(response);
        Assert.False(response!.IsValid);
        Assert.Equal("Invalid signature", response.InvalidReason);
        Assert.Equal("0x857b06519E91e3A54538791bDbb0E22373e36b66", response.Payer);
    }

    [Fact]
    public void Given_VerificationResponseWithoutFailure_When_Serialized_Then_FailureReasonIsOmitted()
    {
        var response = new VerificationResponse
        {
            IsValid = true,
            InvalidReason = null,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        Assert.False(jsonObject.ContainsKey("invalidReason"), "invalidReason should be omitted when null");
    }

    [Fact]
    public void Given_VerificationResponse_When_RoundTripSerialization_Then_AllDataIsPreserved()
    {
        var original = new VerificationResponse
        {
            IsValid = false,
            InvalidReason = "Invalid signature",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<VerificationResponse>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.IsValid, deserialized!.IsValid);
        Assert.Equal(original.InvalidReason, deserialized.InvalidReason);
        Assert.Equal(original.Payer, deserialized.Payer);
    }
}
