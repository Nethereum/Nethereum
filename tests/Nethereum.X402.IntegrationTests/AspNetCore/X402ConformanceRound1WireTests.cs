using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;
using System.Net;
using System.Text;
using System.Text.Json;
using Authorization = Nethereum.X402.Models.Authorization;

namespace Nethereum.X402.IntegrationTests.AspNetCore;

public class X402ConformanceRound1WireTests
{
    private const string NoMatch = "No matching payment requirements";

    internal sealed class RecordingFacilitator : IFacilitatorClient
    {
        public List<PaymentRequirements> Verified { get; } = new();
        public List<PaymentRequirements> Settled { get; } = new();
        public List<string> Order { get; } = new();

        public Task<VerificationResponse> VerifyAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default)
        {
            Verified.Add(r);
            Order.Add("verify");
            return Task.FromResult(new VerificationResponse { IsValid = true, Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66" });
        }

        public Task<SettlementResponse> SettleAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default)
        {
            Settled.Add(r);
            Order.Add("settle");
            return Task.FromResult(new SettlementResponse
            {
                Success = true,
                Transaction = "0xabc",
                Network = r.Network,
                Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
            });
        }

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken c = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse { Kinds = new List<PaymentKind>() });
    }

    internal static PaymentRequirements Eip3009Offer() => new()
    {
        Scheme = "exact",
        Network = "eip155:84532",
        Amount = "10000",
        Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
        MaxTimeoutSeconds = 60,
        Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
    };

    internal static PaymentRequirements Permit2Offer() => new()
    {
        Scheme = "exact",
        Network = "eip155:84532",
        Amount = "10000",
        Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
        MaxTimeoutSeconds = 60,
        Extra = new ExactSchemeExtra { AssetTransferMethod = "permit2", Name = "USDC", Version = "2" }
    };

    internal static TestServer Server(IFacilitatorClient facilitator, Action<RoutePaymentConfig>? configure = null)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton(facilitator))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://facilitator.test";
                    var route = new RoutePaymentConfig(
                        "/p",
                        Eip3009Offer(),
                        new ResourceInfo { Url = "https://api.example.com/premium-data", Description = "Access", MimeType = "application/json" });
                    configure?.Invoke(route);
                    options.Routes.Add(route);
                });
                app.Run(context => context.Response.WriteAsync("premium"));
            });
        return new TestServer(builder);
    }

    private static string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static PaymentPayload Payload(PaymentRequirements accepted) => accepted.Extra is ExactSchemeExtra { AssetTransferMethod: "permit2" } ? Permit2Payload(accepted) : Eip3009Payload(accepted);

    private static PaymentPayload Permit2Payload(PaymentRequirements accepted) => new()
    {
        X402Version = 2,
        Accepted = accepted,
        Payload = new Permit2SchemePayload
        {
            Signature = "0x" + new string('1', 130),
            Permit2Authorization = new Permit2Authorization
            {
                From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
                Permitted = new Permit2TokenPermissions { Token = accepted.Asset, Amount = "10000" },
                Spender = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                Nonce = "5",
                Deadline = "99999999999",
                Witness = new Permit2WitnessData { To = accepted.PayTo, ValidAfter = "0" }
            }
        }
    };

    private static PaymentPayload Eip3009Payload(PaymentRequirements accepted) => new()
    {
        X402Version = 2,
        Accepted = accepted,
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

    private static Task<HttpResponseMessage> Pay(TestServer server, string headerValue)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/p");
        request.Headers.TryAddWithoutValidation("PAYMENT-SIGNATURE", headerValue);
        return server.CreateClient().SendAsync(request);
    }

    private static Task<HttpResponseMessage> Pay(TestServer server, PaymentRequirements accepted) =>
        Pay(server, B64(JsonSerializer.Serialize(Payload(accepted))));

    private static async Task<string?> ErrorOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.TryGetProperty("error", out var error) ? error.GetString() : null;
    }

    public static IEnumerable<object[]> MutatedFields() => new[]
    {
        new object[] { "amount" },
        new object[] { "payTo" },
        new object[] { "asset" },
        new object[] { "maxTimeoutSeconds" },
        new object[] { "extra" },
        new object[] { "network" },
        new object[] { "scheme" }
    };

    [Theory]
    [MemberData(nameof(MutatedFields))]
    [Trait("Rule", "K1")]
    public async Task Given_AcceptedThatDiffersFromTheRouteRequirementInOneField_When_Paying_Then_402NoMatchingPaymentRequirementsAndTheFacilitatorIsNeverCalled(string field)
    {
        var accepted = Eip3009Offer();
        switch (field)
        {
            case "amount": accepted.Amount = "1"; break;
            case "payTo": accepted.PayTo = "0x0000000000000000000000000000000000000001"; break;
            case "asset": accepted.Asset = "0x0000000000000000000000000000000000000002"; break;
            case "maxTimeoutSeconds": accepted.MaxTimeoutSeconds = 999; break;
            case "extra": accepted.Extra = new ExactSchemeExtra { Name = "X", Version = "9" }; break;
            case "network": accepted.Network = "eip155:1"; break;
            case "scheme": accepted.Scheme = "upto"; break;
        }
        var facilitator = new RecordingFacilitator();
        using var server = Server(facilitator);

        var response = await Pay(server, accepted);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal(NoMatch, await ErrorOf(response));
        Assert.Empty(facilitator.Verified);
        Assert.Empty(facilitator.Settled);
    }

    [Fact]
    [Trait("Rule", "K1")]
    public async Task Given_AcceptedMissingEntirely_When_Paying_Then_402NoMatchingPaymentRequirements()
    {
        var facilitator = new RecordingFacilitator();
        using var server = Server(facilitator);
        var payload = Payload(Eip3009Offer());
        payload.Accepted = null!;

        var response = await Pay(server, B64(JsonSerializer.Serialize(payload)));

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal(NoMatch, await ErrorOf(response));
        Assert.Empty(facilitator.Verified);
    }

    [Fact]
    [Trait("Rule", "K1")]
    public async Task Given_AcceptedWithKeysInAnotherOrder_When_Paying_Then_ItMatchesTheRouteRequirement()
    {
        var facilitator = new RecordingFacilitator();
        using var server = Server(facilitator);
        var json = JsonSerializer.Serialize(Payload(Eip3009Offer()));
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        var accepted = node["accepted"]!.AsObject();
        var reordered = new System.Text.Json.Nodes.JsonObject();
        foreach (var key in accepted.Select(entry => entry.Key).Reverse().ToList())
        {
            var value = accepted[key];
            accepted.Remove(key);
            reordered[key] = value;
        }
        node["accepted"] = reordered;

        var response = await Pay(server, B64(node.ToJsonString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(facilitator.Verified);
    }

    [Fact]
    [Trait("Rule", "K1")]
    public async Task Given_ARouteWithTwoAccepts_When_NoPaymentIsSent_Then_The402OffersBoth()
    {
        using var server = Server(new RecordingFacilitator(), route => route.Accepts = new List<PaymentRequirements> { Eip3009Offer(), Permit2Offer() });

        var response = await server.CreateClient().GetAsync("/p");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        var required = JsonSerializer.Deserialize<PaymentRequired>(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(2, required.Accepts.Count);
        var header = JsonSerializer.Deserialize<PaymentRequired>(Encoding.UTF8.GetString(Convert.FromBase64String(response.Headers.GetValues("PAYMENT-REQUIRED").First())))!;
        Assert.Equal(2, header.Accepts.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Rule", "K1")]
    public async Task Given_ARouteOfferingEip3009AndPermit2_When_PayingWithEither_Then_TheMatchedOfferIsVerifiedAndSettled(bool permit2)
    {
        var facilitator = new RecordingFacilitator();
        using var server = Server(facilitator, route => route.Accepts = new List<PaymentRequirements> { Eip3009Offer(), Permit2Offer() });
        var chosen = permit2 ? Permit2Offer() : Eip3009Offer();

        var response = await Pay(server, chosen);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var verified = Assert.Single(facilitator.Verified);
        var settled = Assert.Single(facilitator.Settled);
        Assert.Equal(JsonSerializer.Serialize(chosen), JsonSerializer.Serialize(verified));
        Assert.Same(verified, settled);
    }

    [Fact]
    [Trait("Rule", "K1")]
    public async Task Given_ARouteWithAcceptsSet_When_Paying_Then_TheSingleRequirementsPropertyIsNotAnOffer()
    {
        var facilitator = new RecordingFacilitator();
        using var server = Server(facilitator, route => route.Accepts = new List<PaymentRequirements> { Permit2Offer() });

        var response = await Pay(server, Eip3009Offer());

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal(NoMatch, await ErrorOf(response));
    }

    [Fact]
    [Trait("Rule", "K1")]
    public async Task Given_AnUpfrontOfferAmongTwoAccepts_When_PayingWithIt_Then_ItIsVerifiedThenSettledBeforeTheHandler()
    {
        var upfront = Eip3009Offer();
        upfront.Extra = new ExactSchemeExtra { PaymentFlow = "upfront", Name = "USDC", Version = "2" };
        var facilitator = new RecordingFacilitator();
        using var server = Server(facilitator, route => route.Accepts = new List<PaymentRequirements> { Eip3009Offer(), upfront });

        var response = await Pay(server, upfront);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(facilitator.Verified);
        Assert.Single(facilitator.Settled);
    }

    [Fact]
    [Trait("Rule", "K1")]
    public async Task Given_AMatchedOffer_When_ThePaymentIsReplayed_Then_TheReplayIsRejectedOnTheMatchedRequirement()
    {
        var facilitator = new RecordingFacilitator();
        using var server = Server(facilitator, route => route.Accepts = new List<PaymentRequirements> { Eip3009Offer(), Permit2Offer() });
        var header = B64(JsonSerializer.Serialize(Payload(Eip3009Offer())));

        var first = await Pay(server, header);
        var second = await Pay(server, header);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, second.StatusCode);
        Assert.Equal(X402ErrorCodes.NonceAlreadyUsed, await ErrorOf(second));
    }

    [Fact]
    [Trait("Rule", "K6")]
    public async Task Given_ARouteRequirementWithAnUnknownField_When_AcceptedLacksIt_Then_NoMatch()
    {
        var offer = Eip3009Offer();
        offer.ExtensionData = new Dictionary<string, JsonElement> { ["unknownTop"] = JsonDocument.Parse("{\"k\":1}").RootElement.Clone() };
        var facilitator = new RecordingFacilitator();
        using var server = Server(facilitator, route => route.Requirements = offer);

        var response = await Pay(server, Eip3009Offer());

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal(NoMatch, await ErrorOf(response));
        Assert.Empty(facilitator.Verified);
    }

    [Fact]
    [Trait("Rule", "K6")]
    public async Task Given_ARouteRequirementWithAnUnknownField_When_AcceptedEchoesIt_Then_ItMatches()
    {
        var offer = Eip3009Offer();
        offer.ExtensionData = new Dictionary<string, JsonElement> { ["unknownTop"] = JsonDocument.Parse("{\"k\":1}").RootElement.Clone() };
        var facilitator = new RecordingFacilitator();
        using var server = Server(facilitator, route => route.Requirements = offer);
        var echoed = JsonSerializer.Deserialize<PaymentRequirements>(JsonSerializer.Serialize(offer))!;

        var response = await Pay(server, echoed);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(facilitator.Verified);
    }

    [Fact]
    [Trait("Rule", "K6")]
    public void Given_PaymentRequirementsAndResourceInfoWithUnknownProperties_When_RoundTripped_Then_TheUnknownPropertiesSurvive()
    {
        var requirementsJson = "{\"scheme\":\"exact\",\"network\":\"eip155:84532\",\"amount\":\"1\",\"asset\":\"0x1\",\"payTo\":\"0x2\",\"maxTimeoutSeconds\":60,\"unknownTop\":{\"k\":1},\"extra\":{\"name\":\"USDC\",\"nested\":{\"z\":[1,2]}}}";
        var resourceJson = "{\"url\":\"https://x/r\",\"foo\":1}";

        var requirements = JsonSerializer.Deserialize<PaymentRequirements>(requirementsJson)!;
        var resource = JsonSerializer.Deserialize<ResourceInfo>(resourceJson)!;

        using var requirementsOut = JsonDocument.Parse(JsonSerializer.Serialize(requirements));
        using var resourceOut = JsonDocument.Parse(JsonSerializer.Serialize(resource));
        Assert.Equal(1, requirementsOut.RootElement.GetProperty("unknownTop").GetProperty("k").GetInt32());
        Assert.Equal(2, requirementsOut.RootElement.GetProperty("extra").GetProperty("nested").GetProperty("z")[1].GetInt32());
        Assert.Equal(1, resourceOut.RootElement.GetProperty("foo").GetInt32());
    }

    [Fact]
    [Trait("Rule", "K9")]
    public async Task Given_APaymentSignatureHeaderWithoutBase64Padding_When_Paying_Then_ItIsAccepted()
    {
        var facilitator = new RecordingFacilitator();
        using var server = Server(facilitator);
        var padded = B64(JsonSerializer.Serialize(Payload(Eip3009Offer())));
        var unpadded = padded.TrimEnd('=');
        Assert.NotEqual(padded, unpadded);

        var response = await Pay(server, unpadded);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(facilitator.Verified);
    }

    [Fact]
    [Trait("Rule", "K9")]
    public async Task Given_APaymentSignatureHeaderThatIsNotBase64_When_Paying_Then_ItIsStillRejected()
    {
        var facilitator = new RecordingFacilitator();
        using var server = Server(facilitator);

        var response = await Pay(server, "!!notbase64!!");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(X402ErrorCodes.InvalidPayload, await ErrorOf(response));
        Assert.Empty(facilitator.Verified);
    }
}
