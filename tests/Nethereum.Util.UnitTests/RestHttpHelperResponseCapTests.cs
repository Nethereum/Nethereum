using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Nethereum.Util.Rest;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class RestHttpHelperResponseCapTests
    {
        private sealed class EndlessBodyServer : IDisposable
        {
            private readonly HttpListener _listener = new HttpListener();
            private readonly CancellationTokenSource _stop = new CancellationTokenSource();
            private readonly int _status;
            public string Url { get; }

            public EndlessBodyServer(int status = 200)
            {
                _status = status;
                var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                var port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
                Url = "http://127.0.0.1:" + port + "/";
                _listener.Prefixes.Add(Url);
                _listener.Start();
                _ = Task.Run(Loop);
            }

            private async Task Loop()
            {
                while (!_stop.IsCancellationRequested)
                {
                    HttpListenerContext context;
                    try { context = await _listener.GetContextAsync(); }
                    catch { return; }
                    _ = Task.Run(() => Serve(context));
                }
            }

            private async Task Serve(HttpListenerContext context)
            {
                try
                {
                    context.Response.StatusCode = _status;
                    context.Response.ContentType = "application/json";
                    context.Response.SendChunked = true;
                    var chunk = new byte[8192];
                    Array.Fill(chunk, (byte)'a');
                    chunk[0] = (byte)'"';
                    var stream = context.Response.OutputStream;
                    while (!_stop.IsCancellationRequested)
                    {
                        await stream.WriteAsync(chunk, 0, chunk.Length);
                        await stream.FlushAsync();
                    }
                }
                catch
                {
                }
                finally
                {
                    try { context.Response.Abort(); } catch { }
                }
            }

            public void Dispose()
            {
                _stop.Cancel();
                try { _listener.Abort(); } catch { }
            }
        }

        private sealed class FixedBodyServer : IDisposable
        {
            private readonly HttpListener _listener = new HttpListener();
            public string Url { get; }

            public FixedBodyServer(int status, string contentType, byte[] body)
            {
                var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                var port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
                Url = "http://127.0.0.1:" + port + "/";
                _listener.Prefixes.Add(Url);
                _listener.Start();
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        HttpListenerContext context;
                        try { context = await _listener.GetContextAsync(); }
                        catch { return; }
                        context.Response.StatusCode = status;
                        context.Response.ContentType = contentType;
                        context.Response.ContentLength64 = body.Length;
                        await context.Response.OutputStream.WriteAsync(body, 0, body.Length);
                        context.Response.Close();
                    }
                });
            }

            public void Dispose()
            {
                try { _listener.Abort(); } catch { }
            }
        }

        private static async Task AssertCapped(Func<RestHttpHelper, string, Task> call)
        {
            using var server = new EndlessBodyServer();
            var helper = new RestHttpHelper(new HttpClient { Timeout = TimeSpan.FromSeconds(20) }) { MaxResponseBytes = 64 * 1024 };
            await Assert.ThrowsAsync<HttpRequestException>(() => call(helper, server.Url));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "util-rest-response-cap", "GetAsync stops reading an endless body at MaxResponseBytes")]
        public Task Given_AnEndlessBody_When_GetAsync_Then_TheCapIsEnforced()
            => AssertCapped((h, url) => h.GetAsync<string>(url));

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "util-rest-response-cap", "PostAsync stops reading an endless body at MaxResponseBytes")]
        public Task Given_AnEndlessBody_When_PostAsync_Then_TheCapIsEnforced()
            => AssertCapped((h, url) => h.PostAsync<string, string>(url, "x"));

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "util-rest-response-cap", "PutAsync stops reading an endless body at MaxResponseBytes")]
        public Task Given_AnEndlessBody_When_PutAsync_Then_TheCapIsEnforced()
            => AssertCapped((h, url) => h.PutAsync<string, string>(url, "x"));

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "util-rest-response-cap", "DeleteAsync stops reading an endless body at MaxResponseBytes")]
        public Task Given_AnEndlessBody_When_DeleteAsync_Then_TheCapIsEnforced()
            => AssertCapped((h, url) => h.DeleteAsync(url));

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "util-rest-response-cap", "PostMultipartAsync stops reading an endless body at MaxResponseBytes")]
        public Task Given_AnEndlessBody_When_PostMultipartAsync_Then_TheCapIsEnforced()
            => AssertCapped((h, url) => h.PostMultipartAsync<string>(url, new MultipartFormDataRequest
            {
                Fields = new List<MultipartField> { new MultipartField { Name = "a", Value = "b" } }
            }));

        [Fact]
        public async Task Given_A4xxResponse_When_GetAsync_Then_TheMessageIncludesTheResponseHeaders()
        {
            using var server = new FixedBodyServer(404, "text/plain", System.Text.Encoding.UTF8.GetBytes("missing"));
            var helper = new RestHttpHelper(new HttpClient());
            var ex = await Assert.ThrowsAsync<Exception>(() => helper.GetAsync<string>(server.Url));
            Assert.StartsWith("Error: NotFound, missing, ", ex.Message);
            Assert.Contains("Date:", ex.Message);
        }

        [Fact]
        public async Task Given_A4xxResponse_When_PostAsync_Then_TheMessageHasNoHeaders()
        {
            using var server = new FixedBodyServer(404, "text/plain", System.Text.Encoding.UTF8.GetBytes("missing"));
            var helper = new RestHttpHelper(new HttpClient());
            var ex = await Assert.ThrowsAsync<Exception>(() => helper.PostAsync<string, string>(server.Url, "x"));
            Assert.Equal("Error: NotFound, missing", ex.Message);
        }

        [Theory]
        [InlineData("iso-8859-1")]
        [InlineData("utf-16")]
        public async Task Given_ADeclaredCharset_When_GetAsync_Then_TheBodyIsDecodedWithIt(string charset)
        {
            var body = System.Text.Encoding.GetEncoding(charset).GetBytes("\"café\"");
            using var server = new FixedBodyServer(200, "application/json; charset=" + charset, body);
            var helper = new RestHttpHelper(new HttpClient());
            Assert.Equal("café", await helper.GetAsync<string>(server.Url));
        }

        [Fact]
        public async Task Given_AnEndlessErrorBody_When_GetAsync_Then_TheCapIsEnforced()
        {
            using var server = new EndlessBodyServer(500);
            var helper = new RestHttpHelper(new HttpClient { Timeout = TimeSpan.FromSeconds(20) }) { MaxResponseBytes = 64 * 1024 };
            await Assert.ThrowsAsync<HttpRequestException>(() => helper.GetAsync<string>(server.Url));
        }
    }
}
