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

public class X402Round5Tests
{
    private const string Network = "eip155:84532";
    private const string Token = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const string Payee = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";
    private const string Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66";

    private sealed class SettlingFacilitator : IFacilitatorClient
    {
        public Task<VerificationResponse> VerifyAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default) =>
            Task.FromResult(new VerificationResponse { IsValid = true, Payer = Payer });

        public Task<SettlementResponse> SettleAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default) =>
            Task.FromResult(new SettlementResponse { Success = true, Transaction = "0xabc", Network = Network, Payer = Payer });

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken c = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse());
    }

    private static PaymentRequirements Requirements() => new()
    {
        Scheme = X402Schemes.Exact, Network = Network, Amount = "100000", PayTo = Payee, Asset = Token, MaxTimeoutSeconds = 300,
        Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
    };

    private static HttpRequestMessage Paid(int nonce, string path)
    {
        var payload = new PaymentPayload
        {
            X402Version = 2,
            Accepted = Requirements(),
            Payload = new ExactSchemePayload
            {
                Signature = "0x" + new string('1', 130),
                Authorization = new Nethereum.X402.Models.Authorization
                {
                    From = Payer, To = Payee, Value = "100000", ValidAfter = "0", ValidBefore = "4102444800",
                    Nonce = "0x" + nonce.ToString("x").PadLeft(64, '0')
                }
            }
        };
        var message = new HttpRequestMessage(HttpMethod.Get, path);
        message.Headers.Add(X402Headers.PaymentSignature, Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
        return message;
    }

    private static TestServer Server(string pattern, Action<X402Options> configure) =>
        new(new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton<IFacilitatorClient>(new SettlingFacilitator()))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "http://f.local";
                    options.Routes.Add(new RoutePaymentConfig(pattern, Requirements()));
                    configure(options);
                });
                app.Run(context => context.Response.WriteAsync("ok"));
            }));

    [Fact(Timeout = 60000)]
    [Trait("Rule", "Y2")]
    public async Task Given_AReplayRetentionOfTimeSpanMaxValue_When_APaymentIsReplayed_Then_ThePaidRequestSucceedsAndTheReplayIsRejected()
    {
        using var server = Server("/api/*", options => options.PaymentReplayRetention = TimeSpan.MaxValue);
        var client = server.CreateClient();

        var first = await client.SendAsync(Paid(1, "/api/x"));
        var second = await client.SendAsync(Paid(1, "/api/x"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, second.StatusCode);
        Assert.Contains(X402ErrorCodes.NonceAlreadyUsed, await second.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Rule", "Y3")]
    public void Given_TheBareStarPattern_When_Validating_Then_ItIsAccepted()
    {
        var options = new X402Options { FacilitatorUrl = "http://f.local" };
        options.Routes.Add(new RoutePaymentConfig("*", Requirements()));

        options.Validate();
    }

    [Fact]
    [Trait("Rule", "Y3")]
    public void Given_APatternCarryingAVerb_When_Validating_Then_ItIsStillRejected()
    {
        var options = new X402Options { FacilitatorUrl = "http://f.local" };
        options.Routes.Add(new RoutePaymentConfig("GET /api/*", Requirements()));

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Theory(Timeout = 60000)]
    [Trait("Rule", "Y3")]
    [InlineData("/")]
    [InlineData("/anything")]
    [InlineData("/deep/er/path")]
    public async Task Given_TheBareStarPattern_When_AnyPathIsRequestedWithoutPayment_Then_ItIsCharged(string path)
    {
        using var server = Server("*", _ => { });

        var response = await server.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
    }
}
