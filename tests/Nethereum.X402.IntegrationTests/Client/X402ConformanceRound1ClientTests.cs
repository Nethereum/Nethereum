using Nethereum.X402.Client;
using Nethereum.X402.IntegrationTests.AspNetCore;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.Models;
using Nethereum.X402.Server;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Nethereum.X402.IntegrationTests.Client;

public class X402ConformanceRound1ClientTests
{
    private sealed class ScriptedHandler : DelegatingHandler
    {
        private int _calls;
        public List<HttpRequestMessage> Requests { get; } = new();
        public string PaymentRequiredHeader { get; set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_calls++ == 0)
            {
                var response = new HttpResponseMessage(HttpStatusCode.PaymentRequired) { Content = new StringContent("{}") };
                response.Headers.Add("PAYMENT-REQUIRED", PaymentRequiredHeader);
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static X402HttpClientOptions Options(string network = "eip155:84532") => new()
    {
        MaxAmount = "100000",
        PreferredNetwork = network
    };

    private const string EchoRequired =
        "{\"x402Version\":2,\"resource\":{\"url\":\"https://x/r\",\"foo\":1},\"accepts\":[{\"scheme\":\"exact\",\"network\":\"eip155:84532\",\"amount\":\"1000\",\"asset\":\"0x036CbD53842c5426634e7929541eC2318f3dCF7e\",\"payTo\":\"0x209693Bc6afc0C5328bA36FaF03C514EF312287C\",\"maxTimeoutSeconds\":60,\"unknownTop\":{\"k\":1},\"extra\":{\"name\":\"USDC\",\"version\":\"2\",\"nested\":{\"z\":[1,2]},\"assetTransferMethod\":\"eip3009\"}}],\"extensions\":{\"x\":{\"info\":{\"a\":1},\"schema\":{}}}}";

    [Fact]
    [Trait("Rule", "K6")]
    public async Task Given_A402WithUnknownFieldsOnAcceptsAndResource_When_TheClientPays_Then_AcceptedAndResourceEchoThemUnchanged()
    {
        var handler = new ScriptedHandler { PaymentRequiredHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes(EchoRequired)) };
        var client = new X402HttpClient(new HttpClient(handler), X402DevChainFixture.PayerKey, Options());

        await client.GetAsync("https://x/r");

        var signature = handler.Requests[1].Headers.GetValues("PAYMENT-SIGNATURE").First();
        using var payload = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(signature)));
        var accepted = payload.RootElement.GetProperty("accepted");
        Assert.Equal(1, accepted.GetProperty("unknownTop").GetProperty("k").GetInt32());
        Assert.Equal(2, accepted.GetProperty("extra").GetProperty("nested").GetProperty("z")[1].GetInt32());
        Assert.Equal(1, payload.RootElement.GetProperty("resource").GetProperty("foo").GetInt32());
    }

    [Fact]
    [Trait("Rule", "K9")]
    public async Task Given_APaymentRequiredHeaderWithoutBase64Padding_When_TheClientReadsIt_Then_ItPays()
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes(EchoRequired));
        var unpadded = header.TrimEnd('=');
        Assert.NotEqual(header, unpadded);
        var handler = new ScriptedHandler { PaymentRequiredHeader = unpadded };
        var client = new X402HttpClient(new HttpClient(handler), X402DevChainFixture.PayerKey, Options());

        var response = await client.GetAsync("https://x/r");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(handler.Requests[1].Headers.Contains("PAYMENT-SIGNATURE"));
    }

    [Fact]
    [Trait("Rule", "K9")]
    public void Given_APaymentResponseHeaderWithoutBase64Padding_When_ReadingTheSettlement_Then_ItIsDecoded()
    {
        var json = JsonSerializer.Serialize(new SettlementResponse { Success = true, Transaction = "0xabcdef", Network = "eip155:84532", Payer = "0x1" });
        var padded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var unpadded = padded.TrimEnd('=');
        Assert.NotEqual(padded, unpadded);
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("PAYMENT-RESPONSE", unpadded);

        var settlement = response.GetSettlementResponse();

        Assert.NotNull(settlement);
        Assert.Equal("0xabcdef", settlement!.Transaction);
    }

    [Fact]
    [Trait("Rule", "K9")]
    public void Given_APaymentSignatureHeaderWithoutBase64Padding_When_TheClientDecodesIt_Then_ItIsDecoded()
    {
        var json = JsonSerializer.Serialize(new PaymentPayload { X402Version = 2, Accepted = X402ConformanceRound1WireTests.Eip3009Offer(), Payload = new { a = 1 } });
        var unpadded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=');

        var payload = X402HttpClient.DecodePaymentHeader(unpadded);

        Assert.Equal(2, payload.X402Version);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Rule", "K1")]
    public async Task Given_ARouteOfferingEip3009AndPermit2_When_TheX402HttpClientPays_Then_TheServerAcceptsTheEchoedOffer(bool permit2First)
    {
        var offers = permit2First
            ? new List<PaymentRequirements> { X402ConformanceRound1WireTests.Permit2Offer(), X402ConformanceRound1WireTests.Eip3009Offer() }
            : new List<PaymentRequirements> { X402ConformanceRound1WireTests.Eip3009Offer(), X402ConformanceRound1WireTests.Permit2Offer() };
        var facilitator = new X402ConformanceRound1WireTests.RecordingFacilitator();
        using var server = X402ConformanceRound1WireTests.Server(facilitator, route => route.Accepts = offers);
        var client = new X402HttpClient(server.CreateClient(), X402DevChainFixture.PayerKey, Options());

        var response = await client.GetAsync("/p");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var verified = Assert.Single(facilitator.Verified);
        Assert.Equal(JsonSerializer.Serialize(offers[0]), JsonSerializer.Serialize(verified));
    }

    [Fact]
    [Trait("Rule", "K6")]
    public async Task Given_ARouteWhoseRequirementCarriesAnUnknownField_When_TheX402HttpClientPays_Then_TheServerMatchesTheEcho()
    {
        var offer = X402ConformanceRound1WireTests.Eip3009Offer();
        offer.ExtensionData = new Dictionary<string, JsonElement> { ["unknownTop"] = JsonDocument.Parse("{\"k\":1}").RootElement.Clone() };
        var facilitator = new X402ConformanceRound1WireTests.RecordingFacilitator();
        using var server = X402ConformanceRound1WireTests.Server(facilitator, route => route.Requirements = offer);
        var client = new X402HttpClient(server.CreateClient(), X402DevChainFixture.PayerKey, Options());

        var response = await client.GetAsync("/p");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(facilitator.Verified);
    }
}
