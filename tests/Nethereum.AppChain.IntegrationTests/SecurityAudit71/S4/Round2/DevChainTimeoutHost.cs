using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.DevChain;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round2
{
    internal sealed class DevChainTimeoutHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly Task _run;

        private DevChainTimeoutHost(WebApplication app, Task run, Uri url)
        {
            _app = app;
            _run = run;
            Url = url;
            Http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        }

        public Uri Url { get; }

        public HttpClient Http { get; }

        public DevChainNode Node => _app.Services.GetRequiredService<DevChainNode>();

        public static async Task<DevChainTimeoutHost> StartAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            var config = new DevChainServerConfig { Port = port, Host = "127.0.0.1", Storage = "memory" };
            var builder = WebApplication.CreateBuilder();
            builder.AddDevChainServer(config);
            builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 10 * 1024 * 1024);
            builder.Logging.ClearProviders();

            var app = builder.Build();
            await app.MapDevChainEndpointsAsync();

            var url = new Uri($"http://127.0.0.1:{port}");
            var run = Task.Run(async () =>
            {
                try { await app.RunAsync(url.ToString()); }
                catch (OperationCanceledException) { }
            });

            var host = new DevChainTimeoutHost(app, run, url);
            await host.WaitUntilReadyAsync();
            return host;
        }

        public async Task<(HttpStatusCode Status, string Body)> PostAsync(byte[] body)
        {
            using var content = new ByteArrayContent(body);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            using var response = await Http.PostAsync(Url, content);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        public Task<(HttpStatusCode Status, string Body)> PostAsync(string json) => PostAsync(Encoding.UTF8.GetBytes(json));

        public async Task<string> CallAsync(string method, string paramsJson)
        {
            var result = await PostAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"" + method + "\",\"params\":" + paramsJson + "}");
            return result.Body;
        }

        private async Task WaitUntilReadyAsync()
        {
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var probe = await PostAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}");
                    if (probe.Status == HttpStatusCode.OK) return;
                }
                catch (HttpRequestException) { }
                await Task.Delay(100);
            }
            throw new TimeoutException("DevChain host did not start");
        }

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            try { await _app.StopAsync(); } catch (ObjectDisposedException) { }
            try { await _run; } catch (OperationCanceledException) { }
            try { await _app.DisposeAsync(); } catch (ObjectDisposedException) { }
        }
    }
}
