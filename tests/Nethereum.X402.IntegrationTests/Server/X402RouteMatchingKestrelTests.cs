using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.AspNetCore;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.Server;

public class X402RouteMatchingKestrelTests
{
    private const string Open = "open";

    [Theory]
    [Trait("Rule", "V1")]
    [InlineData("/api/premium")]
    [InlineData("/api/premium/")]
    [InlineData("/api/premium/x")]
    [InlineData("/api/premium/a/b")]
    [InlineData("/api/premium//x")]
    [InlineData("//api//premium")]
    public async Task Given_ATrailingWildcardPattern_When_TheBarePrefixOrADescendantIsRequested_Then_ItIsProtected(string path)
    {
        Assert.Equal(HttpStatusCode.PaymentRequired, await StatusAsync("/api/premium/*", path));
    }

    [Theory]
    [Trait("Rule", "V1")]
    [InlineData("/api/premiumx")]
    [InlineData("/api/premiu")]
    [InlineData("/other")]
    public async Task Given_ATrailingWildcardPattern_When_ASiblingPathIsRequested_Then_ItIsNotProtected(string path)
    {
        Assert.Equal(HttpStatusCode.OK, await StatusAsync("/api/premium/*", path));
    }

    [Theory]
    [Trait("Rule", "V1")]
    [InlineData("/reports")]
    [InlineData("/reports/")]
    [InlineData("//reports//")]
    public async Task Given_APatternWithATrailingSlash_When_ThePathIsRequestedWithOrWithoutIt_Then_ItIsProtected(string path)
    {
        Assert.Equal(HttpStatusCode.PaymentRequired, await StatusAsync("/reports/", path));
    }

    [Theory]
    [Trait("Rule", "V1")]
    [InlineData("/caf%C3%A9%20menu")]
    [InlineData("/caf%C3%A9%20menu/")]
    [InlineData("/CAF%C3%89%20MENU")]
    public async Task Given_ANonAsciiPatternWithASpace_When_ItsPercentEncodedFormIsRequested_Then_TheDecodedPathIsProtected(string path)
    {
        Assert.Equal(HttpStatusCode.PaymentRequired, await StatusAsync("/café menu", path));
    }

    [Theory]
    [Trait("Rule", "V1")]
    [InlineData("/api/42")]
    [InlineData("/api/some-id")]
    public async Task Given_ABracketOrColonParameterPattern_When_OneSegmentIsRequested_Then_ItIsProtected(string path)
    {
        Assert.Equal(HttpStatusCode.PaymentRequired, await StatusAsync("/api/[id]", path));
        Assert.Equal(HttpStatusCode.PaymentRequired, await StatusAsync("/api/:id", path));
    }

    [Fact]
    [Trait("Rule", "V1")]
    public async Task Given_ABracketOrColonParameterPattern_When_TwoSegmentsAreRequested_Then_ItIsNotProtected()
    {
        Assert.Equal(HttpStatusCode.OK, await StatusAsync("/api/[id]", "/api/42/extra"));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync("/api/:id", "/api/42/extra"));
    }

    [Fact]
    [Trait("Rule", "V1")]
    public async Task Given_AStarPattern_When_ADeepPathIsRequested_Then_TheStarSpansSegments()
    {
        Assert.Equal(HttpStatusCode.PaymentRequired, await StatusAsync("/api/*/data", "/api/a/b/c/data"));
    }

    [Theory]
    [Trait("Rule", "V8")]
    [InlineData("/ITEMS")]
    [InlineData("/Items")]
    [InlineData("/items")]
    public async Task Given_TheTurkishCulture_When_APatternIsMatchedCaseInsensitively_Then_TheDottedAndDotlessIAreNotConfused(string path)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            Assert.Equal(HttpStatusCode.PaymentRequired, await StatusAsync("/items", path));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [Trait("Rule", "V8")]
    [InlineData("/ITEMS")]
    [InlineData("/Items")]
    public void Given_TheTurkishCulture_When_TheProcessorMatchesAPath_Then_TheCaseFoldingIsInvariant(string path)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            var processor = new X402FacilitatorProxyProcessor(new MockFacilitatorClient(), new[] { Route("/items") });

            Assert.NotNull(processor.FindMatchingRoute(path));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static async Task<HttpStatusCode> StatusAsync(string pattern, string path)
    {
        var port = ReferenceProcess.FreePort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var host = new WebHostBuilder()
            .UseKestrel()
            .UseUrls(baseUrl)
            .ConfigureServices(services => services.AddSingleton<IFacilitatorClient>(new MockFacilitatorClient()))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://facilitator.test";
                    options.Routes.Add(Route(pattern));
                });
                app.Run(context => context.Response.WriteAsync(Open));
            })
            .Build();
        host.Start();

        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync(new Uri(baseUrl + path));
            return response.StatusCode;
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    private static RoutePaymentConfig Route(string pattern) => new(pattern, new PaymentRequirements
    {
        Scheme = "exact",
        Network = "eip155:84532",
        Amount = "1000",
        Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
        MaxTimeoutSeconds = 60,
        Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
    });
}
