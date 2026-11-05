using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S7.Round5
{
    [Collection("Sequential")]
    public class AppChainRpcBodyLimitTests : IAsyncLifetime
    {
        private readonly CancellationTokenSource _shutdown = new();
        private readonly HttpClient _http = new();
        private Task _server;
        private Uri _rpcUrl;
        private bool _signRecoverableBeforeStart;

        public async Task InitializeAsync()
        {
            _signRecoverableBeforeStart = EthECKey.SignRecoverable;

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            var config = new AppChainServerConfig
            {
                ChainId = new BigInteger(420422_000 + Environment.TickCount % 1000),
                ChainName = "RpcBodyLimitTest",
                Node = new ChainNodeConfig
                {
                    Rpc = new ChainNodeRpcConfig { Port = port },
                    Storage = new ChainNodeStorageConfig { InMemory = true },
                    Network = new ChainNodeNetworkConfig
                    {
                        Serve = false,
                        Discovery = new ChainNodeDiscoveryConfig { DisableDiscv4 = true, DisableDiscv5 = true },
                    },
                    Sync = new ChainNodeSyncConfig { Mode = SyncMode.None },
                    Mempool = new ChainNodeMempoolConfig { EnableTrustedPeerAdmission = true },
                },
                Mud = new AppChainMudConfig { DeployWorld = false },
            };
            _rpcUrl = new Uri(config.RpcUrl);
            _server = AppChainServerRunner.RunAsync(config, _shutdown.Token);

            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                if (_server.IsFaulted) await _server;
                try
                {
                    var probe = await Post("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}");
                    if (probe.Status == HttpStatusCode.OK) return;
                }
                catch (HttpRequestException) { }
                await Task.Delay(250);
            }
            throw new TimeoutException("AppChain server did not start");
        }

        public async Task DisposeAsync()
        {
            _shutdown.Cancel();
            try { await _server.WaitAsync(TimeSpan.FromSeconds(30)); }
            catch (OperationCanceledException) { }
            _http.Dispose();
            EthECKey.SignRecoverable = _signRecoverableBeforeStart;
        }

        private async Task<(HttpStatusCode Status, string Body)> Post(string json)
        {
            using var response = await _http.PostAsync(_rpcUrl, new StringContent(json, Encoding.UTF8, "application/json"));
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        [Trait("Rule", "S7-R5-08")]
        [Trait("Erc", "JSON-RPC input limits")]
        public async Task Given_ARequestBodyAboveTheNodeHttpLimit_When_ItIsPosted_Then_ItIsRefusedWith413()
        {
            var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[\"" + new string('a', 12 * 1024 * 1024) + "\"]}";

            HttpStatusCode status;
            try
            {
                status = (await Post(body)).Status;
            }
            catch (HttpRequestException)
            {
                status = HttpStatusCode.RequestEntityTooLarge;
            }

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, status);
        }
    }
}
