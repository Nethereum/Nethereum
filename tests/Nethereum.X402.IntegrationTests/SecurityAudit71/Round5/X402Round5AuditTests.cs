using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;
using Xunit;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71.Round5;

public class X402Round5AuditTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _settleBody;
        public int Settles;

        public StubHandler(string settleBody)
        {
            _settleBody = settleBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body;
            if (request.RequestUri!.AbsolutePath.EndsWith("/verify"))
            {
                body = "{\"isValid\":true,\"payer\":\"" + AuditServer.Payer + "\"}";
            }
            else
            {
                Interlocked.Increment(ref Settles);
                body = _settleBody;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "R5-1")]
    public async Task Given_AFacilitatorAnswering200NullToASettle_When_TheSamePaymentIsReplayed_Then_ItIsNotSettledTwice()
    {
        var handler = new StubHandler("null");
        var requirements = AuditServer.Requirements();
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

        Assert.Equal(1, handler.Settles);
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "R5-2")]
    public async Task Given_AnExtensionDeclaredByTheRouteWithKeysDifferingOnlyInCase_When_TheClientEchoesItInThePayload_Then_ThePaymentIsAccepted()
    {
        var requirements = AuditServer.Requirements();
        var facilitator = new AuditFacilitator();
        var route = new RoutePaymentConfig("/api/*", requirements)
        {
            Extensions = new Dictionary<string, object>
            {
                ["bazaar"] = JsonDocument.Parse("{\"schema\":{\"properties\":{\"id\":{\"type\":\"string\"},\"ID\":{\"type\":\"integer\"}}}}").RootElement.Clone()
            }
        };
        using var server = AuditServer.Server(facilitator, route, AuditServer.Ok);
        var payload = AuditServer.PayloadJson(8, requirements);
        payload["extensions"] = JsonNode.Parse("{\"bazaar\":{\"schema\":{\"properties\":{\"id\":{\"type\":\"string\"},\"ID\":{\"type\":\"integer\"}}}}}");

        var response = await server.CreateClient().SendAsync(AuditServer.Request("/api/real", AuditServer.Encode(payload)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
