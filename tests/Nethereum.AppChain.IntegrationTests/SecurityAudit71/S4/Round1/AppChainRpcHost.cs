using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.Signer;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1
{
    public sealed class AppChainRpcHost : IAsyncDisposable
    {
        private readonly CancellationTokenSource _shutdown;
        private readonly Task _server;
        private readonly bool _signRecoverableBeforeStart;

        private AppChainRpcHost(CancellationTokenSource shutdown, Task server, Uri url, bool signRecoverableBeforeStart)
        {
            _shutdown = shutdown;
            _server = server;
            Url = url;
            _signRecoverableBeforeStart = signRecoverableBeforeStart;
            Http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public Uri Url { get; }

        public Uri WebSocketUrl => new Uri($"ws://{Url.Host}:{Url.Port}/ws");

        public HttpClient Http { get; }

        public static async Task<AppChainRpcHost> StartAsync(Action<AppChainServerConfig>? configure = null)
        {
            var signRecoverable = EthECKey.SignRecoverable;

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            var config = new AppChainServerConfig
            {
                ChainId = new BigInteger(420500_000 + Environment.TickCount % 1000),
                ChainName = "S4Round1",
                Node = new ChainNodeConfig
                {
                    Rpc = new ChainNodeRpcConfig { Port = port },
                    Storage = new ChainNodeStorageConfig { InMemory = true },
                    Network = new ChainNodeNetworkConfig
                    {
                        Serve = false,
                        Discovery = new ChainNodeDiscoveryConfig { DisableDiscv4 = true, DisableDiscv5 = true },
                    },
                    Sync = new ChainNodeSyncConfig(),
                    Mempool = new ChainNodeMempoolConfig { EnableTrustedPeerAdmission = true },
                },
                Mud = new AppChainMudConfig { DeployWorld = false },
            };

            configure?.Invoke(config);

            var shutdown = new CancellationTokenSource();
            var host = new AppChainRpcHost(
                shutdown, AppChainServerRunner.RunAsync(config, shutdown.Token), new Uri(config.RpcUrl), signRecoverable);
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
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (DateTime.UtcNow < deadline)
            {
                if (_server.IsFaulted) await _server;
                try
                {
                    var probe = await PostAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}");
                    if (probe.Status == HttpStatusCode.OK) return;
                }
                catch (HttpRequestException) { }
                await Task.Delay(250);
            }
            throw new TimeoutException("AppChain server did not start");
        }

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await DisposeServerAsync();
            EthECKey.SignRecoverable = _signRecoverableBeforeStart;
        }

        private async Task DisposeServerAsync()
        {
            _shutdown.Cancel();
            try { await _server.WaitAsync(TimeSpan.FromSeconds(30)); }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
        }
    }
}
