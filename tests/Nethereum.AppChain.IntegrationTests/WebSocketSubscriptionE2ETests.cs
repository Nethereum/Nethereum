using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Sequencer;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Subscriptions;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

using AppChainCore = Nethereum.AppChain.AppChain;

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class WebSocketSubscriptionE2ETests : IAsyncLifetime
    {
        private const string SequencerPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string UserPrivateKey = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
        private const int ChainId = 420420;

        private string _sequencerAddress = null!;
        private string _userAddress = null!;

        private AppChainCore _appChain = null!;
        private Sequencer.Sequencer _sequencer = null!;
        private WebSocketRpcHandler _wsHandler = null!;
        private WebApplication _app = null!;
        private int _port;
        private string _wsUrl = null!;
        private ILogStore _logStore = null!;

        private string _databasePath = "";
        private RocksDbManager? _dbManager;

        public async Task InitializeAsync()
        {
            _sequencerAddress = new EthECKey(SequencerPrivateKey).GetPublicAddress();
            _userAddress = new EthECKey(UserPrivateKey).GetPublicAddress();

            _databasePath = Path.Combine(Path.GetTempPath(), $"ws_e2e_{Guid.NewGuid():N}");
            var options = new RocksDbStorageOptions { DatabasePath = _databasePath };
            _dbManager = new RocksDbManager(options);

            var blockStore = new RocksDbBlockStore(_dbManager);
            var transactionStore = new RocksDbTransactionStore(_dbManager, blockStore);
            var receiptStore = new RocksDbReceiptStore(_dbManager, blockStore);
            _logStore = new RocksDbLogStore(_dbManager);
            var stateStore = new RocksDbStateStore(_dbManager);

            var appChainConfig = AppChainConfig.CreateWithName("WsTestChain", ChainId);
            appChainConfig.SequencerAddress = _sequencerAddress;

            _appChain = new AppChainCore(appChainConfig, blockStore, transactionStore, receiptStore, _logStore, stateStore);

            var genesisOptions = new GenesisOptions
            {
                PrefundedAddresses = new[] { _sequencerAddress, _userAddress },
                PrefundBalance = BigInteger.Parse("1000000000000000000000"),
                DeployCreate2Factory = true
            };
            await _appChain.InitializeAsync(genesisOptions);

            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                SequencerPrivateKey = SequencerPrivateKey,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                Policy = PolicyConfig.OpenAccess
            };
            _sequencer = new Sequencer.Sequencer(_appChain, sequencerConfig);
            await _sequencer.StartAsync();

            var node = new AppChainNode(_appChain, _sequencer);

            _port = FindFreePort();
            var builder = WebApplication.CreateBuilder();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            _app = builder.Build();
            _app.UseWebSockets();

            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var rpcRegistry = new RpcHandlerRegistry();
            rpcRegistry.AddStandardHandlers();
            var services = new ServiceCollection().BuildServiceProvider();
            var rpcContext = new RpcContext(node, ChainId, services);
            rpcContext.TxPool = _sequencer.TxPool;

            var subscriptionManager = new SubscriptionManager();
            _wsHandler = new WebSocketRpcHandler(subscriptionManager, rpcRegistry, rpcContext, serializerOptions);

            _sequencer.BlockProduced += async (sender, result) =>
            {
                var blockLogs = await _logStore.GetLogsByBlockNumberAsync(result.Header.BlockNumber);
                await _wsHandler.BroadcastBlockAsync(result.Header, result.BlockHash, blockLogs);
            };

            _app.Map("/ws", async (HttpContext httpContext) =>
            {
                if (!httpContext.WebSockets.IsWebSocketRequest)
                {
                    httpContext.Response.StatusCode = 400;
                    return;
                }
                var ws = await httpContext.WebSockets.AcceptWebSocketAsync();
                await _wsHandler.HandleConnectionAsync(ws);
            });

            _wsUrl = $"ws://127.0.0.1:{_port}/ws";
            _ = _app.RunAsync($"http://127.0.0.1:{_port}");
            await Task.Delay(500);
        }

        public async Task DisposeAsync()
        {
            try { await _app.StopAsync(); } catch { }
            await _sequencer.StopAsync();
            _dbManager?.Dispose();
            if (Directory.Exists(_databasePath))
                try { Directory.Delete(_databasePath, true); } catch { }
        }

        [Fact]
        public async Task WebSocket_Connect_And_Disconnect()
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);

            Assert.Equal(WebSocketState.Open, ws.State);

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }

        [Fact]
        public async Task WebSocket_EthSubscribe_NewHeads_ReturnsSubscriptionId()
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);

            var subscribeRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "eth_subscribe",
                @params = new[] { "newHeads" }
            });

            await SendAsync(ws, subscribeRequest);
            var response = await ReceiveAsync(ws);

            var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            Assert.Equal(1, root.GetProperty("id").GetInt32());
            Assert.True(root.TryGetProperty("result", out var result));
            Assert.StartsWith("0x", result.GetString());
            Assert.False(root.TryGetProperty("error", out _));

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }

        [Fact]
        public async Task WebSocket_NewHeads_ReceivesBlockNotification()
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);

            var subscribeRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "eth_subscribe",
                @params = new[] { "newHeads" }
            });
            await SendAsync(ws, subscribeRequest);
            var subResponse = await ReceiveAsync(ws);
            var subId = JsonDocument.Parse(subResponse).RootElement.GetProperty("result").GetString();

            await _sequencer.ProduceBlockAsync();
            await Task.Delay(200);

            var notification = await ReceiveAsync(ws, timeoutMs: 3000);

            var doc = JsonDocument.Parse(notification);
            var root = doc.RootElement;

            Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
            Assert.Equal("eth_subscription", root.GetProperty("method").GetString());

            var notifParams = root.GetProperty("params");
            Assert.Equal(subId, notifParams.GetProperty("subscription").GetString());

            var blockResult = notifParams.GetProperty("result");
            Assert.True(blockResult.TryGetProperty("number", out var numberProp));
            Assert.Equal("0x1", numberProp.GetString());

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }

        [Fact]
        public async Task WebSocket_NewHeads_ReceivesMultipleBlocks()
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);

            await SendAsync(ws, JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = 1,
                method = "eth_subscribe",
                @params = new[] { "newHeads" }
            }));
            await ReceiveAsync(ws);

            await _sequencer.ProduceBlockAsync();
            await _sequencer.ProduceBlockAsync();
            await _sequencer.ProduceBlockAsync();
            await Task.Delay(300);

            var block1 = JsonDocument.Parse(await ReceiveAsync(ws, timeoutMs: 3000));
            var block2 = JsonDocument.Parse(await ReceiveAsync(ws, timeoutMs: 3000));
            var block3 = JsonDocument.Parse(await ReceiveAsync(ws, timeoutMs: 3000));

            Assert.Equal("0x1", block1.RootElement.GetProperty("params").GetProperty("result").GetProperty("number").GetString());
            Assert.Equal("0x2", block2.RootElement.GetProperty("params").GetProperty("result").GetProperty("number").GetString());
            Assert.Equal("0x3", block3.RootElement.GetProperty("params").GetProperty("result").GetProperty("number").GetString());

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }

        [Fact]
        public async Task WebSocket_EthUnsubscribe_StopsNotifications()
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);

            await SendAsync(ws, JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = 1,
                method = "eth_subscribe",
                @params = new[] { "newHeads" }
            }));
            var subResponse = JsonDocument.Parse(await ReceiveAsync(ws));
            var subId = subResponse.RootElement.GetProperty("result").GetString();

            await SendAsync(ws, JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = 2,
                method = "eth_unsubscribe",
                @params = new[] { subId }
            }));
            var unsubResponse = JsonDocument.Parse(await ReceiveAsync(ws));
            Assert.True(unsubResponse.RootElement.GetProperty("result").GetBoolean());

            await Task.Delay(100);

            await _sequencer.ProduceBlockAsync();
            await Task.Delay(500);

            var received = await TryReceiveAsync(ws, timeoutMs: 2000);
            Assert.Null(received);

            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }

        [Fact]
        public async Task WebSocket_MultipleSubscribers_BothReceive()
        {
            using var ws1 = new ClientWebSocket();
            using var ws2 = new ClientWebSocket();
            await ws1.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);
            await ws2.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);

            await SendAsync(ws1, JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = 1,
                method = "eth_subscribe",
                @params = new[] { "newHeads" }
            }));
            await ReceiveAsync(ws1);

            await SendAsync(ws2, JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = 1,
                method = "eth_subscribe",
                @params = new[] { "newHeads" }
            }));
            await ReceiveAsync(ws2);

            await _sequencer.ProduceBlockAsync();
            await Task.Delay(300);

            var notif1 = await ReceiveAsync(ws1, timeoutMs: 3000);
            var notif2 = await ReceiveAsync(ws2, timeoutMs: 3000);

            Assert.Contains("eth_subscription", notif1);
            Assert.Contains("eth_subscription", notif2);
            Assert.Contains("0x1", notif1);
            Assert.Contains("0x1", notif2);

            await ws1.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            await ws2.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }

        [Fact]
        public async Task WebSocket_RpcCall_EthBlockNumber_WorksOverWs()
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);

            await _sequencer.ProduceBlockAsync();
            await _sequencer.ProduceBlockAsync();

            await SendAsync(ws, JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = 42,
                method = "eth_blockNumber",
                @params = Array.Empty<object>()
            }));

            var response = JsonDocument.Parse(await ReceiveAsync(ws));
            Assert.Equal(42, response.RootElement.GetProperty("id").GetInt32());
            Assert.Equal("0x2", response.RootElement.GetProperty("result").GetString());

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }

        [Fact]
        public async Task WebSocket_NewHeads_BlockHasExpectedFields()
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);

            await SendAsync(ws, JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = 1,
                method = "eth_subscribe",
                @params = new[] { "newHeads" }
            }));
            await ReceiveAsync(ws);

            await _sequencer.ProduceBlockAsync();
            await Task.Delay(200);

            var notification = JsonDocument.Parse(await ReceiveAsync(ws, timeoutMs: 3000));
            var block = notification.RootElement.GetProperty("params").GetProperty("result");

            Assert.True(block.TryGetProperty("number", out _));
            Assert.True(block.TryGetProperty("hash", out _));
            Assert.True(block.TryGetProperty("parentHash", out _));
            Assert.True(block.TryGetProperty("stateRoot", out _));
            Assert.True(block.TryGetProperty("gasLimit", out _));
            Assert.True(block.TryGetProperty("gasUsed", out _));
            Assert.True(block.TryGetProperty("timestamp", out _));

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }

        [Fact]
        public async Task WebSocket_LogSubscription_SubscribeSucceeds()
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);

            await SendAsync(ws, JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = 1,
                method = "eth_subscribe",
                @params = new object[] { "logs", new { } }
            }));
            var subResponse = JsonDocument.Parse(await ReceiveAsync(ws));
            Assert.True(subResponse.RootElement.TryGetProperty("result", out var subIdProp));
            Assert.StartsWith("0x", subIdProp.GetString());
            Assert.False(subResponse.RootElement.TryGetProperty("error", out _));

            await SendAsync(ws, JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = 2,
                method = "eth_unsubscribe",
                @params = new[] { subIdProp.GetString() }
            }));
            var unsubResponse = JsonDocument.Parse(await ReceiveAsync(ws));
            Assert.True(unsubResponse.RootElement.GetProperty("result").GetBoolean());

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }

        [Fact]
        public async Task WebSocket_InvalidMethod_ReturnsError()
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);

            await SendAsync(ws, JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = 1,
                method = "nonexistent_method",
                @params = Array.Empty<object>()
            }));

            var response = JsonDocument.Parse(await ReceiveAsync(ws));
            Assert.True(response.RootElement.TryGetProperty("error", out var error));
            Assert.Equal(-32601, error.GetProperty("code").GetInt32());

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }

        private static async Task SendAsync(ClientWebSocket ws, string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static async Task<string> ReceiveAsync(ClientWebSocket ws, int timeoutMs = 5000)
        {
            var buffer = new byte[8192];
            using var cts = new CancellationTokenSource(timeoutMs);
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
            return Encoding.UTF8.GetString(buffer, 0, result.Count);
        }

        private static async Task<string?> TryReceiveAsync(ClientWebSocket ws, int timeoutMs = 1000)
        {
            try
            {
                var buffer = new byte[8192];
                using var cts = new CancellationTokenSource(timeoutMs);
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                return Encoding.UTF8.GetString(buffer, 0, result.Count);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        private static int FindFreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
