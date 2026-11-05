using System.Net;
using System.Text;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71;

public class X402FacilitatorResponseAuditTests
{
    private sealed class CannedHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly Func<string> _body;

        public CannedHandler(HttpStatusCode status, Func<string> body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body(), Encoding.UTF8, "application/json")
            });
    }

    private static HttpFacilitatorClient Client(HttpStatusCode status, Func<string> body) =>
        new(new HttpClient(new CannedHandler(status, body)), "http://facilitator.local");

    private static PaymentPayload Payment() =>
        new() { X402Version = 2, Accepted = AuditServer.Requirements(), Payload = new object() };

    [Fact(Timeout = 120000)]
    [Trait("Rule", "A4")]
    public async Task Given_AFacilitatorAnswerOfSixtyFourMegabytes_When_Verifying_Then_TheClientRefusesTheOversizedBody()
    {
        var client = Client(HttpStatusCode.OK, () =>
            "{\"isValid\":true,\"payer\":\"" + AuditServer.Payer + "\",\"invalidMessage\":\"" + new string('A', 64 * 1024 * 1024) + "\"}");

        VerificationResponse? response = null;
        Exception? failure = null;
        try
        {
            response = await client.VerifyAsync(Payment(), AuditServer.Requirements());
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        Assert.True(
            failure != null || response is { IsValid: false },
            "A 64 MiB facilitator body was buffered and parsed as a valid verification.");
    }

    [Theory(Timeout = 60000)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task Probe_LyingNon2xxBodies_Given_ASuccessBodyOnAFailureStatus_When_VerifyingAndSettling_Then_TheResponseIsAFailure(HttpStatusCode status)
    {
        var verify = await Client(status, () => "{\"isValid\":true,\"payer\":\"0x1\"}")
            .VerifyAsync(Payment(), AuditServer.Requirements());
        var settle = await Client(status, () => "{\"success\":true,\"transaction\":\"0xabc\",\"network\":\"eip155:84532\"}")
            .SettleAsync(Payment(), AuditServer.Requirements());

        Assert.False(verify.IsValid);
        Assert.False(settle.Success);
    }
}
