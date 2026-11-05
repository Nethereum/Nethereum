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

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class AppChainRpcBatchTests : IAsyncLifetime
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
                ChainId = new BigInteger(420421_000 + Environment.TickCount % 1000),
                ChainName = "RpcBatchTest",
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
        public async Task Given_ABatchOfTwoRequests_When_ItIsPosted_Then_AnArrayOfTwoResponsesComesBackById()
        {
            var reply = await Post(
                "[{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}," +
                "{\"jsonrpc\":\"2.0\",\"id\":\"b\",\"method\":\"eth_blockNumber\",\"params\":[]}]");

            Assert.Equal(HttpStatusCode.OK, reply.Status);
            using var document = JsonDocument.Parse(reply.Body);
            Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
            Assert.Equal(2, document.RootElement.GetArrayLength());
            Assert.Equal(1, document.RootElement[0].GetProperty("id").GetInt32());
            Assert.True(document.RootElement[0].TryGetProperty("result", out _));
            Assert.Equal("b", document.RootElement[1].GetProperty("id").GetString());
            Assert.True(document.RootElement[1].TryGetProperty("result", out _));
        }

        [Fact]
        public async Task Given_ABatchWithANotification_When_ItIsPosted_Then_TheNotificationHasNoEntry()
        {
            var reply = await Post(
                "[{\"jsonrpc\":\"2.0\",\"method\":\"eth_blockNumber\",\"params\":[]}," +
                "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"eth_chainId\",\"params\":[]}]");

            Assert.Equal(HttpStatusCode.OK, reply.Status);
            using var document = JsonDocument.Parse(reply.Body);
            Assert.Equal(1, document.RootElement.GetArrayLength());
            Assert.Equal(7, document.RootElement[0].GetProperty("id").GetInt32());
        }

        [Fact]
        public async Task Given_ABatchOfOnlyNotifications_When_ItIsPosted_Then_NothingIsReturned()
        {
            var reply = await Post("[{\"jsonrpc\":\"2.0\",\"method\":\"eth_blockNumber\",\"params\":[]}]");

            Assert.Equal(HttpStatusCode.NoContent, reply.Status);
            Assert.Equal(string.Empty, reply.Body);
        }

        [Fact]
        public async Task Given_ABatchWithAnInvalidMember_When_ItIsPosted_Then_ThatEntryIsInvalidRequestAndTheOthersAnswer()
        {
            var reply = await Post("[1,{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"eth_chainId\",\"params\":[]}]");

            Assert.Equal(HttpStatusCode.OK, reply.Status);
            using var document = JsonDocument.Parse(reply.Body);
            Assert.Equal(2, document.RootElement.GetArrayLength());
            Assert.Equal(-32600, document.RootElement[0].GetProperty("error").GetProperty("code").GetInt32());
            Assert.Equal(JsonValueKind.Null, document.RootElement[0].GetProperty("id").ValueKind);
            Assert.True(document.RootElement[1].TryGetProperty("result", out _));
        }

        [Fact]
        public async Task Given_AnEmptyBatch_When_ItIsPosted_Then_ItIsAnInvalidRequestError()
        {
            var reply = await Post("[]");

            Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
            using var document = JsonDocument.Parse(reply.Body);
            Assert.Equal(-32600, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        }
    }
}
