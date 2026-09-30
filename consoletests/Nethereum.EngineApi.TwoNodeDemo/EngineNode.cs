using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;

namespace Nethereum.EngineApi.TwoNodeDemo;

public sealed class BlockInfo
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

public sealed class ForkchoiceResult
{
    public ForkchoiceResult(string payloadStatus, string? payloadId)
    {
        PayloadStatus = payloadStatus;
        PayloadId = payloadId;
    }

    public string PayloadStatus { get; }
    public string? PayloadId { get; }
}

public sealed class NewPayloadResult
{
    public NewPayloadResult(string status, string latestValidHash)
    {
        Status = status;
        LatestValidHash = latestValidHash;
    }

    public string Status { get; }
    public string LatestValidHash { get; }
}

public sealed class PayloadEnvelope
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

public sealed class EngineNode : IAsyncDisposable
{
    private static readonly HttpClient Client = new HttpClient();
    private static int _nextId = 1;

    private readonly int _port;
    private readonly int _enginePort;
    private readonly int _chainId;
    private readonly string _jwtSecretPath;
    private WebApplication? _app;
    private Task? _runTask;

    public EngineNode(int port, int enginePort, int chainId)
    {
        _port = port;
        _enginePort = enginePort;
        _chainId = chainId;
        _jwtSecretPath = Path.Combine(Path.GetTempPath(), $"engine-api-two-node-demo-jwt-{Guid.NewGuid():N}.hex");
    }

    private string Url => $"http://127.0.0.1:{_port}";
    private string EngineUrl => $"http://127.0.0.1:{_enginePort}";

    public async Task StartAsync()
    {
        var config = new DevChainServerConfig
        {
            Port = _port,
            ChainId = _chainId,
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
        return MintToken(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    private static string MintToken(byte[] secret, long iat)
    {
        var headerJson = JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" });
        var payloadJson = JsonSerializer.Serialize(new { iat });

        var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        using var hmac = new HMACSHA256(secret);
        var signature = hmac.ComputeHash(Encoding.ASCII.GetBytes(headerB64 + "." + payloadB64));
        var signatureB64 = Base64UrlEncode(signature);

        return headerB64 + "." + payloadB64 + "." + signatureB64;
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

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
            id = Interlocked.Increment(ref _nextId),
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
