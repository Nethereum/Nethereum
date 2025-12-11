using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.Server;

/// <summary>
/// BDD tests for RoutePaymentConfig
///
/// Traceability:
/// - Spec: Section 8 - Server Implementation
/// - Use Cases: UC-CORE-1
/// - Requirements: Route configuration with payment requirements
/// - Implementation: src/Nethereum.X402/Server/RoutePaymentConfig.cs
/// </summary>
public class RoutePaymentConfigTests
{
    /// <summary>
    /// Spec: Section 8 - Route configuration
    /// Use Case: UC-CORE-1 Scenario 1
    /// </summary>
    [Fact]
    public void Given_PaymentRequirements_When_CreatingRouteConfig_Then_ConfigurationIsValid()
    {
        // Arrange
        var requirements = CreateTestPaymentRequirements();

        // Act
        var config = new RoutePaymentConfig("/api/premium", requirements);

        // Assert
        Assert.Equal("/api/premium", config.PathPattern);
        Assert.Same(requirements, config.Requirements);
        Assert.Null(config.Method);
    }

    /// <summary>
    /// Spec: Section 8 - HTTP method filtering
    /// Use Case: UC-CORE-1 Scenario 2
    /// </summary>
    [Fact]
    public void Given_MethodSpecified_When_CreatingRouteConfig_Then_MethodIsStored()
    {
        // Arrange
        var requirements = CreateTestPaymentRequirements();

        // Act
        var config = new RoutePaymentConfig("/api/data", requirements, "GET");

        // Assert
        Assert.Equal("/api/data", config.PathPattern);
        Assert.Equal("GET", config.Method);
        Assert.Same(requirements, config.Requirements);
    }

    private PaymentRequirements CreateTestPaymentRequirements()
    {
        return new PaymentRequirements
        {
            Scheme = "exact",
            Network = "base-sepolia",
            MaxAmountRequired = "10000",
            Resource = "/api/premium",
            Description = "Premium API access",
            MimeType = "application/json",
            PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            MaxTimeoutSeconds = 60,
            Asset = "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238"
        };
    }
}
