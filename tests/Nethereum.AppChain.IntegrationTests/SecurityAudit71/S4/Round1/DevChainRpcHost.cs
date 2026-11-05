using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1
{
    internal sealed class DevChainRpcHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly Task _run;

        private DevChainRpcHost(WebApplication app, Task run, Uri url)
        {
            _app = app;
            _run = run;
            Url = url;
            Http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        }

        public Uri Url { get; }

        public HttpClient Http { get; }

        public static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public static async Task<DevChainRpcHost> StartAsync(
            Action<DevChainServerConfig>? configure = null, ILoggerProvider? logProvider = null)
        {
            var config = new DevChainServerConfig { Port = FreePort(), Host = "127.0.0.1", Storage = "memory" };
            configure?.Invoke(config);

            var builder = WebApplication.CreateBuilder();
            builder.AddDevChainServer(config);
            builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 10 * 1024 * 1024);
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.Logging.AddFilter("Nethereum", logProvider != null ? LogLevel.Debug : LogLevel.Information);
            if (logProvider != null) builder.Logging.AddProvider(logProvider);

            var app = builder.Build();
            await app.MapDevChainEndpointsAsync();

            var url = new Uri($"http://127.0.0.1:{config.Port}");
            var run = Task.Run(async () =>
            {
                try { await app.RunAsync(url.ToString()); }
                catch (OperationCanceledException) { }
            });

            var host = new DevChainRpcHost(app, run, url);
            await host.WaitUntilReadyAsync();
            return host;
        }

        public async Task<(HttpStatusCode Status, string Body)> PostAsync(string json, string contentType = "application/json")
        {
            using var response = await Http.PostAsync(Url, new StringContent(json, Encoding.UTF8, contentType));
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

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
