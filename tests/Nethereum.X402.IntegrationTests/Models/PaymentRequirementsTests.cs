using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Models;

/// <summary>
/// BDD tests for PaymentRequirements model
///
/// Traceability:
/// - Spec: Section 5.1.2 - PaymentRequirements Schema
/// - Use Case: UC-M2 - PaymentRequirements Object
/// - Requirements: All fields must serialize/deserialize with exact spec field names (camelCase)
/// - Implementation: src/Nethereum.X402/Models/PaymentRequirements.cs
/// </summary>
public class PaymentRequirementsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Spec: Section 5.1.2 - All required fields must be present
    /// Use Case: UC-M2 Scenario 1
    /// </summary>
    [Fact]
    public void Given_ValidPaymentRequirements_When_CreatingObject_Then_AllRequiredFieldsArePresent()
    {
        // Arrange & Act
        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = "base-sepolia",
            MaxAmountRequired = "10000",
            Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
            PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Resource = "https://api.example.com/premium-data",
            Description = "Access to premium market data",
            MaxTimeoutSeconds = 60
        };

        // Assert - All required fields must be accessible
        Assert.NotNull(requirements.Scheme);
        Assert.NotNull(requirements.Network);
        Assert.NotNull(requirements.MaxAmountRequired);
        Assert.NotNull(requirements.Asset);
        Assert.NotNull(requirements.PayTo);
        Assert.NotNull(requirements.Resource);
        Assert.NotNull(requirements.Description);
        Assert.True(requirements.MaxTimeoutSeconds > 0);
    }

    /// <summary>
    /// Spec: Section 5.1.2 - Optional fields (mimeType, outputSchema, extra) can be null
    /// Use Case: UC-M2 Scenario 1
    /// </summary>
    [Fact]
    public void Given_PaymentRequirements_When_OptionalFieldsAreNull_Then_ObjectIsValid()
    {
        // Arrange & Act
        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = "base-sepolia",
            MaxAmountRequired = "10000",
            Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
            PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Resource = "https://api.example.com/premium-data",
            Description = "Access to premium market data",
            MaxTimeoutSeconds = 60,
            MimeType = null,
            OutputSchema = null,
            Extra = null
        };

        // Assert - Optional fields can be null
        Assert.Null(requirements.MimeType);
        Assert.Null(requirements.OutputSchema);
        Assert.Null(requirements.Extra);
    }

    /// <summary>
    /// Spec: Section 5.1.2 - Field names must be camelCase in JSON
    /// Use Case: UC-M2 Scenario 2
    /// Requirement: Native AOT serialization compatibility
    /// </summary>
    [Fact]
    public void Given_PaymentRequirements_When_SerializedToJson_Then_FieldNamesMatchSpecExactly()
    {
        // Arrange
        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = "base-sepolia",
            MaxAmountRequired = "10000",
            Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
            PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Resource = "https://api.example.com/premium-data",
            Description = "Access to premium market data",
            MimeType = "application/json",
            MaxTimeoutSeconds = 60,
            Extra = new { name = "USDC", version = "2" }
        };

        // Act
        var json = JsonSerializer.Serialize(requirements, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        // Assert - Field names must be camelCase and match spec
        Assert.True(jsonObject.ContainsKey("scheme"), "Missing 'scheme' field");
        Assert.True(jsonObject.ContainsKey("network"), "Missing 'network' field");
        Assert.True(jsonObject.ContainsKey("maxAmountRequired"), "Field must be 'maxAmountRequired' (camelCase)");
        Assert.True(jsonObject.ContainsKey("asset"), "Missing 'asset' field");
        Assert.True(jsonObject.ContainsKey("payTo"), "Field must be 'payTo' (camelCase)");
        Assert.True(jsonObject.ContainsKey("resource"), "Missing 'resource' field");
        Assert.True(jsonObject.ContainsKey("description"), "Missing 'description' field");
        Assert.True(jsonObject.ContainsKey("mimeType"), "Field must be 'mimeType' (camelCase)");
        Assert.True(jsonObject.ContainsKey("maxTimeoutSeconds"), "Field must be 'maxTimeoutSeconds' (camelCase)");
        Assert.True(jsonObject.ContainsKey("extra"), "Missing 'extra' field");

        // Assert - No PascalCase fields should exist
        Assert.False(jsonObject.ContainsKey("MaxAmountRequired"), "Should not have PascalCase field");
        Assert.False(jsonObject.ContainsKey("PayTo"), "Should not have PascalCase field");
        Assert.False(jsonObject.ContainsKey("MimeType"), "Should not have PascalCase field");
        Assert.False(jsonObject.ContainsKey("MaxTimeoutSeconds"), "Should not have PascalCase field");
    }

    /// <summary>
    /// Spec: Section 5.1.2 - maxAmountRequired must be string (not number)
    /// Use Case: UC-M2 Scenario 3
    /// Requirement: Preserve string type for large numbers (BigInteger compatibility)
    /// </summary>
    [Fact]
    public void Given_PaymentRequirementsWithAmount_When_SerializedToJson_Then_AmountIsString()
    {
        // Arrange
        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = "base-sepolia",
            MaxAmountRequired = "10000",
            Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
            PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Resource = "https://api.example.com/premium-data",
            Description = "Access to premium market data",
            MaxTimeoutSeconds = 60
        };

        // Act
        var json = JsonSerializer.Serialize(requirements, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var maxAmountNode = jsonNode!["maxAmountRequired"];

        // Assert - maxAmountRequired must be string, not number
        Assert.NotNull(maxAmountNode);
        Assert.Equal(JsonValueKind.String, maxAmountNode.GetValueKind());
        Assert.Equal("10000", maxAmountNode.GetValue<string>());
    }

    /// <summary>
    /// Spec: Section 5.1 - Example JSON from specification
    /// Use Case: UC-M2 Scenario 2
    /// Requirement: Must deserialize spec-compliant JSON correctly
    /// </summary>
    [Fact]
    public void Given_SpecCompliantJson_When_DeserializedToPaymentRequirements_Then_AllFieldsAreCorrect()
    {
        // Arrange - JSON from spec Section 5.1
        var json = @"{
            ""scheme"": ""exact"",
            ""network"": ""base-sepolia"",
            ""maxAmountRequired"": ""10000"",
            ""asset"": ""0x036CbD53842c5426634e7929541eC2318f3dCF7e"",
            ""payTo"": ""0x209693Bc6afc0C5328bA36FaF03C514EF312287C"",
            ""resource"": ""https://api.example.com/premium-data"",
            ""description"": ""Access to premium market data"",
            ""mimeType"": ""application/json"",
            ""outputSchema"": null,
            ""maxTimeoutSeconds"": 60,
            ""extra"": {
                ""name"": ""USDC"",
                ""version"": ""2""
            }
        }";

        // Act
        var requirements = JsonSerializer.Deserialize<PaymentRequirements>(json, JsonOptions);

        // Assert - All fields deserialized correctly
        Assert.NotNull(requirements);
        Assert.Equal("exact", requirements!.Scheme);
        Assert.Equal("base-sepolia", requirements.Network);
        Assert.Equal("10000", requirements.MaxAmountRequired);
        Assert.Equal("0x036CbD53842c5426634e7929541eC2318f3dCF7e", requirements.Asset);
        Assert.Equal("0x209693Bc6afc0C5328bA36FaF03C514EF312287C", requirements.PayTo);
        Assert.Equal("https://api.example.com/premium-data", requirements.Resource);
        Assert.Equal("Access to premium market data", requirements.Description);
        Assert.Equal("application/json", requirements.MimeType);
        Assert.Null(requirements.OutputSchema);
        Assert.Equal(60, requirements.MaxTimeoutSeconds);
        Assert.NotNull(requirements.Extra);
    }

    /// <summary>
    /// Spec: All sections - Round-trip serialization must preserve data
    /// Use Case: UC-M8 - JSON Serialization/Deserialization
    /// Requirement: Native AOT compatibility - no data loss
    /// </summary>
    [Fact]
    public void Given_PaymentRequirements_When_RoundTripSerialization_Then_AllDataIsPreserved()
    {
        // Arrange
        var original = new PaymentRequirements
        {
            Scheme = "exact",
            Network = "base-sepolia",
            MaxAmountRequired = "10000",
            Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
            PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Resource = "https://api.example.com/premium-data",
            Description = "Access to premium market data",
            MimeType = "application/json",
            MaxTimeoutSeconds = 60
        };

        // Act - Serialize and deserialize
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PaymentRequirements>(json, JsonOptions);

        // Assert - All values preserved
        Assert.NotNull(deserialized);
        Assert.Equal(original.Scheme, deserialized!.Scheme);
        Assert.Equal(original.Network, deserialized.Network);
        Assert.Equal(original.MaxAmountRequired, deserialized.MaxAmountRequired);
        Assert.Equal(original.Asset, deserialized.Asset);
        Assert.Equal(original.PayTo, deserialized.PayTo);
        Assert.Equal(original.Resource, deserialized.Resource);
        Assert.Equal(original.Description, deserialized.Description);
        Assert.Equal(original.MimeType, deserialized.MimeType);
        Assert.Equal(original.MaxTimeoutSeconds, deserialized.MaxTimeoutSeconds);
    }

    /// <summary>
    /// Requirement: Native AOT - Null optional fields should be omitted from JSON
    /// Use Case: UC-M8 Scenario 3
    /// </summary>
    [Fact]
    public void Given_PaymentRequirementsWithNullOptionalFields_When_Serialized_Then_NullFieldsOmitted()
    {
        // Arrange
        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = "base-sepolia",
            MaxAmountRequired = "10000",
            Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
            PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            Resource = "https://api.example.com/premium-data",
            Description = "Access to premium market data",
            MaxTimeoutSeconds = 60,
            MimeType = null,
            OutputSchema = null,
            Extra = null
        };

        // Act
        var json = JsonSerializer.Serialize(requirements, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        // Assert - Null optional fields should not be present
        Assert.False(jsonObject.ContainsKey("mimeType"), "Null mimeType should be omitted");
        Assert.False(jsonObject.ContainsKey("outputSchema"), "Null outputSchema should be omitted");
        Assert.False(jsonObject.ContainsKey("extra"), "Null extra should be omitted");

        // Assert - Required fields should still be present
        Assert.True(jsonObject.ContainsKey("scheme"));
        Assert.True(jsonObject.ContainsKey("network"));
        Assert.True(jsonObject.ContainsKey("maxAmountRequired"));
    }
}
