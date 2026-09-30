using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Nethereum.Node.HarnessServer;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Fixtures
{
    public class HarnessGenesisJsonFixture : IAsyncLifetime
    {
        private WebApplication? _app;
        private Task? _runTask;

        public const int Port = 18570;
        public const int EnginePort = 18571;
        public const int ChainId = 7777;

        public string Url => $"http://127.0.0.1:{Port}";
        public string EngineUrl => $"http://127.0.0.1:{EnginePort}";

        public string DataDir { get; } =
            Path.Combine(Path.GetTempPath(), $"harness-genesis-{Guid.NewGuid():N}");

        public string GenesisPath { get; }

        public string JwtSecretPath { get; }

        public string FundedAddress { get; } = "0x0000000000000000000000000000000000aabbcc";

        public HarnessGenesisJsonFixture()
        {
            Directory.CreateDirectory(DataDir);
            GenesisPath = Path.Combine(DataDir, "genesis.json");
            JwtSecretPath = Path.Combine(DataDir, "jwt.hex");

            File.WriteAllText(GenesisPath, BuildGenesisJson());
        }

        private string BuildGenesisJson() => $$"""
        {
            "config": {
                "chainId": {{ChainId}},
                "homesteadBlock": 0,
                "eip150Block": 0,
                "eip155Block": 0,
                "eip158Block": 0,
                "byzantiumBlock": 0,
                "constantinopleBlock": 0,
                "petersburgBlock": 0,
                "istanbulBlock": 0,
                "berlinBlock": 0,
                "londonBlock": 0,
                "terminalTotalDifficulty": 0,
                "shanghaiTime": 0,
                "cancunTime": 0
            },
            "alloc": {
                "{{FundedAddress}}": { "balance": "0x152D02C7E14AF6800000" }
            },
            "gasLimit": "0x1C9C380",
            "baseFeePerGas": "0x3B9ACA00",
            "timestamp": "0x0",
            "extraData": "0x",
            "difficulty": "0x0",
            "coinbase": "0x0000000000000000000000000000000000000000"
        }
        """;

        public async Task InitializeAsync()
        {
            var options = new HarnessServerOptions
            {
                DataDir = DataDir,
                HttpAddr = "127.0.0.1",
                HttpPort = Port,
                AuthRpcAddr = "127.0.0.1",
                AuthRpcPort = EnginePort,
                JwtSecretPath = JwtSecretPath,
                GenesisPath = GenesisPath,
            };

            _app = await HarnessNodeHost.BuildAsync(options, Array.Empty<string>());

            _runTask = Task.Run(async () =>
            {
                try { await _app.RunAsync(); }
                catch (OperationCanceledException) { }
            });

            await WaitForServerReadyAsync();
        }

        public async Task DisposeAsync()
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

            try { Directory.Delete(DataDir, recursive: true); }
            catch (IOException) { }
        }

        private async Task WaitForServerReadyAsync(int maxRetries = 50)
        {
            using var client = new HttpClient();
            for (var i = 0; i < maxRetries; i++)
            {
                try
                {
                    var content = new StringContent(
                        "{\"jsonrpc\":\"2.0\",\"method\":\"eth_chainId\",\"params\":[],\"id\":1}",
                        Encoding.UTF8,
                        "application/json");
                    var response = await client.PostAsync(Url, content);
                    if (response.IsSuccessStatusCode) return;
                }
                catch (HttpRequestException) { }
                await Task.Delay(100);
            }

            throw new Exception($"Harness server did not become ready at {Url}");
        }

        public string MintValidToken(long? iat = null)
        {
            var secretHex = File.ReadAllText(JwtSecretPath).Trim();
            var secret = Convert.FromHexString(secretHex);
            return MintToken(secret, iat ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public static string MintToken(byte[] secret, long iat)
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
    }
}
