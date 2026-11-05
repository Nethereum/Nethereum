using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests;

[Collection("Sequential")]
public class EngineOnlyServerProcessTests : IDisposable
{
    private static readonly TimeSpan ReadyDeadline = TimeSpan.FromSeconds(90);

    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"engine_only_process_{Guid.NewGuid():N}");

    public EngineOnlyServerProcessTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task Given_the_benchmarkoor_command_line_When_the_server_process_serves_a_devchain_payload_Then_the_chain_follows_it_and_debug_setHead_rolls_it_back()
    {
        await using var author = await DevChainAuthor.CreateAsync(CustomGenesisFixture.Document());
        var block = await author.ProduceAsync();

        var secret = RandomNumberGenerator.GetBytes(32);
        var jwtPath = Path.Combine(_workDir, "jwtsecret");
        File.WriteAllText(jwtPath, Convert.ToHexString(secret).ToLowerInvariant());
        var genesisPath = Path.Combine(_workDir, "genesis.json");
        File.WriteAllText(genesisPath, CustomGenesisFixture.Json());
        var rpcPort = FreePort();
        var enginePort = FreePort();

        using var server = StartServer(
            $"--data-dir={Path.Combine(_workDir, "data")}", "--host=127.0.0.1", $"--port={rpcPort}",
            "--MainnetChain:RpcModules=eth,net,web3,debug", "--engine", $"--engine-port={enginePort}", $"--engine-jwt-secret={jwtPath}", $"--genesis={genesisPath}");

        using var http = new HttpClient();
        var rpc = $"http://127.0.0.1:{rpcPort}";
        var engine = $"http://127.0.0.1:{enginePort}";

        var chainId = await WaitForAsync(() => CallAsync(http, rpc, null, "eth_chainId"), server);
        Assert.Equal("0x" + CustomGenesisFixture.ChainId.ToString("x"), chainId.Value<string>());
        var genesisBlock = await CallAsync(http, rpc, null, "eth_getBlockByNumber", "0x0", false);
        Assert.Equal(author.GenesisHash.ToHex(true), genesisBlock["hash"]!.Value<string>());

        var token = MintToken(secret);
        var genesisState = JObject.FromObject(DevChainAuthor.StateOf(author.GenesisHash));
        await WaitForAsync(async () =>
        {
            var status = await CallAsync(http, engine, token, "engine_forkchoiceUpdatedV3", genesisState, null);
            return status["payloadStatus"]!["status"]!.Value<string>() == "VALID"
                ? status
                : throw new InvalidOperationException("not valid yet");
        }, server);

        var payload = JObject.FromObject(block.Payload);
        var requests = JArray.FromObject(block.ExecutionRequests);
        var imported = await CallAsync(http, engine, token, "engine_newPayloadV4", payload, new JArray(), DevChainAuthor.ZeroRoot, requests);
        Assert.Equal("VALID", imported["status"]!.Value<string>());
        var adopted = await CallAsync(http, engine, token, "engine_forkchoiceUpdatedV3",
            JObject.FromObject(DevChainAuthor.StateOf(block.Hash)), null);
        Assert.Equal("VALID", adopted["payloadStatus"]!["status"]!.Value<string>());
        Assert.Equal("0x1", (await CallAsync(http, rpc, null, "eth_blockNumber")).Value<string>());
        var imported1 = await CallAsync(http, rpc, null, "eth_getBlockByNumber", "0x1", false);
        Assert.Equal(block.StateRoot.ToHex(true), imported1["stateRoot"]!.Value<string>());

        await CallAsync(http, rpc, null, "debug_setHead", "0x0");
        Assert.Equal("0x0", (await CallAsync(http, rpc, null, "eth_blockNumber")).Value<string>());

        var replayed = await CallAsync(http, engine, token, "engine_newPayloadV4", payload, new JArray(), DevChainAuthor.ZeroRoot, requests);
        Assert.Equal("VALID", replayed["status"]!.Value<string>());
    }

    private static async Task<JToken> CallAsync(HttpClient http, string url, string? token, string method, params object?[] parameters)
    {
        var body = new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = method,
            ["params"] = new JArray(parameters.Select(p => p == null ? JValue.CreateNull() : JToken.FromObject(p)))
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json")
        };
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request);
        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
        if (json["error"] != null) throw new InvalidOperationException($"{method}: {json["error"]}");
        return json["result"]!;
    }

    private static async Task<T> WaitForAsync<T>(Func<Task<T>> probe, ServerProcess server)
    {
        var deadline = Stopwatch.StartNew();
        Exception? last = null;
        while (deadline.Elapsed < ReadyDeadline)
        {
            if (server.Process.HasExited)
                throw new InvalidOperationException("the server exited:\n" + server.Output);
            try { return await probe(); }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException) { last = ex; }
            await Task.Delay(250);
        }
        throw new TimeoutException("the server never became ready: " + last + "\n" + server.Output);
    }

    private sealed class ServerProcess : IDisposable
    {
        private readonly ConcurrentQueue<string> _lines;

        public ServerProcess(Process process, ConcurrentQueue<string> lines)
        {
            Process = process;
            _lines = lines;
        }

        public Process Process { get; }

        public string Output => string.Join("\n", _lines);

        public void Dispose()
        {
            try { if (!Process.HasExited) Process.Kill(entireProcessTree: true); } catch { }
            Process.Dispose();
        }
    }

    private ServerProcess StartServer(params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Nethereum.MainnetChain.Server.dll"));
        foreach (var arg in args) startInfo.ArgumentList.Add(arg);

        var lines = new ConcurrentQueue<string>();
        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) lines.Enqueue(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lines.Enqueue(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return new ServerProcess(process, lines);
    }

    private static string MintToken(byte[] secret)
    {
        var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Base64Url(Encoding.UTF8.GetBytes("{\"iat\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "}"));
        using var hmac = new HMACSHA256(secret);
        var signature = Base64Url(hmac.ComputeHash(Encoding.ASCII.GetBytes(header + "." + payload)));
        return header + "." + payload + "." + signature;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
