using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Models;

/// <summary>
/// BDD tests for ExactSchemePayload model
///
/// Traceability:
/// - Spec: Section 5.2.2 - Exact scheme payload structure
/// - Use Case: UC-M4 - ExactSchemePayload and Authorization
/// - Requirements: Must contain signature and authorization fields
/// - Implementation: src/Nethereum.X402/Models/ExactSchemePayload.cs
/// </summary>
public class ExactSchemePayloadTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Spec: Section 5.2.2 - ExactSchemePayload must have signature and authorization
    /// Use Case: UC-M4 Scenario 1
    /// </summary>
    [Fact]
    public void Given_ValidExactSchemePayload_When_CreatingObject_Then_AllRequiredFieldsArePresent()
    {
        // Arrange & Act
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

        // Assert - All required fields must be accessible
        Assert.NotNull(payload.Signature);
        Assert.NotNull(payload.Authorization);
        Assert.NotNull(payload.Authorization.From);
    }

    /// <summary>
    /// Spec: Section 5.2.2 - Field names must be camelCase in JSON
    /// Use Case: UC-M4 Scenario 1
    /// </summary>
    [Fact]
    public void Given_ExactSchemePayload_When_SerializedToJson_Then_FieldNamesMatchSpec()
    {
        // Arrange
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

        // Act
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        // Assert - Field names must be camelCase
        Assert.True(jsonObject.ContainsKey("signature"), "Missing 'signature' field");
        Assert.True(jsonObject.ContainsKey("authorization"), "Missing 'authorization' field");

        // Assert - Authorization is nested object with correct fields
        var authObject = jsonObject["authorization"]!.AsObject();
        Assert.True(authObject.ContainsKey("from"), "Missing 'from' in authorization");
        Assert.True(authObject.ContainsKey("to"), "Missing 'to' in authorization");
        Assert.True(authObject.ContainsKey("value"), "Missing 'value' in authorization");
    }

    /// <summary>
    /// Spec: Section 5.2 - Example from specification
    /// Use Case: UC-M4 Scenario 1
    /// Requirement: Must deserialize spec-compliant JSON correctly
    /// </summary>
    [Fact]
    public void Given_SpecCompliantJson_When_DeserializedToExactSchemePayload_Then_AllFieldsAreCorrect()
    {
        // Arrange - JSON from spec Section 5.2
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

        // Act
        var payload = JsonSerializer.Deserialize<ExactSchemePayload>(json, JsonOptions);

        // Assert - All fields deserialized correctly
        Assert.NotNull(payload);
        Assert.Equal("0x2d6a7588d6acca505cbf0d9a4a227e0c52c6c34008c8e8986a1283259764173608a2ce6496642e377d6da8dbbf5836e9bd15092f9ecab05ded3d6293af148b571c", payload!.Signature);
        Assert.NotNull(payload.Authorization);
        Assert.Equal("0x857b06519E91e3A54538791bDbb0E22373e36b66", payload.Authorization.From);
        Assert.Equal("10000", payload.Authorization.Value);
    }

    /// <summary>
    /// Spec: Section 6.1.1 - Signature must be EIP-712 signature (65 bytes = 132 chars with 0x)
    /// Use Case: UC-M4 Scenario 4
    /// Requirement: Signature format validation
    /// </summary>
    [Fact]
    public void Given_ExactSchemePayloadWithSignature_When_Serialized_Then_SignatureFormatIsPreserved()
    {
        // Arrange - 65-byte hex string (0x + 130 hex chars)
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

        // Act
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ExactSchemePayload>(json, JsonOptions);

        // Assert - Signature format preserved
        Assert.NotNull(deserialized);
        Assert.StartsWith("0x", deserialized!.Signature);
        Assert.Equal(132, deserialized.Signature.Length); // 0x + 130 hex chars = 65 bytes
        Assert.Equal(signature, deserialized.Signature);
    }

    /// <summary>
    /// Spec: All sections - Round-trip serialization must preserve data
    /// Use Case: UC-M8 - JSON Serialization/Deserialization
    /// Requirement: Native AOT compatibility - no data loss
    /// </summary>
    [Fact]
    public void Given_ExactSchemePayload_When_RoundTripSerialization_Then_AllDataIsPreserved()
    {
        // Arrange
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

        // Act - Serialize and deserialize
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ExactSchemePayload>(json, JsonOptions);

        // Assert - All values preserved
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
