using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.AspNetCore;

public class X402PaidResponseCacheControlTests
{
    private const string Network = "eip155:84532";
    private const string Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66";
    private const string Payee = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";
    private const string Token = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";

    [Theory]
    [Trait("Rule", "V12")]
    [InlineData(X402PaymentFlows.Authorization)]
    [InlineData(X402PaymentFlows.Upfront)]
    public async Task Given_AHandlerThatMarksTheResponsePublicWithASharedCacheLifetime_When_ThePaymentSettles_Then_PublicAndSMaxageAreRemovedAndPrivateIsPresent(string flow)
    {
        var response = await PayAsync(flow, "public, max-age=60, s-maxage=600");

        var directives = Directives(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("public", directives);
        Assert.DoesNotContain(directives, directive => directive.StartsWith("s-maxage", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("private", directives);
        Assert.Contains("max-age=60", directives);
    }

    [Theory]
    [Trait("Rule", "V12")]
    [InlineData("PUBLIC, S-MaxAge=5, no-transform", "no-transform")]
    [InlineData("s-maxage=30", null)]
    [InlineData("private, max-age=10", "max-age=10")]
    public async Task Given_ACacheControlWithMixedCaseOrPrivateAlready_When_ThePaymentSettles_Then_OnlyPublicAndSMaxageAreDroppedAndPrivateAppearsOnce(string handlerValue, string? kept)
    {
        var response = await PayAsync(X402PaymentFlows.Authorization, handlerValue);

        var directives = Directives(response);
        Assert.Single(directives, directive => directive.Equals("private", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(directives, directive =>
            directive.Equals("public", StringComparison.OrdinalIgnoreCase) ||
            directive.StartsWith("s-maxage", StringComparison.OrdinalIgnoreCase));
        if (kept != null)
            Assert.Contains(kept, directives);
    }

    [Fact]
    [Trait("Rule", "V12")]
    public async Task Given_AHandlerWithoutCacheControl_When_ThePaymentSettles_Then_TheResponseIsPrivate()
    {
        var response = await PayAsync(X402PaymentFlows.Authorization, null);

        Assert.Equal(new[] { "private" }, Directives(response));
    }

    [Theory]
    [Trait("Rule", "Z2")]
    [InlineData("public, max-age=600, private=\"X-Foo\"")]
    [InlineData("max-age=600, private=\"Set-Cookie\"")]
    public async Task Given_AQualifiedPrivateDirective_When_ThePaymentSettles_Then_ItIsReplacedByABarePrivateAndOtherDirectivesAreKept(string handlerValue)
    {
        var response = await PayAsync(X402PaymentFlows.Authorization, handlerValue);

        Assert.Equal(new[] { "max-age=600", "private" }, Directives(response));
    }

    private static string[] Directives(HttpResponseMessage response) =>
        response.Headers.GetValues("Cache-Control")
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToArray();

    private static async Task<HttpResponseMessage> PayAsync(string flow, string? cacheControl)
    {
        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = Network,
            Amount = "100000",
            Asset = Token,
            PayTo = Payee,
            MaxTimeoutSeconds = 300,
            Extra = new ExactSchemeExtra { Name = "USDC", Version = "2", PaymentFlow = flow }
        };
        var facilitator = new MockFacilitatorClient
        {
            VerifyResponse = new VerificationResponse { IsValid = true, Payer = Payer },
            SettleResponse = new SettlementResponse { Success = true, Transaction = "0x01", Network = Network, Payer = Payer }
        };

        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton<IFacilitatorClient>(facilitator))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://facilitator.test";
                    options.Routes.Add(new RoutePaymentConfig("/premium", requirements));
                });
                app.Run(context =>
                {
                    if (cacheControl != null)
                        context.Response.Headers["Cache-Control"] = cacheControl;
                    return context.Response.WriteAsync("premium");
                });
            }));

        var payment = new PaymentPayload
        {
            X402Version = 2,
            Accepted = requirements,
            Payload = new ExactSchemePayload
            {
                Signature = "0x00",
                Authorization = new Nethereum.X402.Models.Authorization
                {
                    From = Payer,
                    To = Payee,
                    Value = "100000",
                    ValidAfter = "0",
                    ValidBefore = "99999999999",
                    Nonce = "0x" + new string('a', 64)
                }
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Get, "/premium");
        request.Headers.Add(X402Headers.PaymentSignature, Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payment))));
        return await server.CreateClient().SendAsync(request);
    }
}
