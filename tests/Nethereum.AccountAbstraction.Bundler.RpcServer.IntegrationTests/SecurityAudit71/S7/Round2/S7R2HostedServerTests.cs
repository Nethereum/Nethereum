using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round2
{
    public class S7R2HostedServerTests : IDisposable
    {
        private const string EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";
        private const string Beneficiary = "0x1111111111111111111111111111111111111111";

        private readonly HttpListener _fakeNode = new();
        private readonly CancellationTokenSource _stop = new();
        private readonly Process _server;
        private readonly HttpClient _http;

        public S7R2HostedServerTests()
        {
            var nodePort = FreePort();
            var serverPort = FreePort();
            _fakeNode.Prefixes.Add($"http://127.0.0.1:{nodePort}/");
            _fakeNode.Start();
            _ = Task.Run(() => ServeFakeNodeAsync(_stop.Token));

            var assembly = Path.Combine(AppContext.BaseDirectory, "Nethereum.AccountAbstraction.Bundler.RpcServer.dll");
            _server = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{assembly}\" --rpc=http://127.0.0.1:{nodePort} --beneficiary={Beneficiary} --entryPoint={EntryPoint} " +
                            $"--host=127.0.0.1 --port={serverPort} --requireSigner=false",
                WorkingDirectory = AppContext.BaseDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            })!;
            _server.OutputDataReceived += (_, _) => { };
            _server.ErrorDataReceived += (_, _) => { };
            _server.BeginOutputReadLine();
            _server.BeginErrorReadLine();

            _http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{serverPort}"), Timeout = TimeSpan.FromSeconds(60) };
            WaitUntilHealthy();
        }

        public void Dispose()
        {
            _http.Dispose();
            try
            {
                if (!_server.HasExited) _server.Kill(entireProcessTree: true);
                _server.WaitForExit(10_000);
            }
            catch (InvalidOperationException)
            {
            }
            _server.Dispose();
            _stop.Cancel();
            _fakeNode.Close();
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private async Task ServeFakeNodeAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _fakeNode.GetContextAsync();
                }
                catch (Exception)
                {
                    return;
                }
                using var reader = new StreamReader(context.Request.InputStream);
                var body = await reader.ReadToEndAsync();
                var id = JsonDocument.Parse(body).RootElement.GetProperty("id").GetRawText();
                var reply = Encoding.UTF8.GetBytes($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":\"0x539\"}}");
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(reply);
                context.Response.Close();
            }
        }

        private void WaitUntilHealthy()
        {
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (DateTime.UtcNow < deadline)
            {
                if (_server.HasExited) throw new InvalidOperationException("bundler host exited with " + _server.ExitCode);
                try
                {
                    if (_http.GetAsync("/health").GetAwaiter().GetResult().IsSuccessStatusCode) return;
                }
                catch (HttpRequestException)
                {
                }
                Thread.Sleep(250);
            }
            throw new TimeoutException("bundler host did not become healthy");
        }

        private Task<HttpResponseMessage> Post(string body) =>
            _http.PostAsync("/", new StringContent(body, Encoding.UTF8, "application/json"));

        [Fact]
        [Trait("Rule", "S7-R2-13")]
        [Trait("Erc", "JSON-RPC batch")]
        public async Task Post_BatchWithANullMember_IsAnsweredWithAnInvalidRequestErrorNotAnInternalError()
        {
            var response = await Post("[null]");
            var text = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("-32603", text);
            Assert.Contains("-32600", text);
        }

        [Fact]
        [Trait("Rule", "S7-R2-14")]
        [Trait("Erc", "JSON-RPC batch")]
        public async Task Post_BatchAboveTheBatchLimit_IsAnsweredWithAOneElementArrayHoldingABatchTooLargeErrorWithTheIdOfTheFirstCall()
        {
            var item = "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"eth_chainId\",\"params\":[]}";
            var body = "[" + string.Join(",", Enumerable.Repeat(item, 1001)) + "]";

            var response = await Post(body);
            var text = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(text);
            Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
            Assert.Equal(1, document.RootElement.GetArrayLength());
            var member = document.RootElement[0];
            Assert.Equal(-32600, member.GetProperty("error").GetProperty("code").GetInt32());
            Assert.Equal("batch too large", member.GetProperty("error").GetProperty("message").GetString());
            Assert.Equal(7, member.GetProperty("id").GetInt32());
        }

        [Fact]
        [Trait("Rule", "S7-2")]
        [Trait("Erc", "JSON-RPC input limits")]
        public async Task Post_BodyAboveMaxRequestBodyBytesWithContentLength_IsAnswered413()
        {
            var response = await Post(new string(' ', 6 * 1024 * 1024));

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        }

        [Fact]
        [Trait("Rule", "S7-2")]
        [Trait("Erc", "JSON-RPC input limits")]
        public async Task Post_ChunkedBodyAboveMaxRequestBodyBytes_IsAnswered413()
        {
            var stream = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(new string(' ', 6 * 1024 * 1024))));
            stream.Headers.ContentLength = null;
            var request = new HttpRequestMessage(HttpMethod.Post, "/") { Content = stream };
            request.Headers.TransferEncodingChunked = true;

            var response = await _http.SendAsync(request);

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        }
    }
}
