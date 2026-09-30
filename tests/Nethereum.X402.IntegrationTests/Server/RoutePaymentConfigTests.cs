using Nethereum.Documentation;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.Server;

public class RoutePaymentConfigTests
{
    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "RoutePaymentConfig: configure payment-protected endpoints", Order = 30)]
    public void Given_PaymentRequirements_When_CreatingRouteConfig_Then_ConfigurationIsValid()
    {
        var requirements = CreateTestPaymentRequirements();

        var config = new RoutePaymentConfig("/api/premium", requirements);

        Assert.Equal("/api/premium", config.PathPattern);
        Assert.Same(requirements, config.Requirements);
        Assert.Null(config.Method);
    }

    [Fact]
    public void Given_MethodSpecified_When_CreatingRouteConfig_Then_MethodIsStored()
    {
        var requirements = CreateTestPaymentRequirements();

        var config = new RoutePaymentConfig("/api/data", requirements, "GET");

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
            Amount = "10000",
            PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            MaxTimeoutSeconds = 60,
            Asset = "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238"
        };
    }
}
