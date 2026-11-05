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
using Xunit;

namespace Nethereum.X402.IntegrationTests.AspNetCore;

public class X402MiddlewareSettlementTokenTests
{
    private const string Network = "eip155:84532";
    private const string Payee = "0x1111111111111111111111111111111111111111";
    private const string Token = "0x2222222222222222222222222222222222222222";

    private sealed class TokenCapturingFacilitator : IFacilitatorClient
    {
        public CancellationToken VerifyToken { get; private set; }
        public CancellationToken SettleToken { get; private set; }
        public bool Settled { get; private set; }

        public Task<VerificationResponse> VerifyAsync(PaymentPayload p, PaymentRequirements r, CancellationToken ct = default)
        {
            VerifyToken = ct;
            return Task.FromResult(new VerificationResponse { IsValid = true, Payer = "0xpayer" });
        }

        public Task<SettlementResponse> SettleAsync(PaymentPayload p, PaymentRequirements r, CancellationToken ct = default)
        {
            SettleToken = ct;
            Settled = true;
            return Task.FromResult(new SettlementResponse { Success = true, Transaction = "0xdeadbeef", Network = r.Network, Payer = "0xpayer" });
        }

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken ct = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse { Kinds = new List<PaymentKind>() });
    }

    [Fact]
    [Trait("Rule", "S5")]
    public async Task Given_AnAuthorizationFlowRoute_When_ThePaymentSettles_Then_TheFacilitatorSettlesWithAnUncancellableTokenAndVerifiesWithTheRequestToken()
    {
        var facilitator = new TokenCapturingFacilitator();
        using var server = CreateServer(facilitator, upfront: false);
        using var abort = new CancellationTokenSource();

        var response = await Send(server, abort.Token, false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(facilitator.Settled);
        Assert.False(facilitator.SettleToken.CanBeCanceled);
        Assert.True(facilitator.VerifyToken.CanBeCanceled);
    }

    [Fact]
    [Trait("Rule", "S5")]
    public async Task Given_AnUpfrontFlowRoute_When_ThePaymentSettles_Then_TheFacilitatorSettlesWithAnUncancellableToken()
    {
        var facilitator = new TokenCapturingFacilitator();
        using var server = CreateServer(facilitator, upfront: true);
        using var abort = new CancellationTokenSource();

        var response = await Send(server, abort.Token, true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(facilitator.Settled);
        Assert.False(facilitator.SettleToken.CanBeCanceled);
    }

    private static Task<HttpResponseMessage> Send(TestServer server, CancellationToken cancellationToken, bool upfront)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/premium");
        request.Headers.Add("PAYMENT-SIGNATURE", BuildHeader(upfront));
        return server.CreateClient().SendAsync(request, cancellationToken);
    }

    private static string BuildHeader(bool upfront)
    {
        var payload = new PaymentPayload
        {
            X402Version = 2,
            Accepted = RouteRequirements(upfront),
            Payload = new ExactSchemePayload
            {
                Signature = "0x" + new string('0', 130),
                Authorization = new Nethereum.X402.Models.Authorization
                {
                    From = "0x3333333333333333333333333333333333333333",
                    To = Payee,
                    Value = "100000",
                    ValidAfter = "0",
                    ValidBefore = "99999999999",
                    Nonce = "0x" + new string('a', 64)
                }
            }
        };
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
    }

    private static PaymentRequirements RouteRequirements(bool upfront) => new()
    {
        Scheme = "exact",
        Network = Network,
        Amount = "100000",
        PayTo = Payee,
        Asset = Token,
        MaxTimeoutSeconds = 300,
        Extra = new ExactSchemeExtra
        {
            Name = "USDC",
            Version = "2",
            PaymentFlow = upfront ? X402PaymentFlows.Upfront : null
        }
    };

    private static TestServer CreateServer(IFacilitatorClient facilitator, bool upfront)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton(facilitator))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "http://facilitator.local";
                    options.Routes.Add(new RoutePaymentConfig
                    {
                        PathPattern = "/premium",
                        Requirements = RouteRequirements(upfront)
                    });
                });
                app.Run(context => context.Response.WriteAsync("premium"));
            });
        return new TestServer(builder);
    }
}
