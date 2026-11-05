using Nethereum.X402.AspNetCore;
using Nethereum.X402.Models;
using Nethereum.X402.Server;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Server;

public class X402OptionsValidationRound4Tests
{
    private static PaymentRequirements Requirements() => new()
    {
        Scheme = X402Schemes.Exact,
        Network = "eip155:84532",
        Amount = "100000",
        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
        Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
        MaxTimeoutSeconds = 300,
        Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
    };

    private static X402Options OptionsFor(string pattern)
    {
        var options = new X402Options { FacilitatorUrl = "http://facilitator.local" };
        options.Routes.Add(new RoutePaymentConfig(pattern, Requirements()));
        return options;
    }

    [Theory]
    [Trait("Rule", "X3")]
    [InlineData("premium")]
    [InlineData("api/premium")]
    [InlineData("https://host/premium")]
    [InlineData("/premium?x=1")]
    [InlineData("/premium#frag")]
    public void Given_APathPatternThatCannotMatchARequestPath_When_Validating_Then_ArgumentExceptionNamesThePattern(string pattern)
    {
        var exception = Assert.Throws<ArgumentException>(() => OptionsFor(pattern).Validate());

        Assert.Contains(pattern, exception.Message);
    }

    [Theory]
    [Trait("Rule", "X3")]
    [InlineData("/premium")]
    [InlineData("/api/*")]
    [InlineData("/*")]
    public void Given_APathPatternStartingWithASlash_When_Validating_Then_ItIsAccepted(string pattern)
    {
        OptionsFor(pattern).Validate();
    }

    [Theory]
    [Trait("Rule", "X3")]
    [InlineData(0)]
    [InlineData(-5)]
    public void Given_ANonPositiveReplayRetentionWithReplayProtectionOn_When_Validating_Then_ArgumentException(int seconds)
    {
        var options = OptionsFor("/premium");
        options.PaymentReplayRetention = TimeSpan.FromSeconds(seconds);

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    [Trait("Rule", "X3")]
    public void Given_ANonPositiveReplayRetentionWithReplayProtectionOff_When_Validating_Then_ItIsAccepted()
    {
        var options = OptionsFor("/premium");
        options.EnablePaymentReplayProtection = false;
        options.PaymentReplayRetention = TimeSpan.Zero;

        options.Validate();
    }
}
