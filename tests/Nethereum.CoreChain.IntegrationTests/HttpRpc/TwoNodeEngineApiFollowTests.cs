using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.HttpRpc
{
    [Collection("EngineApi")]
    public class TwoNodeEngineApiFollowTests : IAsyncLifetime
    {
        private const int BlockCount = 5;
        private const int ChainId = 31337;

        private readonly ITestOutputHelper _output;
        private readonly EngineNode _producer = new EngineNode(18590, 18591);
        private readonly EngineNode _follower = new EngineNode(18592, 18593);

        public TwoNodeEngineApiFollowTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public async Task InitializeAsync()
        {
            await _producer.StartAsync();
            await _follower.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await _producer.DisposeAsync();
            await _follower.DisposeAsync();
        }

        [Fact]
        public async Task Given_TwoIndependentDevChainNodes_When_TheProducerBuildsAndTheFollowerAdopts_Then_TheFollowerReconstructsTheProducersChainOverTheEngineApi()
        {
            var producerGenesis = await _producer.GetBlockByNumberAsync(0);
            var followerGenesis = await _follower.GetBlockByNumberAsync(0);
            Assert.Equal(producerGenesis.Hash, followerGenesis.Hash);

            var parentBeaconBlockRoot = "0x" + new string('0', 64);
            var feeRecipient = "0x0000000000000000000000000000000000009999";

            var headHash = producerGenesis.Hash;
            var timestamp = producerGenesis.Timestamp;
            var targetGasLimit = producerGenesis.GasLimit;

            for (var blockNumber = 1; blockNumber <= BlockCount; blockNumber++)
            {
                timestamp += 1;
                var prevRandao = "0x" + blockNumber.ToString("x").PadLeft(64, '0');

                var attributes = new
                {
                    timestamp = "0x" + timestamp.ToString("x"),
                    prevRandao,
                    suggestedFeeRecipient = feeRecipient,
                    withdrawals = Array.Empty<object>(),
                    parentBeaconBlockRoot,
                    slotNumber = "0x" + blockNumber.ToString("x"),
                    targetGasLimit = "0x" + targetGasLimit.ToString("x")
                };

                var buildResult = await _producer.ForkchoiceUpdatedAsync(headHash, attributes);
                Assert.Equal("VALID", buildResult.PayloadStatus);
                Assert.False(string.IsNullOrEmpty(buildResult.PayloadId));

                var payload = await _producer.GetPayloadAsync(buildResult.PayloadId!);
                Assert.Equal(blockNumber, payload.Number);
                var blockHash = payload.BlockHash;

                var producerAdopt = await _producer.ForkchoiceUpdatedAsync(blockHash, attributes: null);
                Assert.Equal("VALID", producerAdopt.PayloadStatus);

                var followerImport = await _follower.NewPayloadAsync(payload, parentBeaconBlockRoot);
                Assert.Equal("VALID", followerImport.Status);
                Assert.Equal(blockHash, followerImport.LatestValidHash);

                var followerAdopt = await _follower.ForkchoiceUpdatedAsync(blockHash, attributes: null);
                Assert.Equal("VALID", followerAdopt.PayloadStatus);

                var producerHeadNumber = await _producer.GetBlockNumberAsync();
                var followerHeadNumber = await _follower.GetBlockNumberAsync();
                Assert.Equal(blockNumber, producerHeadNumber);
                Assert.Equal(blockNumber, followerHeadNumber);

                _output.WriteLine(
                    $"block {blockNumber} | produced {blockHash.Substring(0, 10)} feeRecipient={feeRecipient} | " +
                    $"follower newPayload={followerImport.Status} | heads: producer={producerHeadNumber} follower={followerHeadNumber}");

                headHash = blockHash;
            }

            var producerFinalHeadNumber = await _producer.GetBlockNumberAsync();
            var followerFinalHeadNumber = await _follower.GetBlockNumberAsync();
            Assert.Equal(BlockCount, producerFinalHeadNumber);
            Assert.Equal(BlockCount, followerFinalHeadNumber);

            for (var blockNumber = 0; blockNumber <= BlockCount; blockNumber++)
            {
                var producerBlock = await _producer.GetBlockByNumberAsync(blockNumber);
                var followerBlock = await _follower.GetBlockByNumberAsync(blockNumber);
                Assert.Equal(producerBlock.Hash, followerBlock.Hash);
            }
        }

        private readonly struct BlockInfo
        {
            public BlockInfo(string hash, long timestamp, long gasLimit)
            {
                Hash = hash;
                Timestamp = timestamp;
                GasLimit = gasLimit;
            }

            public string Hash { get; }
            public long Timestamp { get; }
            public long GasLimit { get; }
        }

        private readonly struct ForkchoiceResult
        {
            public ForkchoiceResult(string payloadStatus, string? payloadId)
            {
                PayloadStatus = payloadStatus;
                PayloadId = payloadId;
            }

            public string PayloadStatus { get; }
            public string? PayloadId { get; }
        }

        private readonly struct NewPayloadResult
        {
            public NewPayloadResult(string status, string latestValidHash)
            {
                Status = status;
                LatestValidHash = latestValidHash;
            }

            public string Status { get; }
            public string LatestValidHash { get; }
        }

        private readonly struct PayloadEnvelope
        {
            public PayloadEnvelope(int number, string blockHash, Dictionary<string, JsonElement> raw, List<string> executionRequests)
            {
                Number = number;
                BlockHash = blockHash;
                Raw = raw;
                ExecutionRequests = executionRequests;
            }

            public int Number { get; }
            public string BlockHash { get; }
            public Dictionary<string, JsonElement> Raw { get; }
            public List<string> ExecutionRequests { get; }
        }

        private sealed class EngineNode : IAsyncDisposable
        {
            private static readonly HttpClient Client = new HttpClient();
            private static int _nextId = 1;

            private readonly int _port;
            private readonly int _enginePort;
            private readonly string _jwtSecretPath;
            private WebApplication? _app;
            private Task? _runTask;

            public EngineNode(int port, int enginePort)
            {
                _port = port;
                _enginePort = enginePort;
                _jwtSecretPath = Path.Combine(Path.GetTempPath(), $"two-node-engine-jwt-{Guid.NewGuid():N}.hex");
            }

            private string Url => $"http://127.0.0.1:{_port}";
            private string EngineUrl => $"http://127.0.0.1:{_enginePort}";

            public async Task StartAsync()
            {
                var config = new DevChainServerConfig
                {
                    Port = _port,
                    ChainId = ChainId,
                    Hardfork = "amsterdam",
                    Storage = "memory",
                    AutoMine = false,
                    EngineApiEnabled = true,
                    EngineJwtSecretPath = _jwtSecretPath,
                    EnginePort = _enginePort
                };

                var builder = WebApplication.CreateBuilder();
                builder.Services.AddDevChainServer(config);
                builder.Services.AddCors(options =>
                    options.AddDefaultPolicy(policy =>
                        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
                builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

                _app = builder.Build();
                _app.Urls.Add(Url);
                await _app.MapDevChainEndpointsAsync();

                _runTask = Task.Run(async () =>
                {
                    try { await _app.RunAsync(); }
                    catch (OperationCanceledException) { }
                });

                await WaitForServerReadyAsync();
            }

            public async ValueTask DisposeAsync()
            {
                if (_app != null)
                {
                    await _app.StopAsync();
                    await _app.DisposeAsync();
                }

                if (_runTask != null)
                {
                    try { await _runTask; }
                    catch (OperationCanceledException) { }
                }

                try { File.Delete(_jwtSecretPath); }
                catch (IOException) { }
            }

            private async Task WaitForServerReadyAsync(int maxRetries = 50)
            {
                for (var i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        var result = await PostPlainAsync("eth_chainId", Array.Empty<object>());
                        if (result.Status == HttpStatusCode.OK) return;
                    }
                    catch (HttpRequestException) { }
                    await Task.Delay(100);
                }

                throw new Exception($"Server did not become ready at {Url}");
            }

            private string MintValidToken()
            {
                var secretHex = File.ReadAllText(_jwtSecretPath).Trim();
                var secret = Convert.FromHexString(secretHex);
                return EngineApiHttpFixture.MintToken(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }

            public async Task<BlockInfo> GetBlockByNumberAsync(long number)
            {
                var result = await PostPlainAsync("eth_getBlockByNumber", new object[] { "0x" + number.ToString("x"), false });
                var block = result.Body.GetProperty("result");
                var hash = block.GetProperty("hash").GetString()!;
                var timestamp = Convert.ToInt64(block.GetProperty("timestamp").GetString(), 16);
                var gasLimit = Convert.ToInt64(block.GetProperty("gasLimit").GetString(), 16);
                return new BlockInfo(hash, timestamp, gasLimit);
            }

            public async Task<long> GetBlockNumberAsync()
            {
                var result = await PostPlainAsync("eth_blockNumber", Array.Empty<object>());
                return Convert.ToInt64(result.Body.GetProperty("result").GetString(), 16);
            }

            public async Task<ForkchoiceResult> ForkchoiceUpdatedAsync(string headHash, object? attributes)
            {
                var forkchoiceState = new
                {
                    headBlockHash = headHash,
                    safeBlockHash = headHash,
                    finalizedBlockHash = headHash
                };

                var result = await PostEngineAsync("engine_forkchoiceUpdatedV4", new object[] { forkchoiceState, attributes! });
                Assert.Equal(HttpStatusCode.OK, result.Status);
                var response = result.Body.GetProperty("result");
                var payloadStatus = response.GetProperty("payloadStatus").GetProperty("status").GetString()!;
                var payloadId = response.TryGetProperty("payloadId", out var idElement) && idElement.ValueKind == JsonValueKind.String
                    ? idElement.GetString()
                    : null;

                return new ForkchoiceResult(payloadStatus, payloadId);
            }

            public async Task<PayloadEnvelope> GetPayloadAsync(string payloadId)
            {
                var result = await PostEngineAsync("engine_getPayloadV6", new object[] { payloadId });
                Assert.Equal(HttpStatusCode.OK, result.Status);
                var response = result.Body.GetProperty("result");
                var payload = response.GetProperty("executionPayload");

                var number = Convert.ToInt32(payload.GetProperty("blockNumber").GetString(), 16);
                var blockHash = payload.GetProperty("blockHash").GetString()!;
                var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload.GetRawText())!;
                var executionRequests = new List<string>();
                foreach (var request in response.GetProperty("executionRequests").EnumerateArray())
                {
                    executionRequests.Add(request.GetString()!);
                }

                return new PayloadEnvelope(number, blockHash, raw, executionRequests);
            }

            public async Task<NewPayloadResult> NewPayloadAsync(PayloadEnvelope payload, string parentBeaconBlockRoot)
            {
                var result = await PostEngineAsync(
                    "engine_newPayloadV5",
                    new object[] { payload.Raw, Array.Empty<object>(), parentBeaconBlockRoot, payload.ExecutionRequests });

                Assert.Equal(HttpStatusCode.OK, result.Status);
                var response = result.Body.GetProperty("result");
                var status = response.GetProperty("status").GetString()!;
                var latestValidHash = response.GetProperty("latestValidHash").GetString()!;

                return new NewPayloadResult(status, latestValidHash);
            }

            private Task<RpcHttpResult> PostEngineAsync(string method, object[] parameters) =>
                PostAsync(EngineUrl, method, parameters, MintValidToken());

            private Task<RpcHttpResult> PostPlainAsync(string method, object[] parameters) =>
                PostAsync(Url, method, parameters, null);

            private static async Task<RpcHttpResult> PostAsync(string url, string method, object[] parameters, string? bearerToken)
            {
                var requestBody = JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = _nextId++,
                    method,
                    @params = parameters
                });

                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
                };

                if (bearerToken != null)
                {
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearerToken);
                }

                using var response = await Client.SendAsync(request);
                var responseJson = await response.Content.ReadAsStringAsync();

                var body = string.IsNullOrWhiteSpace(responseJson)
                    ? default
                    : JsonDocument.Parse(responseJson).RootElement.Clone();

                return new RpcHttpResult(response.StatusCode, body);
            }

            private readonly struct RpcHttpResult
            {
                public RpcHttpResult(HttpStatusCode status, JsonElement body)
                {
                    Status = status;
                    Body = body;
                }

                public HttpStatusCode Status { get; }
                public JsonElement Body { get; }
            }
        }
    }
}
