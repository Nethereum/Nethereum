using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Models;

/// <summary>
/// BDD tests for Authorization model (EIP-3009)
///
/// Traceability:
/// - Spec: Section 5.2.2, Section 6.1.1 - EIP-3009 Authorization
/// - Use Case: UC-M4 - ExactSchemePayload and Authorization
/// - Requirements: All authorization fields must be strings (value, validAfter, validBefore, nonce)
/// - Implementation: src/Nethereum.X402/Models/Authorization.cs
/// </summary>
public class AuthorizationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Spec: Section 5.2.2 - All EIP-3009 authorization fields must be present
    /// Use Case: UC-M4 Scenario 2
    /// </summary>
    [Fact]
    public void Given_ValidAuthorization_When_CreatingObject_Then_AllRequiredFieldsArePresent()
    {
        // Arrange & Act
        var authorization = new Authorization
        {
            From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
            To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Value = "10000",
            ValidAfter = "1740672089",
            ValidBefore = "1740672154",
            Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
        };

        // Assert - All required fields must be accessible
        Assert.NotNull(authorization.From);
        Assert.NotNull(authorization.To);
        Assert.NotNull(authorization.Value);
        Assert.NotNull(authorization.ValidAfter);
        Assert.NotNull(authorization.ValidBefore);
        Assert.NotNull(authorization.Nonce);
    }

    /// <summary>
    /// Spec: Section 5.2.2 - Field names must be camelCase in JSON
    /// Use Case: UC-M4 Scenario 3
    /// Requirement: EIP-3009 standard field names
    /// </summary>
    [Fact]
    public void Given_Authorization_When_SerializedToJson_Then_FieldNamesMatchSpec()
    {
        // Arrange
        var authorization = new Authorization
        {
            From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
            To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Value = "10000",
            ValidAfter = "1740672089",
            ValidBefore = "1740672154",
            Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
        };

        // Act
        var json = JsonSerializer.Serialize(authorization, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        // Assert - Field names must be camelCase
        Assert.True(jsonObject.ContainsKey("from"), "Missing 'from' field");
        Assert.True(jsonObject.ContainsKey("to"), "Missing 'to' field");
        Assert.True(jsonObject.ContainsKey("value"), "Missing 'value' field");
        Assert.True(jsonObject.ContainsKey("validAfter"), "Field must be 'validAfter' (camelCase)");
        Assert.True(jsonObject.ContainsKey("validBefore"), "Field must be 'validBefore' (camelCase)");
        Assert.True(jsonObject.ContainsKey("nonce"), "Missing 'nonce' field");
    }

    /// <summary>
    /// Spec: Section 5.2.2 - All numeric fields must be strings (not numbers)
    /// Use Case: UC-M4 Scenario 3
    /// Requirement: Preserve string type for BigInteger compatibility
    /// </summary>
    [Fact]
    public void Given_Authorization_When_SerializedToJson_Then_NumericFieldsAreStrings()
    {
        // Arrange
        var authorization = new Authorization
        {
            From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
            To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Value = "10000",
            ValidAfter = "1740672089",
            ValidBefore = "1740672154",
            Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
        };

        // Act
        var json = JsonSerializer.Serialize(authorization, JsonOptions);
        var jsonNode = JsonNode.Parse(json);

        // Assert - All numeric fields must be strings
        Assert.Equal(JsonValueKind.String, jsonNode!["value"]!.GetValueKind());
        Assert.Equal("10000", jsonNode["value"]!.GetValue<string>());

        Assert.Equal(JsonValueKind.String, jsonNode["validAfter"]!.GetValueKind());
        Assert.Equal("1740672089", jsonNode["validAfter"]!.GetValue<string>());

        Assert.Equal(JsonValueKind.String, jsonNode["validBefore"]!.GetValueKind());
        Assert.Equal("1740672154", jsonNode["validBefore"]!.GetValue<string>());
    }

    /// <summary>
    /// Spec: Section 5.2.2 - Example from specification
    /// Use Case: UC-M4 Scenario 2
    /// Requirement: Must deserialize spec-compliant JSON correctly
    /// </summary>
    [Fact]
    public void Given_SpecCompliantJson_When_DeserializedToAuthorization_Then_AllFieldsAreCorrect()
    {
        // Arrange - JSON from spec Section 5.2
        var json = @"{
            ""from"": ""0x857b06519E91e3A54538791bDbb0E22373e36b66"",
            ""to"": ""0x209693Bc6afc0C5328bA36FaF03C514EF312287C"",
            ""value"": ""10000"",
            ""validAfter"": ""1740672089"",
            ""validBefore"": ""1740672154"",
            ""nonce"": ""0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480""
        }";

        // Act
        var authorization = JsonSerializer.Deserialize<Authorization>(json, JsonOptions);

        // Assert - All fields deserialized correctly
        Assert.NotNull(authorization);
        Assert.Equal("0x857b06519E91e3A54538791bDbb0E22373e36b66", authorization!.From);
        Assert.Equal("0x209693Bc6afc0C5328bA36FaF03C514EF312287C", authorization.To);
        Assert.Equal("10000", authorization.Value);
        Assert.Equal("1740672089", authorization.ValidAfter);
        Assert.Equal("1740672154", authorization.ValidBefore);
        Assert.Equal("0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480", authorization.Nonce);
    }

    /// <summary>
    /// Spec: All sections - Round-trip serialization must preserve data
    /// Use Case: UC-M8 - JSON Serialization/Deserialization
    /// Requirement: Native AOT compatibility - no data loss
    /// </summary>
    [Fact]
    public void Given_Authorization_When_RoundTripSerialization_Then_AllDataIsPreserved()
    {
        // Arrange
        var original = new Authorization
        {
            From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
            To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Value = "10000",
            ValidAfter = "1740672089",
            ValidBefore = "1740672154",
            Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
        };

        // Act - Serialize and deserialize
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Authorization>(json, JsonOptions);

        // Assert - All values preserved
        Assert.NotNull(deserialized);
        Assert.Equal(original.From, deserialized!.From);
        Assert.Equal(original.To, deserialized.To);
        Assert.Equal(original.Value, deserialized.Value);
        Assert.Equal(original.ValidAfter, deserialized.ValidAfter);
        Assert.Equal(original.ValidBefore, deserialized.ValidBefore);
        Assert.Equal(original.Nonce, deserialized.Nonce);
    }

    /// <summary>
    /// Spec: Section 6.1.1 - AuthorisationNonce must be 32-byte hex string
    /// Use Case: UC-M4 Scenario 3
    /// Requirement: Format validation for nonce field
    /// </summary>
    [Fact]
    public void Given_AuthorizationWithNonce_When_Serialized_Then_NonceFormatIsPreserved()
    {
        // Arrange - 32-byte hex string (0x + 64 hex chars)
        var authorization = new Authorization
        {
            From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
            To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Value = "10000",
            ValidAfter = "1740672089",
            ValidBefore = "1740672154",
            Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
        };

        // Act
        var json = JsonSerializer.Serialize(authorization, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Authorization>(json, JsonOptions);

        // Assert - AuthorisationNonce format preserved
        Assert.NotNull(deserialized);
        Assert.StartsWith("0x", deserialized!.Nonce);
        Assert.Equal(66, deserialized.Nonce.Length); // 0x + 64 hex chars
        Assert.Equal(authorization.Nonce, deserialized.Nonce);
    }
}
