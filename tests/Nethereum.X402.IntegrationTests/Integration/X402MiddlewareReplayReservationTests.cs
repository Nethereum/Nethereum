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

namespace Nethereum.X402.IntegrationTests.Integration;

public class X402MiddlewareReplayReservationTests
{
    private const string Network = "eip155:84532";
    private const string Token = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const string Payee = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";
    private const string Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66";

    private sealed class ScriptedFacilitator : IFacilitatorClient
    {
        public Func<SettlementResponse> Settle { get; set; } = Succeeded;
        public int Settles;

        public static SettlementResponse Failed(string reason, string? transaction) =>
            new() { Success = false, ErrorReason = reason, Transaction = transaction ?? string.Empty, Network = Network, Payer = Payer };

        public static SettlementResponse Succeeded() =>
            new() { Success = true, Transaction = "0xabc", Network = Network, Payer = Payer };

        public Task<VerificationResponse> VerifyAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default) =>
            Task.FromResult(new VerificationResponse { IsValid = true, Payer = Payer });

        public Task<SettlementResponse> SettleAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default)
        {
            Interlocked.Increment(ref Settles);
            return Task.FromResult(Settle());
        }

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken c = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse());
    }

    private static PaymentRequirements Requirements() => new()
    {
        Scheme = X402Schemes.Exact,
        Network = Network,
        Amount = "100000",
        PayTo = Payee,
        Asset = Token,
        MaxTimeoutSeconds = 300,
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
                    From = Payer,
                    To = Payee,
                    Value = "100000",
                    ValidAfter = "0",
                    ValidBefore = "4102444800",
                    Nonce = "0x" + nonce.ToString("x").PadLeft(64, '0')
                }
            }
        };
        var message = new HttpRequestMessage(HttpMethod.Get, path);
        message.Headers.Add(X402Headers.PaymentSignature, Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
        return message;
    }

    private static TestServer Server(ScriptedFacilitator facilitator, int maxEntries, RequestDelegate endpoint) =>
        new(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IFacilitatorClient>(facilitator);
                services.AddSingleton<IPaymentReplayStore>(new InMemoryPaymentReplayStore(maxEntries));
            })
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "http://facilitator.local";
                    options.Routes.Add(new RoutePaymentConfig("/api/*", Requirements()));
                });
                app.Run(endpoint);
            }));

    private static Task Ok(HttpContext context) => context.Response.WriteAsync("ok");

    [Fact(Timeout = 60000)]
    [Trait("Rule", "X1")]
    public async Task Given_PaymentsWhoseHandlerReturns404FillingTheStore_When_ALegitimatePaymentArrives_Then_ItIsServedAndSettled()
    {
        var facilitator = new ScriptedFacilitator();
        using var server = Server(facilitator, 3, context =>
        {
            context.Response.StatusCode = context.Request.Path.Value!.StartsWith("/api/missing") ? 404 : 200;
            return Task.CompletedTask;
        });
        var client = server.CreateClient();

        for (var nonce = 1; nonce <= 3; nonce++)
            Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Paid(nonce, "/api/missing" + nonce))).StatusCode);

        var legitimate = await client.SendAsync(Paid(99, "/api/real"));

        Assert.Equal(HttpStatusCode.OK, legitimate.StatusCode);
        Assert.Equal(1, facilitator.Settles);
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "X1")]
    public async Task Given_APaymentWhoseHandlerReturned404_When_ThePaymentIsPresentedToARealUrl_Then_ItIsServedAndSettled()
    {
        var facilitator = new ScriptedFacilitator();
        using var server = Server(facilitator, 100, context =>
        {
            context.Response.StatusCode = context.Request.Path.Value == "/api/missing" ? 404 : 200;
            return Task.CompletedTask;
        });
        var client = server.CreateClient();

        var missing = await client.SendAsync(Paid(1, "/api/missing"));
        var retried = await client.SendAsync(Paid(1, "/api/real"));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal(1, facilitator.Settles);
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "X1")]
    public async Task Given_ASettlementThatFailed_When_TheSamePaymentIsRetriedAfterTheFacilitatorRecovers_Then_ItSettlesOnce()
    {
        var facilitator = new ScriptedFacilitator
        {
            Settle = () => ScriptedFacilitator.Failed(X402ErrorCodes.InsufficientFunds, null)
        };
        using var server = Server(facilitator, 100, Ok);
        var client = server.CreateClient();

        var failed = await client.SendAsync(Paid(1, "/api/real"));
        facilitator.Settle = ScriptedFacilitator.Succeeded;
        var retried = await client.SendAsync(Paid(1, "/api/real"));

        Assert.Equal(HttpStatusCode.PaymentRequired, failed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal(2, facilitator.Settles);
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "X1")]
    public async Task Given_ASettlementThatIsPending_When_TheSamePaymentIsPresentedAgain_Then_ItIsRejectedAsAReplay()
    {
        var facilitator = new ScriptedFacilitator
        {
            Settle = () => ScriptedFacilitator.Failed(X402ErrorCodes.SettlementPending, "0xpending")
        };
        using var server = Server(facilitator, 100, Ok);
        var client = server.CreateClient();

        var pending = await client.SendAsync(Paid(1, "/api/real"));
        var again = await client.SendAsync(Paid(1, "/api/real"));

        Assert.Equal(HttpStatusCode.PaymentRequired, pending.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, again.StatusCode);
        Assert.Contains(X402ErrorCodes.NonceAlreadyUsed, await again.Content.ReadAsStringAsync());
        Assert.Equal(1, facilitator.Settles);
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "X1")]
    public async Task Given_AHandlerThatThrows_When_TheSamePaymentIsRetried_Then_TheReservationWasReleased()
    {
        var facilitator = new ScriptedFacilitator();
        var failNext = true;
        using var server = Server(facilitator, 100, context =>
        {
            if (failNext)
            {
                failNext = false;
                throw new InvalidOperationException("endpoint failed");
            }

            return Ok(context);
        });
        var client = server.CreateClient();

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(Paid(1, "/api/real")));
        var retried = await client.SendAsync(Paid(1, "/api/real"));

        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal(1, facilitator.Settles);
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "X1")]
    public async Task Given_ASettledPayment_When_ItIsPresentedAgain_Then_ItIsRejectedAsAReplay()
    {
        var facilitator = new ScriptedFacilitator();
        using var server = Server(facilitator, 100, Ok);
        var client = server.CreateClient();

        var first = await client.SendAsync(Paid(1, "/api/real"));
        var again = await client.SendAsync(Paid(1, "/api/real"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, again.StatusCode);
        Assert.Equal(1, facilitator.Settles);
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "X1")]
    public async Task Given_AFullStoreOfExpiredReservations_When_TwoReservationsArriveWithinASecond_Then_TheSecondDoesNotSweepAgain()
    {
        var store = new InMemoryPaymentReplayStore(1);
        var retention = TimeSpan.FromMilliseconds(1);

        await store.TryReserveAsync("a", retention);
        await Task.Delay(20);
        Assert.True(await store.TryReserveAsync("b", retention));
        await Task.Delay(20);

        await Assert.ThrowsAsync<PaymentReplayStoreFullException>(() => store.TryReserveAsync("c", retention));

        await Task.Delay(1100);
        Assert.True(await store.TryReserveAsync("c", retention));
    }
}
