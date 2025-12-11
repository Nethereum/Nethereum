using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Models;

/// <summary>
/// BDD tests for SettlementResponse model
///
/// Traceability:
/// - Spec: Section 5.4 - POST /settle Response
/// - Use Case: UC-M6 - SettlementResponse Object
/// - Requirements: Must contain settled field and optional failureReason
/// - Implementation: src/Nethereum.X402/Models/SettlementResponse.cs
/// </summary>
public class SettlementResponseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Spec: Section 5.4 - SettlementResponse must have success field
    /// Use Case: UC-M6 Scenario 1
    /// </summary>
    [Fact]
    public void Given_ValidSettlementResponse_When_CreatingObject_Then_AllRequiredFieldsArePresent()
    {
        // Arrange & Act
        var response = new SettlementResponse
        {
            Success = true,
            Transaction = "0xabc123",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        // Assert - Required fields must be accessible
        Assert.True(response.Success);
        Assert.NotNull(response.Transaction);
        Assert.NotNull(response.Network);
        Assert.NotNull(response.Payer);
    }

    /// <summary>
    /// Spec: Section 5.4 - ErrorReason is optional, only present when success is false
    /// Use Case: UC-M6 Scenario 2
    /// </summary>
    [Fact]
    public void Given_SettlementResponseWithFailure_When_CreatingObject_Then_FailureReasonIsPresent()
    {
        // Arrange & Act
        var response = new SettlementResponse
        {
            Success = false,
            ErrorReason = "Insufficient balance",
            Transaction = "0x",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        // Assert - Optional field must be accessible
        Assert.False(response.Success);
        Assert.NotNull(response.ErrorReason);
        Assert.Equal("Insufficient balance", response.ErrorReason);
    }

    /// <summary>
    /// Spec: Section 5.4 - Field names must be camelCase in JSON
    /// Use Case: UC-M6 Scenario 1
    /// </summary>
    [Fact]
    public void Given_SettlementResponse_When_SerializedToJson_Then_FieldNamesMatchSpec()
    {
        // Arrange
        var response = new SettlementResponse
        {
            Success = true,
            Transaction = "0xabc123",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        // Assert - Field names must be camelCase
        Assert.True(jsonObject.ContainsKey("success"), "Missing 'success' field");
        Assert.True(jsonObject.ContainsKey("transaction"), "Missing 'transaction' field");
        Assert.True(jsonObject.ContainsKey("network"), "Missing 'network' field");
        Assert.True(jsonObject.ContainsKey("payer"), "Missing 'payer' field");
    }

    /// <summary>
    /// Spec: Section 5.4 - Success must be boolean type
    /// Use Case: UC-M6 Scenario 1
    /// Requirement: Preserve boolean type (not string)
    /// </summary>
    [Fact]
    public void Given_SettlementResponse_When_SerializedToJson_Then_SettledFieldIsBoolean()
    {
        // Arrange
        var response = new SettlementResponse
        {
            Success = true,
            Transaction = "0xabc123",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);

        // Assert - Success must be boolean
        Assert.Equal(JsonValueKind.True, jsonNode!["success"]!.GetValueKind());
    }

    /// <summary>
    /// Spec: Section 5.4 - Example from specification (success case)
    /// Use Case: UC-M6 Scenario 1
    /// Requirement: Must deserialize spec-compliant JSON correctly
    /// </summary>
    [Fact]
    public void Given_SpecCompliantSuccessJson_When_DeserializedToSettlementResponse_Then_AllFieldsAreCorrect()
    {
        // Arrange - JSON from spec Section 5.4 (success)
        var json = @"{
            ""success"": true,
            ""transaction"": ""0xabc123"",
            ""network"": ""base-sepolia"",
            ""payer"": ""0x857b06519E91e3A54538791bDbb0E22373e36b66""
        }";

        // Act
        var response = JsonSerializer.Deserialize<SettlementResponse>(json, JsonOptions);

        // Assert - All fields deserialized correctly
        Assert.NotNull(response);
        Assert.True(response!.Success);
        Assert.Null(response.ErrorReason);
        Assert.Equal("0xabc123", response.Transaction);
        Assert.Equal("base-sepolia", response.Network);
        Assert.Equal("0x857b06519E91e3A54538791bDbb0E22373e36b66", response.Payer);
    }

    /// <summary>
    /// Spec: Section 5.4 - Example from specification (failure case)
    /// Use Case: UC-M6 Scenario 2
    /// Requirement: Must deserialize spec-compliant JSON with failure reason
    /// </summary>
    [Fact]
    public void Given_SpecCompliantFailureJson_When_DeserializedToSettlementResponse_Then_AllFieldsAreCorrect()
    {
        // Arrange - JSON from spec Section 5.4 (failure)
        var json = @"{
            ""success"": false,
            ""errorReason"": ""Insufficient balance"",
            ""transaction"": ""0x"",
            ""network"": ""base-sepolia"",
            ""payer"": ""0x857b06519E91e3A54538791bDbb0E22373e36b66""
        }";

        // Act
        var response = JsonSerializer.Deserialize<SettlementResponse>(json, JsonOptions);

        // Assert - All fields deserialized correctly
        Assert.NotNull(response);
        Assert.False(response!.Success);
        Assert.Equal("Insufficient balance", response.ErrorReason);
        Assert.Equal("0x", response.Transaction);
        Assert.Equal("base-sepolia", response.Network);
        Assert.Equal("0x857b06519E91e3A54538791bDbb0E22373e36b66", response.Payer);
    }

    /// <summary>
    /// Spec: Section 5.4 - ErrorReason should be omitted when null
    /// Use Case: UC-M6 Scenario 1
    /// Requirement: Null optional fields omitted from JSON
    /// </summary>
    [Fact]
    public void Given_SettlementResponseWithoutFailure_When_Serialized_Then_FailureReasonIsOmitted()
    {
        // Arrange
        var response = new SettlementResponse
        {
            Success = true,
            ErrorReason = null,
            Transaction = "0xabc123",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        // Assert - Null fields should be omitted
        Assert.False(jsonObject.ContainsKey("errorReason"), "errorReason should be omitted when null");
    }

    /// <summary>
    /// Spec: All sections - Round-trip serialization must preserve data
    /// Use Case: UC-M8 - JSON Serialization/Deserialization
    /// Requirement: Native AOT compatibility - no data loss
    /// </summary>
    [Fact]
    public void Given_SettlementResponse_When_RoundTripSerialization_Then_AllDataIsPreserved()
    {
        // Arrange
        var original = new SettlementResponse
        {
            Success = false,
            ErrorReason = "Insufficient balance",
            Transaction = "0x",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        // Act - Serialize and deserialize
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SettlementResponse>(json, JsonOptions);

        // Assert - All values preserved
        Assert.NotNull(deserialized);
        Assert.Equal(original.Success, deserialized!.Success);
        Assert.Equal(original.ErrorReason, deserialized.ErrorReason);
        Assert.Equal(original.Transaction, deserialized.Transaction);
        Assert.Equal(original.Network, deserialized.Network);
        Assert.Equal(original.Payer, deserialized.Payer);
    }
}
