using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.Extensions;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;
using System.Net;
using System.Text;
using System.Text.Json;
using Authorization = Nethereum.X402.Models.Authorization;

namespace Nethereum.X402.IntegrationTests.Facilitator;

public class X402ConformanceRound1FacilitatorTests
{
    private static PaymentRequirements Requirements() => new()
    {
        Scheme = "exact",
        Network = "eip155:84532",
        Amount = "10000",
        Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
        MaxTimeoutSeconds = 60,
        Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
    };

    private static PaymentPayload Payload() => new()
    {
        X402Version = 2,
        Accepted = Requirements(),
        Payload = new ExactSchemePayload
        {
            Signature = "0x" + new string('1', 130),
            Authorization = new Authorization
            {
                From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
                To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                Value = "10000",
                ValidAfter = "0",
                ValidBefore = "99999999999",
                Nonce = "0x" + new string('a', 64)
            }
        }
    };

    private static HttpFacilitatorClient ClientAnswering(HttpStatusCode status, string body, string contentType = "application/json") =>
        new(
            new HttpClient(new MockHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, contentType) }))),
            "https://facilitator.example.com");

    private const string VerifyBody = "{\"isValid\":false,\"invalidReason\":\"insufficient_funds\",\"invalidMessage\":\"m\",\"payer\":\"0xabc\"}";
    private const string SettleBody = "{\"success\":false,\"errorReason\":\"insufficient_funds\",\"errorMessage\":\"m\",\"transaction\":\"\",\"network\":\"eip155:84532\",\"payer\":\"0xabc\"}";

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [Trait("Rule", "K2")]
    public async Task Given_ANonSuccessVerifyStatusWithAVerificationBody_When_Verifying_Then_TheFailedResponseIsReturnedWithItsReason(HttpStatusCode status)
    {
        var client = ClientAnswering(status, VerifyBody);

        var response = await client.VerifyAsync(Payload(), Requirements());

        Assert.False(response.IsValid);
        Assert.Equal("insufficient_funds", response.InvalidReason);
        Assert.Equal("0xabc", response.Payer);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [Trait("Rule", "K2")]
    public async Task Given_ANonSuccessSettleStatusWithASettlementBody_When_Settling_Then_TheFailedResponseIsReturnedWithItsReason(HttpStatusCode status)
    {
        var client = ClientAnswering(status, SettleBody);

        var response = await client.SettleAsync(Payload(), Requirements());

        Assert.False(response.Success);
        Assert.Equal("insufficient_funds", response.ErrorReason);
        Assert.Equal("eip155:84532", response.Network);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Redirect)]
    [Trait("Rule", "D1")]
    public async Task Given_ASettleStatusOutsideTheDefiniteAllowlistWithASettlementBody_When_Settling_Then_TheOutcomeIsSettlementPending(HttpStatusCode status)
    {
        var client = ClientAnswering(status, SettleBody);

        var response = await client.SettleAsync(Payload(), Requirements());

        Assert.False(response.Success);
        Assert.Equal(X402ErrorCodes.SettlementPending, response.ErrorReason);
        Assert.Equal("eip155:84532", response.Network);
    }

    [Fact]
    [Trait("Rule", "K2")]
    public async Task Given_A500WithANonJsonBody_When_Verifying_Then_ItStillFailsAsBefore()
    {
        var client = ClientAnswering(HttpStatusCode.InternalServerError, "upstream exploded", "text/plain");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.VerifyAsync(Payload(), Requirements()));
    }

    [Fact]
    [Trait("Rule", "K2")]
    public async Task Given_A500WithANonJsonBody_When_Settling_Then_ItStillFailsAsBefore()
    {
        var client = ClientAnswering(HttpStatusCode.InternalServerError, "upstream exploded", "text/plain");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SettleAsync(Payload(), Requirements()));
    }

    [Fact]
    [Trait("Rule", "K2")]
    public async Task Given_A500WithJsonThatCarriesNoIsValid_When_Verifying_Then_ItStillFailsAsBefore()
    {
        var client = ClientAnswering(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.VerifyAsync(Payload(), Requirements()));
    }

    [Fact]
    [Trait("Rule", "K2")]
    public async Task Given_A500WithJsonThatCarriesNoSuccess_When_Settling_Then_ItStillFailsAsBefore()
    {
        var client = ClientAnswering(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SettleAsync(Payload(), Requirements()));
    }

    [Fact]
    [Trait("Rule", "K8")]
    public async Task Given_AFacilitatorAnswerCarryingMessages_When_TheHttpClientReadsIt_Then_InvalidMessageAndErrorMessageRoundTrip()
    {
        var verify = await ClientAnswering(HttpStatusCode.OK, VerifyBody).VerifyAsync(Payload(), Requirements());
        var settle = await ClientAnswering(HttpStatusCode.OK, SettleBody).SettleAsync(Payload(), Requirements());

        Assert.Equal("m", verify.InvalidMessage);
        Assert.Equal("m", settle.ErrorMessage);
        using var verifyJson = JsonDocument.Parse(JsonSerializer.Serialize(verify));
        using var settleJson = JsonDocument.Parse(JsonSerializer.Serialize(settle));
        Assert.Equal("m", verifyJson.RootElement.GetProperty("invalidMessage").GetString());
        Assert.Equal("m", settleJson.RootElement.GetProperty("errorMessage").GetString());
    }

    [Fact]
    [Trait("Rule", "K8")]
    public void Given_ResponsesWithoutMessages_When_Serialized_Then_TheMessageFieldsAreOmitted()
    {
        using var verifyJson = JsonDocument.Parse(JsonSerializer.Serialize(new VerificationResponse { IsValid = true, Payer = "0x1" }));
        using var settleJson = JsonDocument.Parse(JsonSerializer.Serialize(new SettlementResponse { Success = true, Transaction = "0x1", Network = "eip155:1" }));

        Assert.False(verifyJson.RootElement.TryGetProperty("invalidMessage", out _));
        Assert.False(settleJson.RootElement.TryGetProperty("errorMessage", out _));
    }

    private sealed class StubProcessor : IX402PaymentProcessor
    {
        public Task<VerificationResponse> VerifyPaymentAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default) =>
            Task.FromResult(new VerificationResponse { IsValid = true, Payer = "0x1" });

        public Task<SettlementResponse> SettlePaymentAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default) =>
            Task.FromResult(new SettlementResponse { Success = true, Transaction = "0x1", Network = r.Network });

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken c = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse { Kinds = new List<PaymentKind>() });
    }

    private static TestServer FacilitatorServer(string? routePrefix)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddControllers().AddX402FacilitatorControllers();
                services.AddSingleton<IX402PaymentProcessor>(new StubProcessor());
                if (routePrefix != null)
                    services.AddX402FacilitatorOptions(options => options.RoutePrefix = routePrefix);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });
        return new TestServer(builder);
    }

    private static StringContent VerifyRequest() =>
        new(JsonSerializer.Serialize(new { x402Version = 2, paymentPayload = Payload(), paymentRequirements = Requirements() }), Encoding.UTF8, "application/json");

    [Fact]
    [Trait("Rule", "K5")]
    public async Task Given_NoRoutePrefixConfigured_When_CallingTheFacilitator_Then_TheShippedFacilitatorPrefixIsUnchanged()
    {
        using var server = FacilitatorServer(null);
        var client = server.CreateClient();

        var prefixed = await client.PostAsync("/facilitator/verify", VerifyRequest());
        var root = await client.PostAsync("/verify", VerifyRequest());

        Assert.Equal(HttpStatusCode.OK, prefixed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, root.StatusCode);
    }

    [Fact]
    [Trait("Rule", "K5")]
    public async Task Given_AnEmptyRoutePrefix_When_CallingTheFacilitator_Then_VerifySettleAndSupportedAreServedAtTheRoot()
    {
        using var server = FacilitatorServer("");
        var client = server.CreateClient();

        var verify = await client.PostAsync("/verify", VerifyRequest());
        var settle = await client.PostAsync("/settle", VerifyRequest());
        var supported = await client.GetAsync("/supported");
        var oldRoute = await client.PostAsync("/facilitator/verify", VerifyRequest());

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        Assert.Equal(HttpStatusCode.OK, settle.StatusCode);
        Assert.Equal(HttpStatusCode.OK, supported.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, oldRoute.StatusCode);
    }

    [Fact]
    [Trait("Rule", "K5")]
    public async Task Given_ACustomRoutePrefix_When_CallingTheFacilitator_Then_TheEndpointsLiveUnderIt()
    {
        using var server = FacilitatorServer("x402/v2");
        var client = server.CreateClient();

        var custom = await client.PostAsync("/x402/v2/verify", VerifyRequest());
        var oldRoute = await client.PostAsync("/facilitator/verify", VerifyRequest());

        Assert.Equal(HttpStatusCode.OK, custom.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, oldRoute.StatusCode);
    }

    [Fact]
    [Trait("Rule", "K5")]
    public void Given_ANullRoutePrefix_When_Validating_Then_ItIsRejected()
    {
        var options = new X402FacilitatorOptions { RoutePrefix = null! };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
