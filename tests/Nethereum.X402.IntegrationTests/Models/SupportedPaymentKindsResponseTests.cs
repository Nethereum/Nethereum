using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Models;

/// <summary>
/// BDD tests for SupportedPaymentKindsResponse model
///
/// Traceability:
/// - Spec: Section 5.5 - GET /supported-payment-kinds Response
/// - Use Case: UC-M7 - SupportedPaymentKindsResponse Object
/// - Requirements: Must contain supportedPaymentKinds array of strings
/// - Implementation: src/Nethereum.X402/Models/SupportedPaymentKindsResponse.cs
/// </summary>
public class SupportedPaymentKindsResponseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Spec: Section 5.5 - SupportedPaymentKindsResponse must have kinds array
    /// Use Case: UC-M7 Scenario 1
    /// </summary>
    [Fact]
    public void Given_ValidSupportedPaymentKindsResponse_When_CreatingObject_Then_AllRequiredFieldsArePresent()
    {
        // Arrange & Act
        var response = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>
            {
                new() { X402Version = 1, Scheme = "exact", Network = "base-sepolia" },
                new() { X402Version = 1, Scheme = "invoice", Network = "ethereum-mainnet" }
            }
        };

        // Assert - Required field must be accessible
        Assert.NotNull(response.Kinds);
        Assert.Equal(2, response.Kinds.Count);
    }

    /// <summary>
    /// Spec: Section 5.5 - Field names must be camelCase in JSON
    /// Use Case: UC-M7 Scenario 1
    /// </summary>
    [Fact]
    public void Given_SupportedPaymentKindsResponse_When_SerializedToJson_Then_FieldNamesMatchSpec()
    {
        // Arrange
        var response = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>
            {
                new() { X402Version = 1, Scheme = "exact", Network = "base-sepolia" },
                new() { X402Version = 1, Scheme = "invoice", Network = "ethereum-mainnet" }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        // Assert - Field names must be camelCase
        Assert.True(jsonObject.ContainsKey("kinds"), "Missing 'kinds' field");
    }

    /// <summary>
    /// Spec: Section 5.5 - Kinds must be array of PaymentKind objects
    /// Use Case: UC-M7 Scenario 1
    /// Requirement: Array type preservation
    /// </summary>
    [Fact]
    public void Given_SupportedPaymentKindsResponse_When_SerializedToJson_Then_SupportedPaymentKindsIsArray()
    {
        // Arrange
        var response = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>
            {
                new() { X402Version = 1, Scheme = "exact", Network = "base-sepolia" },
                new() { X402Version = 1, Scheme = "invoice", Network = "ethereum-mainnet" }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);

        // Assert - Kinds is an array
        Assert.Equal(JsonValueKind.Array, jsonNode!["kinds"]!.GetValueKind());
        var array = jsonNode["kinds"]!.AsArray();
        Assert.Equal(2, array.Count);
        Assert.Equal("exact", array[0]!["scheme"]!.GetValue<string>());
        Assert.Equal("invoice", array[1]!["scheme"]!.GetValue<string>());
    }

    /// <summary>
    /// Spec: Section 5.5 - Example from specification
    /// Use Case: UC-M7 Scenario 1
    /// Requirement: Must deserialize spec-compliant JSON correctly
    /// </summary>
    [Fact]
    public void Given_SpecCompliantJson_When_DeserializedToSupportedPaymentKindsResponse_Then_AllFieldsAreCorrect()
    {
        // Arrange - JSON from spec Section 5.5
        var json = @"{
            ""kinds"": [
                {
                    ""x402Version"": 1,
                    ""scheme"": ""exact"",
                    ""network"": ""base-sepolia""
                },
                {
                    ""x402Version"": 1,
                    ""scheme"": ""invoice"",
                    ""network"": ""ethereum-mainnet""
                }
            ]
        }";

        // Act
        var response = JsonSerializer.Deserialize<SupportedPaymentKindsResponse>(json, JsonOptions);

        // Assert - All fields deserialized correctly
        Assert.NotNull(response);
        Assert.NotNull(response!.Kinds);
        Assert.Equal(2, response.Kinds.Count);
        Assert.Equal("exact", response.Kinds[0].Scheme);
        Assert.Equal("base-sepolia", response.Kinds[0].Network);
        Assert.Equal("invoice", response.Kinds[1].Scheme);
        Assert.Equal("ethereum-mainnet", response.Kinds[1].Network);
    }

    /// <summary>
    /// Spec: Section 5.5 - Array can contain single payment kind
    /// Use Case: UC-M7 Scenario 2
    /// Requirement: Support single-element arrays
    /// </summary>
    [Fact]
    public void Given_SupportedPaymentKindsWithSingleScheme_When_Serialized_Then_ArrayIsMaintained()
    {
        // Arrange
        var response = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>
            {
                new() { X402Version = 1, Scheme = "exact", Network = "base-sepolia" }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SupportedPaymentKindsResponse>(json, JsonOptions);

        // Assert - Single element array preserved
        Assert.NotNull(deserialized);
        Assert.Single(deserialized!.Kinds);
        Assert.Equal("exact", deserialized.Kinds[0].Scheme);
        Assert.Equal("base-sepolia", deserialized.Kinds[0].Network);
    }

    /// <summary>
    /// Spec: Section 5.5 - Array can be empty
    /// Use Case: UC-M7 Scenario 3
    /// Requirement: Support empty arrays
    /// </summary>
    [Fact]
    public void Given_SupportedPaymentKindsWithEmptyArray_When_Serialized_Then_EmptyArrayIsPreserved()
    {
        // Arrange
        var response = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>()
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SupportedPaymentKindsResponse>(json, JsonOptions);

        // Assert - Empty array preserved
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.Kinds);
        Assert.Empty(deserialized.Kinds);
    }

    /// <summary>
    /// Spec: All sections - Round-trip serialization must preserve data
    /// Use Case: UC-M8 - JSON Serialization/Deserialization
    /// Requirement: Native AOT compatibility - no data loss
    /// </summary>
    [Fact]
    public void Given_SupportedPaymentKindsResponse_When_RoundTripSerialization_Then_AllDataIsPreserved()
    {
        // Arrange
        var original = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>
            {
                new() { X402Version = 1, Scheme = "exact", Network = "base-sepolia" },
                new() { X402Version = 1, Scheme = "invoice", Network = "ethereum-mainnet" },
                new() { X402Version = 1, Scheme = "invoice-async", Network = "polygon-mainnet" }
            }
        };

        // Act - Serialize and deserialize
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SupportedPaymentKindsResponse>(json, JsonOptions);

        // Assert - All values preserved
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.Kinds);
        Assert.Equal(original.Kinds.Count, deserialized.Kinds.Count);
        for (int i = 0; i < original.Kinds.Count; i++)
        {
            Assert.Equal(original.Kinds[i].X402Version, deserialized.Kinds[i].X402Version);
            Assert.Equal(original.Kinds[i].Scheme, deserialized.Kinds[i].Scheme);
            Assert.Equal(original.Kinds[i].Network, deserialized.Kinds[i].Network);
        }
    }
}
