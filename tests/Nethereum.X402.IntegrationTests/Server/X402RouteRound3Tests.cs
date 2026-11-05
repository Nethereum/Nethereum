using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.AspNetCore;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.Server;

public class X402RouteRound3Tests
{
    [Theory]
    [Trait("Rule", "W2")]
    [InlineData("/files/secret/data.txt")]
    [InlineData("/files/secret%5Cdata.txt")]
    [InlineData("/files/SECRET%5Cdata.txt")]
    [InlineData("/files/secret%5C%5Cdata.txt")]
    [InlineData("/files/x/..%5Csecret/data.txt")]
    [InlineData("/files/secret./data.txt")]
    [InlineData("/files/secret%2Fdata.txt")]
    public async Task Given_AStaticFileHostThatResolvesAlternativeSpellings_When_AProtectedFileIsRequestedByThem_Then_ThePaymentIsDemanded(string path)
    {
        Assert.Equal(HttpStatusCode.PaymentRequired, await StaticFilesStatusAsync(path));
    }

    [Theory]
    [Trait("Rule", "W2")]
    [InlineData("/files/public/a.txt")]
    [InlineData("/files/public%5Ca.txt")]
    [InlineData("/files/secretive/data.txt")]
    public async Task Given_AStaticFileHostThatResolvesAlternativeSpellings_When_AnUnprotectedNeighbourIsRequested_Then_ItIsServedWithoutPayment(string path)
    {
        var status = await StaticFilesStatusAsync(path);

        Assert.NotEqual(HttpStatusCode.PaymentRequired, status);
    }

