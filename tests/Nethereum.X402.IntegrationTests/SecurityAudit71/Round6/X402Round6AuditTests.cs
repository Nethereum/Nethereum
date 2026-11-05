using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;
using Nethereum.X402.Server;
using Xunit;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71.Round6;

public class X402Round6AuditTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _settleStatus;
        private readonly string _settleBody;
        public int Settles;

        public StubHandler(string settleBody, HttpStatusCode settleStatus = HttpStatusCode.OK)
        {
            _settleBody = settleBody;
            _settleStatus = settleStatus;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/verify"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"isValid\":true,\"payer\":\"" + AuditServer.Payer + "\"}", Encoding.UTF8, "application/json")
                });

            Interlocked.Increment(ref Settles);
            return Task.FromResult(new HttpResponseMessage(_settleStatus)
            {
                Content = new StringContent(_settleBody, Encoding.UTF8, "application/json")
            });
        }
    }

    private static async Task<int> SettlesAfterReplay(string settleBody, string? paymentFlow = null)
    {
        var handler = new StubHandler(settleBody);
        var requirements = AuditServer.Requirements(paymentFlow);
        var client = new HttpFacilitatorClient(new HttpClient(handler), "http://facilitator.local/facilitator");
        using var server = AuditServer.Server(client, new RoutePaymentConfig("/api/*", requirements), AuditServer.Ok);
        var caller = server.CreateClient();
        var header = AuditServer.Encode(AuditServer.PayloadJson(7, requirements));

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                await caller.SendAsync(AuditServer.Request("/api/real", header));
            }
            catch (Exception)
            {
            }
        }

        return handler.Settles;
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "R6-1")]
    public async Task Given_AFacilitatorAnswering200WithAnEmptyObjectToASettle_When_TheSamePaymentIsReplayed_Then_ItIsNotSettledTwice()
    {
        Assert.Equal(1, await SettlesAfterReplay("{}"));
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "R6-1")]
    public async Task Given_AFacilitatorAnswering200WithAnEmptyObjectToAnUpfrontSettle_When_TheSamePaymentIsReplayed_Then_ItIsNotSettledTwice()
    {
        Assert.Equal(1, await SettlesAfterReplay("{}", X402PaymentFlows.Upfront));
    }

    [Theory(Timeout = 60000)]
    [InlineData("[]")]
    [InlineData("5")]
    [InlineData("\"x\"")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{\"success\":true}")]
    [InlineData("{\"success\":false}")]
    [InlineData("{\"errorReason\":\"\"}")]
    public async Task Given_AMalformedOrSuccessful200SettleBody_When_TheSamePaymentIsReplayed_Then_ItIsNotSettledTwice(string body)
    {
        Assert.Equal(1, await SettlesAfterReplay(body));
    }

    private sealed class Passive : IX402PaymentProcessor
    {
        public Task<VerificationResponse> VerifyPaymentAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default) =>
            Task.FromResult(new VerificationResponse { IsValid = true });

        public Task<SettlementResponse> SettlePaymentAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default) =>
            Task.FromResult(AuditFacilitator.Succeeded());

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken c = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse());
    }

    [Theory(Timeout = 60000)]
    [InlineData("\"text\"")]
    [InlineData("5")]
    [InlineData("[1]")]
    [InlineData("{\"assetTransferMethod\":5}")]
    [InlineData("{\"paymentFlow\":[]}")]
    public async Task Given_RequirementsWhoseExtraIsMalformed_When_TheExactProcessorVerifiesAndSettles_Then_ItAnswersInvalidInsteadOfThrowing(string extra)
    {
        var processor = new X402ExactSchemeProcessor(new Passive(), new Passive());
        var requirements = AuditServer.Requirements();
        requirements.Extra = JsonDocument.Parse(extra).RootElement.Clone();
        var payment = new PaymentPayload { Accepted = requirements, Payload = JsonDocument.Parse("{}").RootElement.Clone() };

        var verification = await processor.VerifyPaymentAsync(payment, requirements);
        var settlement = await processor.SettlePaymentAsync(payment, requirements);

        Assert.False(verification.IsValid);
        Assert.False(settlement.Success);
    }

    public static IEnumerable<object[]> CaseVariantDuplicates()
    {
        string[] requirementFields = { "scheme", "network", "amount", "asset", "payTo", "maxTimeoutSeconds" };
        foreach (var field in requirementFields)
            yield return new object[] { "accepted", field };

        string[] authorizationFields = { "from", "to", "value", "validAfter", "validBefore", "nonce" };
        foreach (var field in authorizationFields)
            yield return new object[] { "authorization", field };

        yield return new object[] { "payload", "signature" };
        yield return new object[] { "payload", "authorization" };
        yield return new object[] { "root", "accepted" };
        yield return new object[] { "root", "payload" };
        yield return new object[] { "root", "x402Version" };
        yield return new object[] { "root", "resource" };
    }

    [Theory(Timeout = 60000)]
    [MemberData(nameof(CaseVariantDuplicates))]
    public async Task Given_ACaseVariantDuplicateOfATypedField_When_TheHeaderIsPresented_Then_ItIsRejectedBeforeTheFacilitator(string scope, string field)
    {
        var facilitator = new AuditFacilitator();
        var requirements = AuditServer.Requirements();
        using var server = AuditServer.Server(facilitator, new RoutePaymentConfig("/api/*", requirements), AuditServer.Ok);
        var payload = AuditServer.PayloadJson(9, requirements);
        payload["resource"] = JsonNode.Parse("{\"url\":\"http://x/api/real\"}");
        JsonObject target = scope switch
        {
            "root" => payload,
            "accepted" => payload["accepted"]!.AsObject(),
            "payload" => payload["payload"]!.AsObject(),
            _ => payload["payload"]!["authorization"]!.AsObject()
        };
        var key = target.First(p => string.Equals(p.Key, field, StringComparison.OrdinalIgnoreCase)).Key;
        var clone = JsonNode.Parse(target[key]!.ToJsonString());
        var variant = key.ToUpperInvariant() == key ? key.ToLowerInvariant() : key.ToUpperInvariant();
        target.Add(variant, clone);

        var response = await server.CreateClient().SendAsync(AuditServer.Request("/api/real", AuditServer.Encode(payload)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, facilitator.Verifies);
        Assert.Equal(0, facilitator.Settles);
    }

    [Theory(Timeout = 60000)]
    [InlineData("extra", "PaymentFlow", "\"upfront\"")]
    [InlineData("extra", "assetTransferMethod", "\"permit2\"")]
    [InlineData("accepted", "extra2", "1")]
    public async Task Given_AnAcceptedWithAnExtraMemberOrCaseVariantInsideExtra_When_TheHeaderIsPresented_Then_ItDoesNotMatchTheOffer(string where, string name, string value)
    {
        var facilitator = new AuditFacilitator();
        var requirements = AuditServer.Requirements();
        using var server = AuditServer.Server(facilitator, new RoutePaymentConfig("/api/*", requirements), AuditServer.Ok);
        var payload = AuditServer.PayloadJson(10, requirements);
        var target = where == "extra" ? payload["accepted"]!["extra"]!.AsObject() : payload["accepted"]!.AsObject();
        target.Add(name, JsonNode.Parse(value));

        var response = await server.CreateClient().SendAsync(AuditServer.Request("/api/real", AuditServer.Encode(payload)));

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal(0, facilitator.Verifies);
    }
}
