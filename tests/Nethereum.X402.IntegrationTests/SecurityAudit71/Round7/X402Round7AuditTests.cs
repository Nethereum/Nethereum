using System.Net;
using System.Net.Sockets;
using System.Text;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;
using Xunit;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71.Round7;

public class X402Round7AuditTests
{
    private sealed class ReplyLostAfterSendHandler : HttpMessageHandler
    {
        private readonly SocketError _midStreamError;
        public int Settles;

        public ReplyLostAfterSendHandler(SocketError midStreamError)
        {
            _midStreamError = midStreamError;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/verify"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"isValid\":true,\"payer\":\"" + AuditServer.Payer + "\"}", Encoding.UTF8, "application/json")
                });

            Interlocked.Increment(ref Settles);
            throw new HttpRequestException(
                HttpRequestError.ResponseEnded,
                "The response ended prematurely after the request was fully sent.",
                new IOException("Unable to read data from the transport connection.", new SocketException((int)_midStreamError)));
        }
    }

    [Theory(Timeout = 60000)]
    [InlineData(SocketError.HostUnreachable, null)]
    [InlineData(SocketError.NetworkUnreachable, null)]
    [InlineData(SocketError.HostUnreachable, X402PaymentFlows.Upfront)]
    [InlineData(SocketError.NetworkUnreachable, X402PaymentFlows.Upfront)]
    public async Task Given_AnUnreachableErrorRaisedWhileReadingTheSettleReplyAfterTheRequestWasSent_When_TheSamePaymentIsReplayed_Then_ItIsNotSettledTwice(SocketError error, string? paymentFlow)
    {
        var handler = new ReplyLostAfterSendHandler(error);
        var requirements = AuditServer.Requirements(paymentFlow);
        var client = new HttpFacilitatorClient(new HttpClient(handler), "http://facilitator.local/facilitator");
        using var server = AuditServer.Server(client, new RoutePaymentConfig("/api/*", requirements), AuditServer.Ok);
        var caller = server.CreateClient();
        var header = AuditServer.Encode(AuditServer.PayloadJson(31, requirements));

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
}
