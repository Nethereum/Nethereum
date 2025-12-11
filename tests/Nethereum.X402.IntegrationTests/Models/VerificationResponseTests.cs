using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Models;

/// <summary>
/// BDD tests for VerificationResponse model
///
/// Traceability:
/// - Spec: Section 5.3 - POST /verify Response
/// - Use Case: UC-M5 - VerificationResponse Object
/// - Requirements: Must contain valid field and optional failureReason
/// - Implementation: src/Nethereum.X402/Models/VerificationResponse.cs
/// </summary>
public class VerificationResponseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Spec: Section 5.3 - VerificationResponse must have isValid field
    /// Use Case: UC-M5 Scenario 1
    /// </summary>
    [Fact]
    public void Given_ValidVerificationResponse_When_CreatingObject_Then_AllRequiredFieldsArePresent()
    {
        // Arrange & Act
        var response = new VerificationResponse
        {
            IsValid = true,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        // Assert - Required fields must be accessible
        Assert.True(response.IsValid);
        Assert.NotNull(response.Payer);
    }

    /// <summary>
    /// Spec: Section 5.3 - InvalidReason is optional, only present when isValid is false
    /// Use Case: UC-M5 Scenario 2
    /// </summary>
    [Fact]
    public void Given_VerificationResponseWithFailure_When_CreatingObject_Then_FailureReasonIsPresent()
    {
        // Arrange & Act
        var response = new VerificationResponse
        {
            IsValid = false,
            InvalidReason = "Invalid signature",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        // Assert - Optional field must be accessible
        Assert.False(response.IsValid);
        Assert.NotNull(response.InvalidReason);
        Assert.Equal("Invalid signature", response.InvalidReason);
    }

    /// <summary>
    /// Spec: Section 5.3 - Field names must be camelCase in JSON
    /// Use Case: UC-M5 Scenario 1
    /// </summary>
    [Fact]
    public void Given_VerificationResponse_When_SerializedToJson_Then_FieldNamesMatchSpec()
    {
        // Arrange
        var response = new VerificationResponse
        {
            IsValid = true,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        // Assert - Field names must be camelCase
        Assert.True(jsonObject.ContainsKey("isValid"), "Missing 'isValid' field");
        Assert.True(jsonObject.ContainsKey("payer"), "Missing 'payer' field");
    }

    /// <summary>
    /// Spec: Section 5.3 - IsValid must be boolean type
    /// Use Case: UC-M5 Scenario 1
    /// Requirement: Preserve boolean type (not string)
    /// </summary>
    [Fact]
    public void Given_VerificationResponse_When_SerializedToJson_Then_ValidFieldIsBoolean()
    {
        // Arrange
        var response = new VerificationResponse
        {
            IsValid = true,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);

        // Assert - IsValid must be boolean
        Assert.Equal(JsonValueKind.True, jsonNode!["isValid"]!.GetValueKind());
    }

    /// <summary>
    /// Spec: Section 5.3 - Example from specification (success case)
    /// Use Case: UC-M5 Scenario 1
    /// Requirement: Must deserialize spec-compliant JSON correctly
    /// </summary>
    [Fact]
    public void Given_SpecCompliantSuccessJson_When_DeserializedToVerificationResponse_Then_AllFieldsAreCorrect()
    {
        // Arrange - JSON from spec Section 5.3 (success)
        var json = @"{
            ""isValid"": true,
            ""payer"": ""0x857b06519E91e3A54538791bDbb0E22373e36b66""
        }";

        // Act
        var response = JsonSerializer.Deserialize<VerificationResponse>(json, JsonOptions);

        // Assert - All fields deserialized correctly
        Assert.NotNull(response);
        Assert.True(response!.IsValid);
        Assert.Null(response.InvalidReason);
        Assert.Equal("0x857b06519E91e3A54538791bDbb0E22373e36b66", response.Payer);
    }

    /// <summary>
    /// Spec: Section 5.3 - Example from specification (failure case)
    /// Use Case: UC-M5 Scenario 2
    /// Requirement: Must deserialize spec-compliant JSON with failure reason
    /// </summary>
    [Fact]
    public void Given_SpecCompliantFailureJson_When_DeserializedToVerificationResponse_Then_AllFieldsAreCorrect()
    {
        // Arrange - JSON from spec Section 5.3 (failure)
        var json = @"{
            ""isValid"": false,
            ""invalidReason"": ""Invalid signature"",
            ""payer"": ""0x857b06519E91e3A54538791bDbb0E22373e36b66""
        }";

        // Act
        var response = JsonSerializer.Deserialize<VerificationResponse>(json, JsonOptions);

        // Assert - All fields deserialized correctly
        Assert.NotNull(response);
        Assert.False(response!.IsValid);
        Assert.Equal("Invalid signature", response.InvalidReason);
        Assert.Equal("0x857b06519E91e3A54538791bDbb0E22373e36b66", response.Payer);
    }

    /// <summary>
    /// Spec: Section 5.3 - InvalidReason should be omitted when null
    /// Use Case: UC-M5 Scenario 1
    /// Requirement: Null optional fields omitted from JSON
    /// </summary>
    [Fact]
    public void Given_VerificationResponseWithoutFailure_When_Serialized_Then_FailureReasonIsOmitted()
    {
        // Arrange
        var response = new VerificationResponse
        {
            IsValid = true,
            InvalidReason = null,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        // Assert - Null fields should be omitted
        Assert.False(jsonObject.ContainsKey("invalidReason"), "invalidReason should be omitted when null");
    }

    /// <summary>
    /// Spec: All sections - Round-trip serialization must preserve data
    /// Use Case: UC-M8 - JSON Serialization/Deserialization
    /// Requirement: Native AOT compatibility - no data loss
    /// </summary>
    [Fact]
    public void Given_VerificationResponse_When_RoundTripSerialization_Then_AllDataIsPreserved()
    {
        // Arrange
        var original = new VerificationResponse
        {
            IsValid = false,
            InvalidReason = "Invalid signature",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        // Act - Serialize and deserialize
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<VerificationResponse>(json, JsonOptions);

        // Assert - All values preserved
        Assert.NotNull(deserialized);
        Assert.Equal(original.IsValid, deserialized!.IsValid);
        Assert.Equal(original.InvalidReason, deserialized.InvalidReason);
        Assert.Equal(original.Payer, deserialized.Payer);
    }
}
