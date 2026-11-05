using System.Net;
using System.Text;
using Nethereum.X402.Client;
using Xunit;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71.Round8;

public class X402Round8AuditTests
{
    private sealed class DripContent : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            while (true)
            {
                await stream.WriteAsync(new byte[] { (byte)' ' });
                await Task.Delay(100);
            }
        }

        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new DripStream());

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class DripStream : Stream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(100, cancellationToken);
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
        private readonly HttpStatusCode _status;

        public DripHandler(HttpStatusCode status)
        {
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status) { Content = new DripContent() });
    }

    [Theory(Timeout = 60000)]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.OK)]
    public async Task Given_AServerThatDripsTheResponseBody_When_TheClientHasAShortHttpClientTimeout_Then_TheCallEndsAtThatTimeout(HttpStatusCode status)
    {
        var http = new HttpClient(new DripHandler(status)) { Timeout = TimeSpan.FromSeconds(1) };
        var options = new X402HttpClientOptions { MaxAmount = "1000000", PreferredNetwork = AuditServer.Network };
        var client = new X402HttpClient(http, "0x" + new string('1', 63) + "2", options);
        using var escape = new CancellationTokenSource();

        var call = client.GetAsync("http://resource.local/api/real", escape.Token);
        var finished = await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(10)));
        escape.Cancel();

        Assert.Same(call, finished);
    }
}
