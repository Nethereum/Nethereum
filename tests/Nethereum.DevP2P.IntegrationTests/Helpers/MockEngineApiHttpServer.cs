using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    /// <summary>
    /// Loopback HTTP server that returns <c>200 OK {}</c> to every POST.
    /// Lets go-ethereum's <c>cmd/devp2p rlpx eth-test</c> tool satisfy its
    /// <c>s.engine.sendForkchoiceUpdated()</c> call so it'll proceed to the
    /// transaction-pool sub-tests. The tool only checks that <c>http.Do()</c>
    /// returned no error; it discards the response body.
    /// <para>
    /// Binds on an ephemeral loopback port — read <see cref="Port"/> after
    /// <see cref="Start"/>. Disposing stops the listener.
    /// </para>
    /// </summary>
    public sealed class MockEngineApiHttpServer : IDisposable
    {
        private const int ReadBufferSize = 8192;
        private const string HttpHeaderTerminator = "\r\n\r\n";
        private const string ContentLengthHeader = "Content-Length:";
        private const string FixedResponse =
            "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n{}";

        private static readonly byte[] FixedResponseBytes = Encoding.ASCII.GetBytes(FixedResponse);

        private readonly TcpListener _tcp;
        private readonly CancellationTokenSource _cts;
        private Task _acceptLoop;

        /// <summary>Bound loopback port; valid only after <see cref="Start"/>.</summary>
        public int Port { get; }

        private MockEngineApiHttpServer(TcpListener tcp, int port)
        {
            _tcp = tcp;
            _cts = new CancellationTokenSource();
            Port = port;
        }

        /// <summary>Bind on a free loopback port and begin accepting connections.</summary>
        public static MockEngineApiHttpServer Start()
        {
            var tcp = new TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            var server = new MockEngineApiHttpServer(tcp, port);
            server._acceptLoop = Task.Run(() => server.AcceptLoopAsync(server._cts.Token));
            return server;
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await _tcp.AcceptTcpClientAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }

                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
        }

        private static async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    await ReadRequestAsync(stream, ct).ConfigureAwait(false);
                    await stream.WriteAsync(FixedResponseBytes, 0, FixedResponseBytes.Length, ct).ConfigureAwait(false);
                }
            }
            catch (IOException) { /* peer closed */ }
            catch (OperationCanceledException) { /* server stopping */ }
        }

        private static async Task ReadRequestAsync(NetworkStream stream, CancellationToken ct)
        {
            var buf = new byte[ReadBufferSize];
            int total = 0;
            int contentLength = 0;
            while (true)
            {
                int n = await stream.ReadAsync(buf, total, buf.Length - total, ct).ConfigureAwait(false);
                if (n == 0) return;
                total += n;

                var headerSection = Encoding.ASCII.GetString(buf, 0, total);
                var headerEnd = headerSection.IndexOf(HttpHeaderTerminator, StringComparison.Ordinal);
                if (headerEnd < 0) continue;

                foreach (var line in headerSection.Substring(0, headerEnd).Split('\n'))
                {
                    if (line.StartsWith(ContentLengthHeader, StringComparison.OrdinalIgnoreCase))
                        contentLength = int.Parse(line.Substring(ContentLengthHeader.Length).Trim());
                }

                int bodyRead = total - (headerEnd + HttpHeaderTerminator.Length);
                while (bodyRead < contentLength)
                {
                    int more = await stream.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false);
                    if (more == 0) return;
                    bodyRead += more;
                }
                return;
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _tcp.Stop();
        }
    }
}
