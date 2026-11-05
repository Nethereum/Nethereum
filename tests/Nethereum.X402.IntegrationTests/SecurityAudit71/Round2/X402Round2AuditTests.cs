using System.Net;
using System.Text;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.SecurityAudit71;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71.Round2;

public class X402Round2AuditTests
{
    private sealed class DripContent : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            while (true)
            {
                await stream.WriteAsync(new byte[] { (byte)' ' });
                await stream.FlushAsync();
                await Task.Delay(50);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new DripStream());
    }

    private sealed class DripStream : Stream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(50, cancellationToken);
            buffer.Span[0] = (byte)' ';
            return 1;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class DripHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new DripContent() });
    }

    private sealed class ThrowingFacilitator : IFacilitatorClient
    {
        public Task<VerificationResponse> VerifyAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default) =>
            Task.FromResult(new VerificationResponse { IsValid = true, Payer = AuditServer.Payer });

        public Task<SettlementResponse> SettleAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default) =>
            throw new HttpRequestException(HttpRequestError.ConnectionError, "connection refused", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused));

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken c = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse());
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "R2-1")]
    public async Task Given_AnUpfrontRouteAndAnUnreachableFacilitator_When_UnpaidPaymentsWithFreshNoncesArrive_Then_TheyDoNotFillTheReplayStoreForTheHonestPayer()
    {
        var requirements = AuditServer.Requirements(X402PaymentFlows.Upfront);
        using var server = AuditServer.Server(
            new ThrowingFacilitator(),
            new RoutePaymentConfig("/api/*", requirements),
            AuditServer.Ok,
            maxEntries: 3);
        var client = server.CreateClient();

        for (var junk = 1; junk <= 3; junk++)
            await client.SendAsync(AuditServer.Request("/api/real", AuditServer.Encode(AuditServer.PayloadJson(junk, requirements))));

        var honest = await client.SendAsync(AuditServer.Request("/api/real", AuditServer.Encode(AuditServer.PayloadJson(99, requirements))));

        Assert.NotEqual(HttpStatusCode.ServiceUnavailable, honest.StatusCode);
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "R2-2")]
    public async Task Given_AFacilitatorThatSendsHeadersThenDripsTheSupportedBody_When_TheHttpClientTimeoutElapses_Then_GetSupportedStops()
    {
        var http = new HttpClient(new DripHandler()) { Timeout = TimeSpan.FromSeconds(2) };
        var client = new HttpFacilitatorClient(http, "http://facilitator.local");

        var call = client.GetSupportedAsync();
        var finished = await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.Same(call, finished);
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "R2-3")]
    public async Task Given_APayloadWithTwoNonceKeysInTheAuthorization_When_Presented_Then_ItIsRejectedAsMalformed()
    {
        var facilitator = new AuditFacilitator();
        using var server = AuditServer.Server(
            facilitator,
            new RoutePaymentConfig("/api/*", AuditServer.Requirements()),
            AuditServer.Ok);
        var client = server.CreateClient();

        var json = AuditServer.PayloadJson(7).ToJsonString();
        var real = "\"nonce\":\"" + AuditServer.Nonce(7) + "\"";
        Assert.Contains(real, json);
        var duplicated = json.Replace(real, "\"nonce\":\"" + AuditServer.Nonce(8) + "\"," + real);
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes(duplicated));

        var response = await client.SendAsync(AuditServer.Request("/api/real", header));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
