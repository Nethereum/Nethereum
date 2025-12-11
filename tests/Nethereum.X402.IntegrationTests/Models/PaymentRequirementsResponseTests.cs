using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Models;

/// <summary>
/// BDD tests for PaymentRequirementsResponse model
///
/// Traceability:
/// - Spec: Section 5.1 - GET /x402 Response (402 Payment Required)
/// - Use Case: UC-M1 - PaymentRequirementsResponse Object
/// - Requirements: Must contain x402Version, error, and accepts fields
/// - Implementation: src/Nethereum.X402/Models/PaymentRequirementsResponse.cs
/// </summary>
public class PaymentRequirementsResponseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Spec: Section 5.1 - PaymentRequirementsResponse must have x402Version, error, and accepts fields
    /// Use Case: UC-M1 Scenario 1
    /// </summary>
    [Fact]
    public void Given_ValidPaymentRequirementsResponse_When_CreatingObject_Then_AllRequiredFieldsArePresent()
    {
        // Arrange & Act
        var response = new PaymentRequirementsResponse
        {
            X402Version = 1,
            Error = "",
            Accepts = new List<PaymentRequirements>
            {
                new PaymentRequirements
                {
                    Scheme = "exact",
                    Network = "sepolia",
                    MaxAmountRequired = "10000",
                    Asset = "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238",
                    PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                    Resource = "https://resource-server.com/data",
                    Description = "Access to premium content",
                    MaxTimeoutSeconds = 300
                }
            }
        };

        // Assert - Required fields must be accessible
        Assert.Equal(1, response.X402Version);
        Assert.NotNull(response.Error);
        Assert.NotNull(response.Accepts);
        Assert.Single(response.Accepts);
        Assert.Equal("exact", response.Accepts[0].Scheme);
    }

    /// <summary>
    /// Spec: Section 5.1 - Field names must be camelCase in JSON
    /// Use Case: UC-M1 Scenario 1
    /// </summary>
    [Fact]
    public void Given_PaymentRequirementsResponse_When_SerializedToJson_Then_FieldNamesMatchSpec()
    {
        // Arrange
        var response = new PaymentRequirementsResponse
        {
            X402Version = 1,
            Error = "",
            Accepts = new List<PaymentRequirements>
            {
                new PaymentRequirements
                {
                    Scheme = "exact",
                    Network = "sepolia",
                    MaxAmountRequired = "10000",
                    Asset = "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238",
                    PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                    Resource = "https://resource-server.com/data",
                    Description = "Access to premium content",
                    MaxTimeoutSeconds = 300
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        var jsonObject = jsonNode!.AsObject();

        // Assert - Field names must match spec
        Assert.True(jsonObject.ContainsKey("x402Version"), "Missing 'x402Version' field");
        Assert.True(jsonObject.ContainsKey("error"), "Missing 'error' field");
        Assert.True(jsonObject.ContainsKey("accepts"), "Missing 'accepts' field");

        // Assert - Accepts is an array with nested objects containing expected fields
        var acceptsArray = jsonObject["accepts"]!.AsArray();
        Assert.NotEmpty(acceptsArray);
        var firstAccept = acceptsArray[0]!.AsObject();
        Assert.True(firstAccept.ContainsKey("scheme"));
        Assert.True(firstAccept.ContainsKey("network"));
    }

    /// <summary>
    /// Spec: Section 5.1 - Example from specification
    /// Use Case: UC-M1 Scenario 1
    /// Requirement: Must deserialize spec-compliant JSON correctly
    /// </summary>
    [Fact]
    public void Given_SpecCompliantJson_When_DeserializedToPaymentRequirementsResponse_Then_AllFieldsAreCorrect()
    {
        // Arrange - JSON from spec Section 5.1
        var json = @"{
            ""x402Version"": 1,
            ""error"": """",
            ""accepts"": [{
                ""scheme"": ""exact"",
                ""network"": ""sepolia"",
                ""maxAmountRequired"": ""10000"",
                ""asset"": ""0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238"",
                ""payTo"": ""0x209693Bc6afc0C5328bA36FaF03C514EF312287C"",
                ""resource"": ""https://resource-server.com/data"",
                ""description"": ""Access to premium content"",
                ""maxTimeoutSeconds"": 300
            }]
        }";

        // Act
        var response = JsonSerializer.Deserialize<PaymentRequirementsResponse>(json, JsonOptions);

        // Assert - All fields deserialized correctly
        Assert.NotNull(response);
        Assert.Equal(1, response!.X402Version);
        Assert.NotNull(response.Error);
        Assert.NotNull(response.Accepts);
        Assert.Single(response.Accepts);
        Assert.Equal("exact", response.Accepts[0].Scheme);
        Assert.Equal("sepolia", response.Accepts[0].Network);
        Assert.Equal("10000", response.Accepts[0].MaxAmountRequired);
    }

    /// <summary>
    /// Spec: Section 5.1 - Accepts is an array of PaymentRequirements
    /// Use Case: UC-M1 Scenario 1
    /// Requirement: Accepts array structure preserved
    /// </summary>
    [Fact]
    public void Given_PaymentRequirementsResponse_When_Serialized_Then_AcceptsIsArrayOfObjects()
    {
        // Arrange
        var response = new PaymentRequirementsResponse
        {
            X402Version = 1,
            Error = "",
            Accepts = new List<PaymentRequirements>
            {
                new PaymentRequirements
                {
                    Scheme = "exact",
                    Network = "sepolia",
                    MaxAmountRequired = "10000",
                    Asset = "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238",
                    PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                    Resource = "https://resource-server.com/data",
                    Description = "Access to premium content",
                    MaxTimeoutSeconds = 300
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var jsonNode = JsonNode.Parse(json);

        // Assert - Accepts is an array
        Assert.Equal(JsonValueKind.Array, jsonNode!["accepts"]!.GetValueKind());

        // Assert - Array contains objects
        var acceptsArray = jsonNode["accepts"]!.AsArray();
        Assert.NotEmpty(acceptsArray);
        Assert.Equal(JsonValueKind.Object, acceptsArray[0]!.GetValueKind());
    }

    /// <summary>
    /// Spec: All sections - Round-trip serialization must preserve data
    /// Use Case: UC-M8 - JSON Serialization/Deserialization
    /// Requirement: Native AOT compatibility - no data loss
    /// </summary>
    [Fact]
    public void Given_PaymentRequirementsResponse_When_RoundTripSerialization_Then_AllDataIsPreserved()
    {
        // Arrange
        var original = new PaymentRequirementsResponse
        {
            X402Version = 1,
            Error = "",
            Accepts = new List<PaymentRequirements>
            {
                new PaymentRequirements
                {
                    Scheme = "exact",
                    Network = "sepolia",
                    MaxAmountRequired = "10000",
                    Asset = "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238",
                    PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                    Resource = "https://resource-server.com/data",
                    Description = "Access to premium content",
                    MaxTimeoutSeconds = 300
                }
            }
        };

        // Act - Serialize and deserialize
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PaymentRequirementsResponse>(json, JsonOptions);

        // Assert - All values preserved
        Assert.NotNull(deserialized);
        Assert.Equal(original.X402Version, deserialized!.X402Version);
        Assert.Equal(original.Error, deserialized.Error);
        Assert.NotNull(deserialized.Accepts);
        Assert.Single(deserialized.Accepts);
        Assert.Equal(original.Accepts[0].Scheme, deserialized.Accepts[0].Scheme);
        Assert.Equal(original.Accepts[0].Network, deserialized.Accepts[0].Network);
        Assert.Equal(original.Accepts[0].MaxAmountRequired, deserialized.Accepts[0].MaxAmountRequired);
        Assert.Equal(original.Accepts[0].Asset, deserialized.Accepts[0].Asset);
        Assert.Equal(original.Accepts[0].PayTo, deserialized.Accepts[0].PayTo);
        Assert.Equal(original.Accepts[0].Resource, deserialized.Accepts[0].Resource);
        Assert.Equal(original.Accepts[0].Description, deserialized.Accepts[0].Description);
        Assert.Equal(original.Accepts[0].MaxTimeoutSeconds, deserialized.Accepts[0].MaxTimeoutSeconds);
    }
}
