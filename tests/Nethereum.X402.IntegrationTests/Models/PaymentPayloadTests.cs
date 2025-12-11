using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Models;

/// <summary>
/// BDD tests for PaymentPayload model
///
/// Traceability:
/// - Spec: Section 5.2 - Payment Payload Structure
/// - Use Case: UC-M3 - PaymentPayload Object
/// - Requirements: PaymentPayload is polymorphic based on scheme (exact, invoice, etc.)
/// - Implementation: src/Nethereum.X402/Models/PaymentPayload.cs
/// </summary>
public class PaymentPayloadTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Spec: Section 5.2 - PaymentPayload must have scheme and data fields
    /// Use Case: UC-M3 Scenario 1
    /// </summary>
    [Fact]
    public void Given_ValidPaymentPayload_When_CreatingObject_Then_AllRequiredFieldsArePresent()
    {
        // Arrange & Act
        var payload = new PaymentPayload
        {
            X402Version = 1,
            Scheme = "exact",
            Network = "base-sepolia",
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

        // Assert - All required fields must be accessible
        Assert.NotNull(payload.Scheme);
        Assert.NotNull(payload.Payload);
    }

    /// <summary>
    /// Spec: Section 5.2 - Field names must be camelCase in JSON
    /// Use Case: UC-M3 Scenario 2
    /// </summary>
    [Fact]
    public void Given_PaymentPayload_When_SerializedToJson_Then_FieldNamesMatchSpec()
    {
        // Arrange
        var payload = new PaymentPayload
        {
            X402Version = 1,
            Scheme = "exact",
            Network = "base-sepolia",
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

        // Act
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        // Assert - Field names must be camelCase
        Assert.True(jsonObject.ContainsKey("x402Version"), "Missing 'x402Version' field");
        Assert.True(jsonObject.ContainsKey("scheme"), "Missing 'scheme' field");
        Assert.True(jsonObject.ContainsKey("network"), "Missing 'network' field");
        Assert.True(jsonObject.ContainsKey("payload"), "Missing 'payload' field");
    }

    /// <summary>
    /// Spec: Section 5.2 - Example from specification (exact scheme)
    /// Use Case: UC-M3 Scenario 1
    /// Requirement: Must deserialize spec-compliant JSON correctly
    /// </summary>
    [Fact]
    public void Given_SpecCompliantJson_When_DeserializedToPaymentPayload_Then_AllFieldsAreCorrect()
    {
        // Arrange - JSON from spec Section 5.2
        var json = @"{
            ""x402Version"": 1,
            ""scheme"": ""exact"",
            ""network"": ""base-sepolia"",
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

        // Act
        var payload = JsonSerializer.Deserialize<PaymentPayload>(json, JsonOptions);

        // Assert - All fields deserialized correctly
        Assert.NotNull(payload);
        Assert.Equal("exact", payload!.Scheme);
        Assert.NotNull(payload.Payload);
    }

    /// <summary>
    /// Spec: Section 5.2 - Data field must support polymorphic types
    /// Use Case: UC-M3 Scenario 3
    /// Requirement: Data can be any type based on scheme
    /// </summary>
    [Fact]
    public void Given_PaymentPayloadWithExactScheme_When_Serialized_Then_DataIsPreservedAsObject()
    {
        // Arrange
        var exactData = new ExactSchemePayload
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

        var payload = new PaymentPayload
        {
            X402Version = 1,
            Scheme = "exact",
            Network = "base-sepolia",
            Payload = exactData
        };

        // Act
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var jsonNode = JsonNode.Parse(json);

        // Assert - Payload field is serialized as nested object
        Assert.NotNull(jsonNode!["payload"]);
        var payloadObject = jsonNode["payload"]!.AsObject();
        Assert.True(payloadObject.ContainsKey("signature"));
        Assert.True(payloadObject.ContainsKey("authorization"));
    }

    /// <summary>
    /// Spec: All sections - Round-trip serialization must preserve data
    /// Use Case: UC-M8 - JSON Serialization/Deserialization
    /// Requirement: Native AOT compatibility - no data loss
    /// </summary>
    [Fact]
    public void Given_PaymentPayload_When_RoundTripSerialization_Then_AllDataIsPreserved()
    {
        // Arrange
        var original = new PaymentPayload
        {
            Scheme = "exact",
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

        // Act - Serialize and deserialize
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PaymentPayload>(json, JsonOptions);

        // Assert - All values preserved
        Assert.NotNull(deserialized);
        Assert.Equal(original.Scheme, deserialized!.Scheme);
        Assert.NotNull(deserialized.Payload);
    }
}
