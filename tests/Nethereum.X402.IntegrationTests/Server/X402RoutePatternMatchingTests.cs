using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.AspNetCore;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.Server;

public class X402RoutePatternMatchingTests
{
    [Theory]
    [Trait("Rule", "S10")]
    [InlineData("/api/data", "/api/data")]
    [InlineData("/api/data", "/api/data/")]
    [InlineData("/api/data/*", "/api/data/x")]
    [InlineData("/api/data/*", "/api/data/x/y")]
    [InlineData("/api/data/", "/api/data/")]
    [InlineData("/api/data", "/api/data//")]
    [InlineData("/api/data/", "/api/data")]
    public void Given_APattern_When_ThePathIsTheSameWithOrWithoutOneTrailingSlash_Then_TheRouteMatches(string pattern, string path)
    {
        var processor = ProcessorFor(pattern);

        Assert.NotNull(processor.FindMatchingRoute(path));
    }

    [Theory]
    [Trait("Rule", "S10")]
    [InlineData("/api/data", "/api/data/x")]
    [InlineData("/api/data", "/api/datax")]
    public void Given_APattern_When_ThePathIsAnotherResource_Then_TheRouteDoesNotMatch(string pattern, string path)
    {
        var processor = ProcessorFor(pattern);

        Assert.Null(processor.FindMatchingRoute(path));
    }

    private static X402FacilitatorProxyProcessor ProcessorFor(string pattern) =>
        new(new MockFacilitatorClient(), new[]
        {
            new RoutePaymentConfig(pattern, new PaymentRequirements { Scheme = "exact", Network = "base-sepolia", Amount = "1" })
        });
}
