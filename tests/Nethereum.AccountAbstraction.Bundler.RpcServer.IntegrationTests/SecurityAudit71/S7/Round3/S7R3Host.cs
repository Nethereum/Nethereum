using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.SecurityAudit71.S7.Round3
{
    public sealed class S7R3Host : IDisposable
    {
        public const string EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";
        private const string Beneficiary = "0x1111111111111111111111111111111111111111";

        private readonly HttpListener _fakeNode = new();
        private readonly CancellationTokenSource _stop = new();
        private readonly Process _server;
        private int _nodeCalls;

        public HttpClient Http { get; }

        public int NodeCalls => Volatile.Read(ref _nodeCalls);

        public long PeakWorkingSetBytes
        {
            get
            {
                _server.Refresh();
                return _server.PeakWorkingSet64;
            }
        }

        public S7R3Host(string extraArguments = "", string nodePath = "")
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
                Arguments = $"\"{assembly}\" --rpc=http://127.0.0.1:{nodePort}{nodePath} --beneficiary={Beneficiary} --entryPoint={EntryPoint} " +
                            $"--host=127.0.0.1 --port={serverPort} --requireSigner=false {extraArguments}",
                WorkingDirectory = AppContext.BaseDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            })!;
            _server.OutputDataReceived += (_, _) => { };
            _server.ErrorDataReceived += (_, _) => { };
            _server.BeginOutputReadLine();
            _server.BeginErrorReadLine();

            Http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{serverPort}"), Timeout = TimeSpan.FromSeconds(60) };
            WaitUntilHealthy();
        }

        public void Dispose()
        {
            Http.Dispose();
            try
            {
                if (!_server.HasExited) _server.Kill(entireProcessTree: true);
                _server.WaitForExit(10_000);
            }
            catch (InvalidOperationException)
            {
            }
            _server.Dispose();
            StopNode();
        }

        public void StopNode()
        {
            _stop.Cancel();
            _fakeNode.Close();
        }

        public async Task<(HttpStatusCode Status, string Body)> PostAsync(string body)
        {
            var response = await Http.PostAsync("/", new StringContent(body, Encoding.UTF8, "application/json"));
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
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
                Interlocked.Increment(ref _nodeCalls);
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
                    if (Http.GetAsync("/health").GetAwaiter().GetResult().IsSuccessStatusCode) return;
                }
                catch (HttpRequestException)
                {
                }
                Thread.Sleep(250);
            }
            throw new TimeoutException("bundler host did not become healthy");
        }
    }
}
