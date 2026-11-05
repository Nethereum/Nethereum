using System.Net;
using Nethereum.X402.Client;
using Xunit;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71.Round7;

public class X402Round7ClientAuditTests
{
    private sealed class EndlessStream : Stream
    {
        public long Served;
        private const long Total = 256L * 1024 * 1024;

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = (int)Math.Min(count, Total - Served);
            Array.Fill(buffer, (byte)' ', offset, n);
            Served += n;
            return n;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var n = (int)Math.Min(buffer.Length, Total - Served);
            buffer.Span.Slice(0, n).Fill((byte)' ');
            Served += n;
            return new ValueTask<int>(n);
        }

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

    private sealed class EndlessContent : HttpContent
    {
        public readonly EndlessStream Stream = new();

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Stream.CopyToAsync(stream);

        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(Stream);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class Big402Handler : HttpMessageHandler
    {
        public readonly EndlessContent Content = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired) { Content = Content });
    }

    [Fact(Timeout = 120000)]
    public async Task Given_AServerAnswering402WithAHugeBody_When_TheAutomaticClientReadsTheRequirements_Then_ItStopsReadingAtABound()
    {
        var handler = new Big402Handler();
        var options = new X402HttpClientOptions { MaxAmount = "1000000", PreferredNetwork = AuditServer.Network };
        var client = new X402HttpClient(new HttpClient(handler), "0x" + new string('1', 63) + "2", options);

        try
        {
            await client.GetAsync("http://resource.local/api/real");
        }
        catch (Exception)
        {
        }

        Assert.True(handler.Content.Stream.Served < 16L * 1024 * 1024, "read " + handler.Content.Stream.Served + " bytes");
    }

    private sealed class ClosableContent : HttpContent
    {
        public bool ConnectionClosed;

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            if (ConnectionClosed)
                throw new IOException("The connection is gone.");

            return stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("paid content")).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 12;
            return true;
        }
    }

    private sealed class OkHandler : HttpMessageHandler
    {
        public readonly ClosableContent Content = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = Content });
    }

    [Fact(Timeout = 60000)]
    public async Task Given_AServerAnswering200_When_TheAutomaticClientReturnsIt_Then_TheContentIsBufferedAndReadableAfterTheConnectionIsGone()
    {
        var handler = new OkHandler();
        var options = new X402HttpClientOptions { MaxAmount = "1000000", PreferredNetwork = AuditServer.Network };
        var client = new X402HttpClient(new HttpClient(handler), "0x" + new string('1', 63) + "2", options);

        using var response = await client.GetAsync("http://resource.local/api/real");
        handler.Content.ConnectionClosed = true;

        Assert.Equal("paid content", await response.Content.ReadAsStringAsync());
    }
}
