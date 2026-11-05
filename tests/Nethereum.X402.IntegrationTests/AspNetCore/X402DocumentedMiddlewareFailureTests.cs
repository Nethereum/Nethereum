using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
using Authorization = Nethereum.X402.Models.Authorization;

namespace Nethereum.X402.IntegrationTests.AspNetCore;

public class X402DocumentedMiddlewareFailureTests
{
    private const string Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66";
    private const string Receiver = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";
    private const string Usdc = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";

    [Fact]
    [Trait("Rule", "D5")]
    public async Task Given_AnEndpointResponseLargerThanMaxBufferedResponseBytes_When_APaidRequestIsServed_Then_TheMiddlewareReturns500AndDoesNotSettle()
    {
        var facilitator = new MockFacilitatorClient();
        using var server = Server(facilitator, options => options.MaxBufferedResponseBytes = 16, new string('x', 64));

        var response = await server.CreateClient().SendAsync(PaidRequest());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(facilitator.VerifyCalled);
        Assert.False(facilitator.SettleCalled);
    }

    [Fact]
    [Trait("Rule", "D5")]
    public async Task Given_AnEndpointResponseWithinMaxBufferedResponseBytes_When_APaidRequestIsServed_Then_TheMiddlewareSettlesAndReturnsTheResponse()
    {
        var facilitator = new MockFacilitatorClient();
        using var server = Server(facilitator, options => options.MaxBufferedResponseBytes = 64, new string('x', 16));

        var response = await server.CreateClient().SendAsync(PaidRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(facilitator.SettleCalled);
        Assert.Equal(new string('x', 16), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Rule", "D5")]
    public async Task Given_AFacilitatorWhoseVerifyThrows_When_APaidRequestIsServed_Then_TheMiddlewareReturns500WithFacilitatorError()
    {
        var facilitator = new ThrowingVerifyFacilitatorClient();
        using var server = Server(facilitator, _ => { }, "premium");

        var response = await server.CreateClient().SendAsync(PaidRequest());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(X402ErrorCodes.FacilitatorError, (string?)body["error"]);
        Assert.Equal("facilitator_error", (string?)body["error"]);
        Assert.False(facilitator.SettleCalled);
    }

    [Fact]
    [Trait("Rule", "D5")]
    public async Task Given_AReplayStoreThatIsFull_When_APaidRequestIsServed_Then_TheMiddlewareReturns503WithReplayStoreFullAndNoStore()
    {
        var facilitator = new MockFacilitatorClient();
        var store = new InMemoryPaymentReplayStore(1);
        await store.TryReserveAsync("other-payment", TimeSpan.FromHours(1));
        using var server = Server(facilitator, _ => { }, "premium", services => services.AddSingleton<IPaymentReplayStore>(store));

        var response = await server.CreateClient().SendAsync(PaidRequest());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("replay_store_full", (string?)body["error"]);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.False(facilitator.SettleCalled);
    }

    private static TestServer Server(
        IFacilitatorClient facilitator,
        Action<X402Options> configure,
        string body,
        Action<IServiceCollection>? configureServices = null) =>
        new(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(facilitator);
                configureServices?.Invoke(services);
            })
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://x402.org/facilitator";
                    options.Routes.Add(new RoutePaymentConfig("/api/premium", Requirements()));
                    configure(options);
                });

                app.Run(context => context.Response.WriteAsync(body));
            }));

    private static PaymentRequirements Requirements() => new()
    {
        Scheme = "exact",
        Network = "eip155:84532",
        Amount = "10000",
        Asset = Usdc,
        PayTo = Receiver,
        MaxTimeoutSeconds = 60,
        Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
    };

    private static HttpRequestMessage PaidRequest()
    {
        var payment = new PaymentPayload
        {
            Accepted = Requirements(),
            Payload = new ExactSchemePayload
            {
                Signature = "0xsignature",
                Authorization = new Authorization
                {
                    From = Payer,
                    To = Receiver,
                    Value = "10000",
                    ValidAfter = "0",
                    ValidBefore = "1740672154",
                    Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
                }
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/premium");
        request.Headers.Add(X402Headers.PaymentSignature, Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payment))));
        return request;
    }

    private sealed class ThrowingVerifyFacilitatorClient : IFacilitatorClient
    {
        public bool SettleCalled { get; private set; }

        public Task<VerificationResponse> VerifyAsync(PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("facilitator unreachable");

        public Task<SettlementResponse> SettleAsync(PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default)
        {
            SettleCalled = true;
            return Task.FromResult(new SettlementResponse { Success = true });
        }

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse { Kinds = new List<PaymentKind>() });
    }
}
