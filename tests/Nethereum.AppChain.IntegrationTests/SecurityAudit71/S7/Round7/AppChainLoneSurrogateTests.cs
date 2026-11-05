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

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S7.Round7
{
    [Collection("Sequential")]
    public class AppChainLoneSurrogateTests : IAsyncLifetime
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
                ChainName = "LoneSurrogateTest",
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

        [Theory]
        [InlineData("eth_getBalance", "[\"\\ud800\",\"latest\"]")]
        [InlineData("eth_getBalance", "[\"0x0000000000000000000000000000000000000001\",\"\\ud800\"]")]
        [InlineData("eth_getTransactionByHash", "[\"\\ud800\"]")]
        [InlineData("eth_getBalance", "[\"\ud800\",\"latest\"]")]
        [InlineData("eth_getBalance", "[\"0x0000000000000000000000000000000000000001\",\"\ud800\"]")]
        [InlineData("eth_getTransactionByHash", "[\"\ud800\"]")]
        [Trait("Rule", "S7-R7-08")]
        [Trait("Erc", "JSON-RPC input limits")]
        public async Task Given_AStringParameterWithALoneSurrogateEscape_When_ARealMethodIsCalled_Then_ItIsInvalidParams(string method, string parameters)
        {
            var (_, body) = await Post("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"" + method + "\",\"params\":" + parameters + "}");

            using var document = JsonDocument.Parse(body);
            Assert.Equal(-32602, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        }
    }
}