    [Theory]
    [Trait("Rule", "W2")]
    [InlineData("/files/secret%2Fdata")]
    [InlineData("/files/secret%2fdata")]
    [InlineData("/FILES/SECRET%2Fdata")]
    public async Task Given_ACatchAllEndpointThatDecodesEncodedSlashes_When_AProtectedPathIsRequestedWithAnEncodedSlash_Then_ThePaymentIsDemanded(string path)
    {
        var port = ReferenceProcess.FreePort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var host = new WebHostBuilder().UseKestrel().UseUrls(baseUrl)
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSingleton<IFacilitatorClient>(new MockFacilitatorClient());
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://facilitator.test";
                    options.Routes.Add(Route("/files/secret/*", "1000"));
                });
                app.UseEndpoints(endpoints => endpoints.MapGet("/files/{**path}", (string path) => "HANDLER " + path));
            }).Build();
        host.Start();
        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync(new Uri(baseUrl + path));

            Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Theory]
    [Trait("Rule", "W4")]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_ALiteralRouteAndAParameterRoute_When_TheLiteralPathIsRequested_Then_TheLiteralRouteChargesWhateverTheDeclarationOrder(bool parameterFirst)
    {
        var literal = Route("/api/vip", "100");
        var parameter = Route("/api/:identifier", "1");
        var routes = parameterFirst ? new[] { parameter, literal } : new[] { literal, parameter };
        var processor = new X402FacilitatorProxyProcessor(new MockFacilitatorClient(), routes);

        Assert.Equal("100", processor.FindMatchingRoute("/api/vip", "GET")!.Requirements.Amount);
        Assert.Equal("1", processor.FindMatchingRoute("/api/other", "GET")!.Requirements.Amount);
    }

    [Theory]
    [Trait("Rule", "W4")]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_AParameterRouteAndATrailingWildcardRoute_When_AChildIsRequested_Then_TheParameterRouteChargesBecauseItOutranksTheCatchAll(bool wildcardFirst)
    {
        var wildcard = Route("/premium/*", "500");
        var parameter = Route("/premium/:id", "1");
        var routes = wildcardFirst ? new[] { wildcard, parameter } : new[] { parameter, wildcard };
        var processor = new X402FacilitatorProxyProcessor(new MockFacilitatorClient(), routes);

        Assert.Equal("1", processor.FindMatchingRoute("/premium/x", "GET")!.Requirements.Amount);
        Assert.Equal("500", processor.FindMatchingRoute("/premium/x/y", "GET")!.Requirements.Amount);
        Assert.Equal("500", processor.FindMatchingRoute("/premium", "GET")!.Requirements.Amount);
    }

    [Fact]
    [Trait("Rule", "W4")]
    public void Given_TwoRoutesOfEqualSpecificity_When_TheyBothMatch_Then_TheFirstDeclaredCharges()
    {
        var processor = new X402FacilitatorProxyProcessor(
            new MockFacilitatorClient(), new[] { Route("/items/:a", "7"), Route("/items/:b", "8") });

        Assert.Equal("7", processor.FindMatchingRoute("/items/1", "GET")!.Requirements.Amount);
    }

    [Fact]
    [Trait("Rule", "W4")]
    public void Given_ALongerParameterPatternAndAShorterLiteralPattern_When_TheLiteralPathIsRequested_Then_SpecificityBeatsStringLength()
    {
        var processor = new X402FacilitatorProxyProcessor(
            new MockFacilitatorClient(), new[] { Route("/reports/:identifier/summary", "1"), Route("/reports/q3/summary", "9") });

        Assert.Equal("9", processor.FindMatchingRoute("/reports/q3/summary", "GET")!.Requirements.Amount);
    }

    [Fact]
    [Trait("Rule", "W7")]
    public async Task Given_AMultiWildcardPattern_When_ALongPathThatCannotMatchIsRequested_Then_NoRouteMatchesWithinOneSecond()
    {
        var processor = new X402FacilitatorProxyProcessor(
            new MockFacilitatorClient(), new[] { Route("/files/*/v/*/v/*/v/*/end", "1") });
        var path = "/files/" + string.Concat(Enumerable.Repeat("v/", 2000)) + "nope";
        var stopwatch = Stopwatch.StartNew();

        var match = Task.Run(() => processor.FindMatchingRoute(path, "GET"));
        var finished = await Task.WhenAny(match, Task.Delay(TimeSpan.FromSeconds(1))) == match;

        Assert.True(finished, "the route match did not finish within a second");
        Assert.Null(await match);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    [Trait("Rule", "W7")]
    public void Given_AMultiWildcardPattern_When_APathThatMatchesIsRequested_Then_TheRouteMatchesCaseInsensitively()
    {
        var processor = new X402FacilitatorProxyProcessor(
            new MockFacilitatorClient(), new[] { Route("/files/*/v/*/end", "1") });

        Assert.NotNull(processor.FindMatchingRoute("/FILES/a/b/V/c/END", "GET"));
        Assert.Null(processor.FindMatchingRoute("/files/a/b/v/c/end/more", "GET"));
    }

    private static async Task<HttpStatusCode> StaticFilesStatusAsync(string path)
    {
        var root = Path.Combine(Path.GetTempPath(), "x402-w2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "secret"));
        Directory.CreateDirectory(Path.Combine(root, "public"));
        Directory.CreateDirectory(Path.Combine(root, "secretive"));
        File.WriteAllText(Path.Combine(root, "secret", "data.txt"), "PAID");
        File.WriteAllText(Path.Combine(root, "public", "a.txt"), "FREE");
        File.WriteAllText(Path.Combine(root, "secretive", "data.txt"), "FREE");
        var port = ReferenceProcess.FreePort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var host = new WebHostBuilder().UseKestrel().UseUrls(baseUrl)
            .ConfigureServices(services => services.AddSingleton<IFacilitatorClient>(new MockFacilitatorClient()))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://facilitator.test";
                    options.Routes.Add(Route("/files/secret/*", "1000"));
                });
                app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(root), RequestPath = "/files" });
                app.Run(context =>
                {
                    context.Response.StatusCode = 404;
                    return Task.CompletedTask;
                });
            }).Build();
        host.Start();
        try
        {
            using var client = new HttpClient();
            return (await client.GetAsync(new Uri(baseUrl + path))).StatusCode;
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    private static RoutePaymentConfig Route(string pattern, string amount) => new(pattern, new PaymentRequirements
    {
        Scheme = "exact",
        Network = "eip155:84532",
        Amount = amount,
        Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
        MaxTimeoutSeconds = 60,
        Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
    });
}
