using System.Net;
using System.Text.Json.Nodes;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71;

public class X402MatcherAuditTests
{
    private static async Task<(HttpStatusCode Status, int Verifies)> Present(Action<JsonObject> mutateAccepted)
    {
        var facilitator = new AuditFacilitator();
        using var server = AuditServer.Server(
            facilitator, new RoutePaymentConfig("/api/*", AuditServer.Requirements()), AuditServer.Ok);
        var payload = AuditServer.PayloadJson(21);
        mutateAccepted(payload["accepted"]!.AsObject());
        var response = await server.CreateClient().SendAsync(AuditServer.Request("/api/real", AuditServer.Encode(payload)));
        return (response.StatusCode, facilitator.Verifies);
    }

    public static IEnumerable<object[]> Mutations()
    {
        yield return new object[] { "amount padded", (Action<JsonObject>)(accepted => accepted["amount"] = "0100000") };
        yield return new object[] { "amount decimal", (Action<JsonObject>)(accepted => accepted["amount"] = "100000.0") };
        yield return new object[] { "amount hex", (Action<JsonObject>)(accepted => accepted["amount"] = "0x186a0") };
        yield return new object[] { "amount as number", (Action<JsonObject>)(accepted => accepted["amount"] = 100000) };
        yield return new object[] { "payTo lowercased", (Action<JsonObject>)(accepted => accepted["payTo"] = AuditServer.Payee.ToLowerInvariant()) };
        yield return new object[] { "asset lowercased", (Action<JsonObject>)(accepted => accepted["asset"] = AuditServer.Token.ToLowerInvariant()) };
        yield return new object[] { "network uppercased", (Action<JsonObject>)(accepted => accepted["network"] = "EIP155:84532") };
        yield return new object[] { "network leading zero", (Action<JsonObject>)(accepted => accepted["network"] = "eip155:084532") };
        yield return new object[] { "scheme case", (Action<JsonObject>)(accepted => accepted["scheme"] = "Exact") };
        yield return new object[] { "extra null", (Action<JsonObject>)(accepted => accepted["extra"] = null) };
        yield return new object[] { "extra name case", (Action<JsonObject>)(accepted => accepted["extra"]!.AsObject()["name"] = "usdc") };
        yield return new object[] { "extra added key", (Action<JsonObject>)(accepted => accepted["extra"]!.AsObject()["assetTransferMethod"] = "permit2") };
        yield return new object[] { "unknown top-level key", (Action<JsonObject>)(accepted => accepted["x"] = 1) };
        yield return new object[] { "maxTimeout bigger", (Action<JsonObject>)(accepted => accepted["maxTimeoutSeconds"] = 301) };
    }

    [Theory(Timeout = 60000)]
    [MemberData(nameof(Mutations))]
    public async Task Probe_Matcher_Given_AMutatedAccepted_When_Paying_Then_NoOfferMatchesAndTheFacilitatorIsNotCalled(string name, Action<JsonObject> mutate)
    {
        var (status, verifies) = await Present(mutate);

        Assert.True(status is HttpStatusCode.PaymentRequired or HttpStatusCode.BadRequest, name + " was answered with " + status);
        Assert.Equal(0, verifies);
    }

    [Fact(Timeout = 60000)]
    public async Task Probe_Matcher_Given_AnAcceptedThatEqualsTheOfferExceptForNumberFormatOfItsOwnNumbers_When_Paying_Then_ItStillMatches()
    {
        var (status, verifies) = await Present(accepted => accepted["maxTimeoutSeconds"] = 300);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1, verifies);
    }

    [Fact(Timeout = 60000)]
    public async Task Probe_Matcher_Given_ADeeplyNestedExtraInAccepted_When_Paying_Then_TheServerAnswersWithoutFailing()
    {
        var facilitator = new AuditFacilitator();
        using var server = AuditServer.Server(
            facilitator, new RoutePaymentConfig("/api/*", AuditServer.Requirements()), AuditServer.Ok);
        var json = AuditServer.PayloadJson(23).ToJsonString();
        var nested = new string('[', 5000) + new string(']', 5000);
        json = json.Replace("\"accepted\":{", "\"accepted\":{\"deep\":" + nested + ",");
        var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

        var response = await server.CreateClient().SendAsync(AuditServer.Request("/api/real", header));

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.PaymentRequired, response.StatusCode.ToString());
        Assert.Equal(0, facilitator.Verifies);
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "A5")]
    public async Task Given_DuplicateKeysInAcceptedWhoseFirstValueDiffers_When_Paying_Then_TheForwardedAcceptedIsTheOfferAndNeverCarriesTheDuplicate()
    {
        var recorded = new List<PaymentPayload>();
        var facilitator = new RecordingFacilitator(recorded);
        using var server = AuditServer.Server(
            facilitator, new RoutePaymentConfig("/api/*", AuditServer.Requirements()), AuditServer.Ok);
        var json = AuditServer.PayloadJson(25).ToJsonString();
        json = json.Replace("\"name\":\"USDC\"", "\"name\":\"EVIL\",\"name\":\"USDC\"");
        var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

        var response = await server.CreateClient().SendAsync(AuditServer.Request("/api/real", header));

        var forwarded = recorded.Count == 0
            ? string.Empty
            : System.Text.Json.JsonSerializer.Serialize(recorded[0].Accepted);
        Assert.DoesNotContain("EVIL", forwarded);
        Assert.True(response.StatusCode is HttpStatusCode.PaymentRequired or HttpStatusCode.OK or HttpStatusCode.BadRequest);
    }

    private sealed class RecordingFacilitator : IFacilitatorClient
    {
        private readonly List<PaymentPayload> _recorded;

        public RecordingFacilitator(List<PaymentPayload> recorded)
        {
            _recorded = recorded;
        }

        public Task<VerificationResponse> VerifyAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default)
        {
            _recorded.Add(p);
            return Task.FromResult(new VerificationResponse { IsValid = true, Payer = AuditServer.Payer });
        }

        public Task<SettlementResponse> SettleAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default) =>
            Task.FromResult(AuditFacilitator.Succeeded());

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken c = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse());
    }
}
