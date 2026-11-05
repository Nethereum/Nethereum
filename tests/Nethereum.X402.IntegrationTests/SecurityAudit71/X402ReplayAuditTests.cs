using System.Net;
using System.Text.Json.Nodes;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71;

public class X402ReplayAuditTests
{
    private static JsonObject WithJunkPermit2Authorization(JsonObject payload, int junkNonce)
    {
        var inner = payload["payload"]!.AsObject();
        inner["permit2Authorization"] = new JsonObject
        {
            ["from"] = AuditServer.Payer,
            ["nonce"] = junkNonce.ToString(),
            ["deadline"] = "4102444800",
            ["spender"] = AuditServer.Payer,
            ["permitted"] = new JsonObject { ["token"] = AuditServer.Token, ["amount"] = "1" },
            ["witness"] = new JsonObject { ["to"] = AuditServer.Payee, ["validAfter"] = "0" }
        };
        return payload;
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "A1")]
    public async Task Given_AnEip3009PaymentWithAJunkPermit2AuthorizationOfAnotherNonce_When_TheSamePaymentIsPresentedAgain_Then_ItIsRejectedAsAReplay()
    {
        var facilitator = new AuditFacilitator();
        var handlerRuns = 0;
        using var server = AuditServer.Server(
            facilitator,
            new RoutePaymentConfig("/api/*", AuditServer.Requirements()),
            context =>
            {
                Interlocked.Increment(ref handlerRuns);
                return AuditServer.Ok(context);
            });
        var client = server.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var junk = 1; junk <= 3; junk++)
        {
            var header = AuditServer.Encode(WithJunkPermit2Authorization(AuditServer.PayloadJson(7), junk));
            statuses.Add((await client.SendAsync(AuditServer.Request("/api/real", header))).StatusCode);
        }

        Assert.Equal(
            new[] { HttpStatusCode.OK, HttpStatusCode.PaymentRequired, HttpStatusCode.PaymentRequired },
            statuses);
        Assert.Equal(1, handlerRuns);
        Assert.Equal(1, facilitator.Settles);
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "A2")]
    public async Task Given_AnUpfrontSettlementThatIsPending_When_TheSamePaymentIsPresentedAgain_Then_ItIsRejectedAsAReplay()
    {
        var facilitator = new AuditFacilitator
        {
            Settle = () => AuditFacilitator.Failed(X402ErrorCodes.SettlementPending, "0xpending")
        };
        using var server = AuditServer.Server(
            facilitator,
            new RoutePaymentConfig("/api/*", AuditServer.Requirements(X402PaymentFlows.Upfront)),
            AuditServer.Ok);
        var client = server.CreateClient();
        var header = AuditServer.Encode(AuditServer.PayloadJson(11, AuditServer.Requirements(X402PaymentFlows.Upfront)));

        var pending = await client.SendAsync(AuditServer.Request("/api/real", header));
        var again = await client.SendAsync(AuditServer.Request("/api/real", header));

        Assert.Equal(HttpStatusCode.PaymentRequired, pending.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, again.StatusCode);
        Assert.Contains(X402ErrorCodes.NonceAlreadyUsed, await again.Content.ReadAsStringAsync());
        Assert.Equal(1, facilitator.Settles);
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "A3")]
    public async Task Given_ASettleWhoseOutcomeIsUnknownBecauseTheFacilitatorCallThrew_When_TheSamePaymentIsPresentedAgain_Then_ItIsNotSettledASecondTime()
    {
        var facilitator = new AuditFacilitator
        {
            Settle = () => throw new HttpRequestException(HttpRequestError.ResponseEnded, "connection reset after the facilitator broadcast")
        };
        using var server = AuditServer.Server(
            facilitator,
            new RoutePaymentConfig("/api/*", AuditServer.Requirements()),
            AuditServer.Ok);
        var client = server.CreateClient();
        var header = AuditServer.Encode(AuditServer.PayloadJson(13));

        await client.SendAsync(AuditServer.Request("/api/real", header));
        await client.SendAsync(AuditServer.Request("/api/real", header));

        Assert.Equal(1, facilitator.Settles);
    }

    [Fact(Timeout = 60000)]
    public async Task Probe_Base64Variants_Given_ThePaymentReEncodedWithPaddingRemovedAndWhitespace_When_Presented_Then_TheReplayKeyIsTheSame()
    {
        var facilitator = new AuditFacilitator();
        using var server = AuditServer.Server(
            facilitator, new RoutePaymentConfig("/api/*", AuditServer.Requirements()), AuditServer.Ok);
        var client = server.CreateClient();
        var header = AuditServer.Encode(AuditServer.PayloadJson(17));

        var first = await client.SendAsync(AuditServer.Request("/api/real", header));
        var unpadded = await client.SendAsync(AuditServer.Request("/api/real", header.TrimEnd('=')));
        var spaced = await client.SendAsync(AuditServer.Request("/api/real", header.Insert(8, "  ").Insert(20, "  ")));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, unpadded.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, spaced.StatusCode);
        Assert.Equal(1, facilitator.Settles);
    }

    [Fact(Timeout = 60000)]
    public async Task Probe_NonceVariants_Given_TheSameNonceAsUppercaseHexAndAnUppercasePrefix_When_Presented_Then_TheReplayKeyIsTheSame()
    {
        var facilitator = new AuditFacilitator();
        using var server = AuditServer.Server(
            facilitator, new RoutePaymentConfig("/api/*", AuditServer.Requirements()), AuditServer.Ok);
        var client = server.CreateClient();

        var first = await client.SendAsync(AuditServer.Request("/api/real", AuditServer.Encode(AuditServer.PayloadJson(0xabc))));
        var variant = AuditServer.PayloadJson(0xabc);
        var authorization = variant["payload"]!["authorization"]!.AsObject();
        authorization["nonce"] = "0X" + AuditServer.Nonce(0xabc).Substring(2).ToUpperInvariant();
        authorization["from"] = AuditServer.Payer.ToLowerInvariant();
        var second = await client.SendAsync(AuditServer.Request("/api/real", AuditServer.Encode(variant)));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, second.StatusCode);
        Assert.Equal(1, facilitator.Settles);
    }
}
